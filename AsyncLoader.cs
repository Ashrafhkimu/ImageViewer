using System;
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
	///   2) изображение, загруженное из файла, возвращается через обработчик ImageLoaded
	/// </summary>
	// Подробности реализации:
	//    - Асинхронность обеспечивается запуском отдельного потока.  Передача заданий потоку и синхронизация
	//      реализованы с использованием "мониторов", а именно блоков lock(...) и методов класса Monitor.
	//    - Для каждого типа заданий (загрузка thumbnail'ов, загрузка изображения из файла) нас интересует только
	//      последнее полученное задание, поэтому для хранения используются просто поля, а не очередь.
	//    - Возвращение значений в GUI-поток реализовано с использованием метода System.Windows.Forms.Control.BeginInvoke,
	//      который позволяет удобно выполнить указанный обработчик в GUI-потоке.
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
		/// Структура данных для хранения заданий, поставленных загрузчику
		/// </summary>
		private struct DataStruct
		{
			public bool NeedLoadThumbnails;
			public ThumbnailRequest[] ThumbnailRequests;

			public bool NeedLoadImage;
			public string ImageFilePath;
		}

		/// <summary>
		/// Описание заданий, поставленных загрузчику
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
		/// Конструктор
		/// </summary>
		/// <param name="context">
		/// Контрол WinForms, используемый для выполнения обработчиков в GUI-потоке.
		/// Не принципиально какой именно это контрол, важно что он управляется WinForms.
		/// </param>
		public AsyncLoader( Control context )
		{
			Context = context;

			// Запуск потока для асинхронного выполнения заданий.
			// Должен быть фоновым (background), чтобы его наличие не блокировало завершение процесса.
			Thread thread = new Thread( ThreadFunction );
			thread.IsBackground = true;
			thread.Start();
		}

		/// <summary>
		/// Загрузить список thumbnail'ов.
		/// Отменяет предыдущий запрос на загрузку thumbnail'ов
		/// Результаты возвращаются через обработчик ThumbnailLoaded.
		/// </summary>
		/// <param name="requests"></param>
		public override void LoadThumbnails( Loader.ThumbnailRequest[] requests )
		{
			lock( _lock )
			{
				Data.ThumbnailRequests = requests;
				Data.NeedLoadThumbnails = true;
				Monitor.Pulse( _lock );
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
			DataStruct data = new DataStruct();
			data.ThumbnailRequests = new ThumbnailRequest[0];

			// индекс thumbnail'а в массиве ThumbnailRequests, который нужно обработать следующим
			int thumbnailIndex = 0;

			while( true )
			{
				lock( _lock )
				{
					// если заданий ещё нет, блокируем поток в ожидании их поступления
					if( ! ( thumbnailIndex < data.ThumbnailRequests.Length ) && ! Data.NeedLoadThumbnails && ! Data.NeedLoadImage )
						Monitor.Wait( _lock );
				
					// копируем "слепок" заданий на текущий момент, для дальнейшего использования вне блокировки
					data = Data;

					// если поставлено новое задание на загрузку thumbnail'ов, необходимо сбросить индекс,
					// чтобы новый массив thumbnail'ов начал обрабатываться с начала
					if( Data.NeedLoadThumbnails )
						thumbnailIndex = 0;

					Data.NeedLoadThumbnails = false;
					Data.NeedLoadImage = false;
				}

				// Выполнение задания на загрузку thumbnail'ов.
				// За одну итерацию обрабатывается один thumbnail, чтобы не блокировать поток надолго
				// (например, пока мы обрабатываем текущий массив thumbnail'ов, может быть
				// поставлено задание на обработку другого)
				if( ThumbnailLoaded != null && thumbnailIndex < data.ThumbnailRequests.Length )
				{
					DoLoadThumbnail( data.ThumbnailRequests[ thumbnailIndex ] );
					++thumbnailIndex;
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
		/// Реализация загрузки thumbnail'а.
		/// Выполняется во вспомогательном потоке
		/// </summary>
		/// <param name="request"></param>
		private new void DoLoadThumbnail( ThumbnailRequest request )
		{
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
				Context.BeginInvoke( ThumbnailLoaded, request, thumbnail );
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
