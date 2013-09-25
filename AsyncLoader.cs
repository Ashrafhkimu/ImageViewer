using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
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
	//    - Для загрузки полноразмерного изображения используется Task.
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
		/// Количество запросов загрузки thumbnail'ов, которое рекомендуется
		/// передавать в одну задачу (в метод LoadThumbnails).
		/// Чем меньше это значение, тем быстрее пользователь заметит прогрузку
		/// thumbnail'ов, но тем медленнее загрузка thumbnail'ов будет работать в целом
		/// </summary>
		public override int RecommendedThumbnailTaskRequestsCount { get { return 5; } }

		/// <summary>
		/// Количество задач по загрузке thumbnail'ов, которое рекомендуется запускать
		/// одновременно (количество вызовов LoadThumbnails).
		/// Чем больше это значение, тем быстрее будут загружаться thumbnail'ы, но тем
		/// сильнее будет загружаться GUI-поток.  Неразумно делать это значение сильно
		/// бОльшим, чем количество ядер процессора.
		/// </summary>
		public override int RecommendedThumbnailTasksCount { get { return 2; } }

		/// <summary>
		/// Контрол WinForms, используемый для выполнения обработчиков в GUI-потоке
		/// </summary>
		private Control Context;

		/// <summary>
		/// Объект отмены для задач по загрузке thumbnail'ов
		/// </summary>
		private CancellationTokenSource ThumbnailsCts;

		/// <summary>
		/// Задача загрузки полноразмерного изображения
		/// </summary>
		private Task ImageTask;

		/// <summary>
		/// Объект отмены для задачи загрузки полноразмерного изображения
		/// </summary>
		private CancellationTokenSource ImageCts;

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
		}

		/// <summary>
		/// Добавить запросы в очередь на загрузку thumbnail'ов.
		/// Результаты возвращаются через обработчик ThumbnailsLoaded.
		/// </summary>
		/// <param name="requests"></param>
		public override void LoadThumbnails( ThumbnailRequest[] requests )
		{
			if( requests.Length > 0 )
			{
				if( ThumbnailsCts == null )
					ThumbnailsCts = new CancellationTokenSource();
				CancellationToken ct = ThumbnailsCts.Token;

				ThreadPool.QueueUserWorkItem( delegate {
					DoLoadThumbnails( requests, ct );
				});
			}
		}

		/// <summary>
		/// Отменить текущие задачи по загрузке thumbnail'ов
		/// </summary>
		public override void CancelLoadThumbnails()
		{
			// отмена предыдущего запроса
			if( ThumbnailsCts != null )
				ThumbnailsCts.Cancel();
			ThumbnailsCts = null;
		}

		/// <summary>
		/// Загрузить изображение из указанного файла.
		/// Загруженное изображение возвращается через обработчик ImageLoaded.
		/// </summary>
		/// <param name="filepath">Полный путь к файлу</param>
		public override void LoadImage( string filepath )
		{
			// отмена предыдущей задачи
			if( ImageCts != null )
				ImageCts.Cancel();

			ImageCts = new CancellationTokenSource();

			// запуск новой задачи
			CancellationToken ct = ImageCts.Token;
			Task previousTask = ImageTask;
			ImageTask = new Task( delegate { DoLoadImage( filepath, ct, previousTask ); }, ct );
			ImageTask.Start();
		}

		/// <summary>
		/// Отменить загрузку изображения.
		/// Вызов этого метода отменяет действие метода LoadImage, в особенности
		/// когда загрузка изображения ещё не успела выполниться.
		/// После отмены вызывается обработчик ImageLoaded с параметром image = null.
		/// </summary>
		public override void CancelLoadImage()
		{
			// отмена предыдущей задачи
			if( ImageCts != null )
				ImageCts.Cancel();

			ImageCts = null;

			if( ImageTask != null )
			{
				// запуск новой задачи
				// передача сообщения в GUI-поток должна выполниться после завершения предыдущей задачи!
				ImageTask = ImageTask.ContinueWith( previousTask => {
					Context.BeginInvoke( ImageLoaded, ( Image )null );
				});
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
					responses.Add( new ThumbnailResponse { Request = request, Thumbnail = thumbnail } );
			}

			if(	! ct.IsCancellationRequested )
			{
				// передача загруженных thumbnail'ов GUI-потоку
				Context.BeginInvoke( ThumbnailsLoaded, ( object )responses.ToArray() );
			}
			else
			{
				// чистка thumbnail'ов, которые успели подгрузить
				foreach( ThumbnailResponse response in responses )
					response.Thumbnail.Dispose();
			}
		}

		/// <summary>
		/// Реализация загрузки изображения из файла.
		/// Выполняется во вспомогательном потоке
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="ct">Объект отмены</param>
		/// <param name="previousTask">
		/// Предыдущая задача загрузки или отмены изображения,
		/// или null если такой задачи нет
		/// </param>
		private void DoLoadImage( string filepath, CancellationToken ct, Task previousTask )
		{
			if( ct.IsCancellationRequested )
				return;

			Image image = null;
			try
			{
				image = Image.FromFile( filepath );
			}
			catch( Exception e )
			{
				Debug.WriteLine( e );
			}

			if( ct.IsCancellationRequested )
				return;

			// передача сообщения в GUI-поток должна выполниться после завершения предыдущей задачи,
			// чтобы не получилось, что GUI-поток получит изображение, запрошенное ранее,
			// вместо изображения, запрошенного последним
			if( previousTask != null )
				previousTask.Wait();

			// при ошибке загрузки изображения обработчик вызывается с аргументом image = null
			Context.BeginInvoke( ImageLoaded, image );
		}
	}
}
