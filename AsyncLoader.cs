using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// Асинхронная реализация загрузчика.
	/// В этой реализации
	///   1) загруженные thumbnail'ы возвращаются через обработчик ThumbnailLoaded
	///   2) полноразмерное изображение возвращается через обработчик ImageLoaded
	/// </summary>
	// Подробности реализации:
	//    - Возвращение значений в GUI-поток реализовано с использованием метода System.Windows.Forms.Control.BeginInvoke,
	//      который позволяет удобно выполнить указанный обработчик в GUI-потоке.
	//    - Загрузку thumbnail'ов целесообразно выполнять в нескольких потоках,
	//      поэтому используется метод ThreadPool.QueueUserWorkItem.
	//    - Для загрузки полноразмерного изображения используется отдельный вспомогательный поток.
	//      Передача заданий потоку и синхронизация реализованы с использованием "мониторов",
	//      а именно блоков lock(...) и методов класса Monitor.
	class AsyncLoader : Loader
	{
		/// <summary>
		/// Обработчик загрузки изображения из файла или отмены загрузки
		/// </summary>
		/// <param name="image">Загруженное изображение или null, если загрузка отменена</param>
		public delegate void ImageLoadedHandler( Image image );

		/// <summary>
		/// Обработчик загрузки изображения из файла или отмены загрузки
		/// </summary>
		public ImageLoadedHandler ImageLoaded;

		/// <summary>
		/// Контрол WinForms, используемый для выполнения обработчиков в GUI-потоке
		/// </summary>
		private Control Context;

		/// <summary>
		/// Количество thumbnail'ов, которые возвращаются GUI-потоку за один раз.
		/// Чем меньше это значение, тем быстрее пользователь заметит прогрузку
		/// thumbnail'ов, но тем сильнее будет загружаться GUI-поток
		/// (и будет медленнее реагировать на действия пользователя)
		/// </summary>
		private int THUMBNAILS_BUCKET_SIZE = 5;

		/// <summary>
		/// Максимальное количество задач по загрузке thumbnail'ов, которые потенциально
		/// могут выполняться одновременно.  Чем больше это значение, тем быстрее будут
		/// загружаться thumbnail'ы, но тем сильнее будет загружаться GUI-поток.
		/// Неразумно делать это значение сильно бОльшим, чем количество ядер процессора.
		/// </summary>
		private int MAX_THUMBNAIL_TASKS = 2;

		/// <summary>
		/// Структура данных для хранения заданий, поставленных вспомогательному потоку
		/// </summary>
		private struct DataStruct
		{
			public bool NeedLoadImage;
			public string ImageFilePath;
		}

		/// <summary>
		/// Описание заданий, поставленных вспомогательному потоку
		/// </summary>
		// вынесено в структуру, чтобы удобнее было копировать
		// все параметры заданий целиком (см. код ниже)
		private DataStruct Data;
		
		/// <summary>
		/// Объект синхронизации
		/// </summary>
		// не используем для синхронизации this, потому что lock( this ) считается дурной
		// практикой, хотя и безвредной в данной конкретной программе
		private object _lock = new object();

		/// <summary>
		/// Объект отмены для задач по загрузке thumbnail'ов
		/// </summary>
		private CancellationTokenSource ThumbnailsCts;

		/// <summary>
		/// Конструктор
		/// </summary>
		/// <param name="context">
		/// Контрол WinForms, используемый для выполнения обработчиков в GUI-потоке.
		/// Не принципиально какой именно это контрол, важно что он управляется WinForms.
		/// </param>
		public AsyncLoader( Control context )
		{
			Context = context;

			// Запуск потока для асинхронной загрузки полноразмерных изображений.
			// Должен быть фоновым (background), чтобы его наличие не блокировало завершение процесса.
			Thread thread = new Thread( ThreadFunction );
			thread.IsBackground = true;
			thread.Start();
		}

		/// <summary>
		/// Загрузить список thumbnail'ов.
		/// Отменяет предыдущий запрос на загрузку thumbnail'ов
		/// Результаты возвращаются через обработчик ThumbnailsLoaded.
		/// </summary>
		/// <param name="requests"></param>
		public override void LoadThumbnails( ThumbnailRequest[] requests )
		{
			// отмена предыдущего запроса
			if( ThumbnailsCts != null )
				ThumbnailsCts.Cancel();

			ThumbnailsCts = null;
			if( requests.Length > 0 )
			{
				ThumbnailsCts = new CancellationTokenSource();
				CancellationToken ct = ThumbnailsCts.Token;

				List<ThumbnailRequest>[] requestsPerTask = new List<ThumbnailRequest>[ MAX_THUMBNAIL_TASKS ];

				// Разбиваем массив запросов на подмассивы, так чтобы число
				// подмассивов не превышало MAX_THUMBNAIL_TASKS.  Каждый подмассив
				// затем обрабатывается отдельной задачей (task).
				//
				// Так как пользователю важно видеть подгрузку thumbnail'ов, распределяем
				// запросы следующим образом: первые THUMBNAIL_BUCKET_SIZE запросов попадают в первый
				// подмассив, вторые - во второй подмассив и так далее.  Когда подмассивы заканчиваются,
				// по новой начинаем добавлять элементы в первый подмассив и далее.
				//
				int taskIndex = 0;
				for( int i = 0; i < requests.Length; i += THUMBNAILS_BUCKET_SIZE )
				{
					if( requestsPerTask[ taskIndex ] == null )
						requestsPerTask[ taskIndex ] = new List<ThumbnailRequest>();
					for( int j = i; j < Math.Min( i + THUMBNAILS_BUCKET_SIZE, requests.Length ); j++ )
						requestsPerTask[ taskIndex ].Add( requests[j] );
					taskIndex = ( taskIndex + 1 ) % MAX_THUMBNAIL_TASKS;
				}

				foreach( List<ThumbnailRequest> taskRequests in requestsPerTask )
					if( taskRequests != null )
					{
						ThumbnailRequest[] r = taskRequests.ToArray(); // осторожно с замыканиями!
						ThreadPool.QueueUserWorkItem( delegate {
							DoLoadThumbnails( r, ct );
						});
					}
			}
		}

		/// <summary>
		/// Загрузить изображение из указанного файла.
		/// Загруженное изображение возвращается через обработчик ImageLoaded.
		/// </summary>
		/// <param name="filepath">Полный путь к файлу</param>
		public override void LoadImage( string filepath )
		{
			lock( _lock )
			{
				Data.ImageFilePath = filepath;
				Data.NeedLoadImage = true;
				Monitor.Pulse( _lock );
			}
		}

		/// <summary>
		/// Отменить загрузку изображения.
		/// Вызов этого метода отменяет действие метода LoadImage, в особенности
		/// когда загрузка изображения ещё не успела выполниться.
		/// После отмены вызывается обработчик ImageLoaded с параметром image = null.
		/// </summary>
		public override void CancelLoadImage()
		{
			lock( _lock )
			{
				Data.ImageFilePath = null;
				Data.NeedLoadImage = true;
				Monitor.Pulse( _lock );
			}
		}

		/// <summary>
		/// Метод, выполняющийся во вспомогательном потоке
		/// </summary>
		private void ThreadFunction()
		{
			DataStruct data;

			while( true )
			{
				lock( _lock )
				{
					// если заданий ещё нет, блокируем поток в ожидании их поступления
					if( ! Data.NeedLoadImage )
						Monitor.Wait( _lock );
				
					// копируем "слепок" заданий на текущий момент, для дальнейшего использования вне блокировки
					data = Data;

					Data.NeedLoadImage = false;
				}

				// выполнение задания на загрузку полноразмерного изображения
				if( ImageLoaded != null && data.NeedLoadImage )
				{
					if( data.ImageFilePath == null )
						DoCancelLoadImage();
					else
						DoLoadImage( data.ImageFilePath );
				}
			}
		}

		/// <summary>
		/// Реализация загрузки thumbnail'ов
		/// </summary>
		/// <param name="request"></param>
		private void DoLoadThumbnails( ThumbnailRequest[] requests, CancellationToken ct )
		{
			List<ThumbnailResponse> responses = new List<ThumbnailResponse>();

			foreach( ThumbnailRequest request in requests )
			{
				if( ct.IsCancellationRequested )
					break;

				Image thumbnail = null;
				try
				{
					thumbnail = base.DoLoadThumbnail( request );
				}
				catch( Exception e )
				{
					Debug.WriteLine( e );
				}

				if( thumbnail != null )
				{
					responses.Add( new ThumbnailResponse { Request = request, Thumbnail = thumbnail } );

					if( responses.Count >= THUMBNAILS_BUCKET_SIZE )
					{
						// передача загруженных thumbnail'ов GUI-потоку
						Context.BeginInvoke( ThumbnailsLoaded, ( object )responses.ToArray() );
						responses.Clear();
					}
				}
			}

			if(	! ct.IsCancellationRequested && responses.Count > 0 )
			{
				// передача загруженных thumbnail'ов GUI-потоку
				Context.BeginInvoke( ThumbnailsLoaded, ( object )responses.ToArray() );
			}
		}

		/// <summary>
		/// Реализация загрузки изображения из файла.
		/// Выполняется во вспомогательном потоке
		/// </summary>
		/// <param name="filepath"></param>
		private void DoLoadImage( string filepath )
		{
			Image image = null;
			try
			{
				image = Image.FromFile( filepath );
			}
			catch( Exception e )
			{
				Debug.WriteLine( e );
			}

			// при ошибке загрузки изображения обработчик вызывается с аргументов image = null
			Context.BeginInvoke( ImageLoaded, image );
		}

		/// <summary>
		/// Реализация отмены загрузки изображения.
		/// Выполняется во вспомогательном потоке.
		/// </summary>
		private void DoCancelLoadImage()
		{
			Context.BeginInvoke( ImageLoaded, ( Image )null );
		}
	}
}
