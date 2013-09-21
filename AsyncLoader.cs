using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// Асинхронная реализация загрузчика.
	/// В этой реализации
	///   1) список файлов-изображений в указанной папке возвращается через обработчик FilesLoaded
	///   2) изображение, загруженное из файла, возвращается через обработчик ImageLoaded
	/// </summary>
	// Подробности реализации:
	//    - Асинхронность обеспечивается запуском отдельного потока.  Передача заданий потоку и синхронизация
	//      реализованы с использованием "мониторов", а именно блоков lock(...) и методов класса Monitor.
	//    - Для каждого типа заданий (загрузка списка файлов, загрузка изображения из файла) нас интересует только
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
			public bool NeedLoadImageFiles;
			public bool NeedLoadImage;
			public string ImageFilesDir;
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
		/// Загрузить список файлов-изображений в указанной папке.
		/// Результаты возвращаются через обработчик FilesLoaded.
		/// </summary>
		/// <param name="dirpath">Полный путь к папке</param>
		public override void LoadImageFiles( string dirpath )
		{
			lock( _lock )
			{
				Data.ImageFilesDir = dirpath;
				Data.NeedLoadImageFiles = true;
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
			while( true )
			{
				DataStruct dataCopy;
				lock( _lock )
				{
					// если заданий ещё нет, блокируем поток в ожидании их поступления
					if( ! Data.NeedLoadImageFiles && ! Data.NeedLoadImage )
						Monitor.Wait( _lock );
				
					// копируем "слепок" заданий на текущий момент, для дальнейшего использования вне блокировки
					dataCopy = Data;

					Data.NeedLoadImageFiles = false;
					Data.NeedLoadImage = false;
				}

				// выполнение заданий
				if( FilesLoaded != null && dataCopy.NeedLoadImageFiles )
					DoLoadFiles( dataCopy.ImageFilesDir );

				if( ImageLoaded != null && dataCopy.NeedLoadImage )
				{
					if( dataCopy.ImageFilePath == null )
						DoCancelLoadImage();
					else
						DoLoadImage( dataCopy.ImageFilePath );
				}
			}
		}

		/// <summary>
		/// Реализации загрузки списка файлов-изображений в указанной папке.
		/// Выполняется во вспомогательном потоке
		/// </summary>
		/// <param name="dirpath"></param>
		private void DoLoadFiles( string dirpath )
		{
			FileInfo[] files = { };

			try { files = GetImageFiles( new DirectoryInfo( dirpath ) ); } catch { }

			Context.BeginInvoke( FilesLoaded, ( object )files );
		}

		/// <summary>
		/// Реализация загрузки изображения из файла.
		/// Выполняется во вспомогательном потоке
		/// </summary>
		/// <param name="filepath"></param>
		private void DoLoadImage( string filepath )
		{
			Image image = null;
			
			try { image = Image.FromFile( filepath ); } catch { }

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
