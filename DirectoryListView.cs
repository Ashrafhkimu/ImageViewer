using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// ListView для отображения содержимого папки на файловой системе.
	/// Согласно заданию, в окне отображаются только подпапки и графические файлы.
	/// </summary>
	class DirectoryListView : ListView
	{
		/// <summary>
		/// Список расширений.  Если название файла имеет одно из
		/// перечисленных расширений, файл считается изображением
		/// </summary>
		private string[] IMAGE_EXTENSIONS = { ".jpg", ".jpeg", ".png" };

		/// <summary>
		/// Загрузчик
		/// </summary>
		private Loader Loader;

		/// <summary>
		/// Thumbnail по умолчанию для графических файлов
		/// </summary>
		private Image DefaultImageThumbnail;

		/// <summary>
		/// ImageList для thumbnail'ов
		/// </summary>
		private ImageList Thumbnails;

		/// <summary>
		/// Проинициализировать компонент.
		/// Необходимо вызвать этот метод перед использованием компонента
		/// </summary>
		/// <param name="loader">Загрузчик</param>
		/// <param name="defaultImageThumbnail">Thumbnail по умолчанию для графических файлов</param>
		public void InitializeComponent( Loader loader, Image defaultImageThumbnail )
		{
			Loader = loader;
			DefaultImageThumbnail = defaultImageThumbnail;

			Thumbnails = new ImageList();
			Thumbnails.ImageSize = new Size( Loader.ThumbnailSize, Loader.ThumbnailSize );
			LargeImageList = Thumbnails;

			Loader.ThumbnailLoaded += Loader_ThumbnailLoaded;
		}

		/// <summary>
		/// Отобразить содержимое указанной папки
		/// </summary>
		/// <param name="dirpath">Полный путь к папке</param>
		public void LoadDirectory( string dirpath )
		{
			Items.Clear();

			// очистка старых thumbnail'ов, чтобы не занимали память
			Thumbnails.Images.Clear();
			Thumbnails.Images.Add( "image", DefaultImageThumbnail );

			List<ThumbnailRequest> requests = new List<ThumbnailRequest>();

			try
			{
				DirectoryInfo dir = new DirectoryInfo( dirpath );

				// обработка графических файлов в папке
				foreach( FileInfo file in dir.GetFiles() )
					if( Array.IndexOf( IMAGE_EXTENSIONS, Path.GetExtension( file.Name ) ) >= 0 )
					{
						ListViewItem item = new ListViewItem( file.Name );
						item.ImageKey = "image"; // пока не загружен thumbnail, используем thumbnail по умолчанию
						item.Tag = file.FullName;
						item = Items.Add( item );

						// добавляем задание на загрузку thumbnail'а для этого файла
						requests.Add( new ThumbnailRequest { FilePath = file.FullName, ItemIndex = item.Index } );
					}
			}
			catch( Exception e )
			{
				Debug.WriteLine( e );
			}

			Loader.LoadThumbnails( requests.ToArray() );
		}

		/// <summary>
		/// Обработчик загрузки thumbnail'а.
		/// Проставляем thumbnail соответствующему элементу списка.
		/// </summary>
		/// <param name="files"></param>
		private void Loader_ThumbnailLoaded( Loader.ThumbnailRequest _request, Image thumbnail )
		{
			ThumbnailRequest request = ( ThumbnailRequest ) _request;
			using( thumbnail )
			{
				// Обработчик загрузки thumbnail'а может сработать после того как содержимое окна поменялось.
				// Элемента, для которого загрузили thumbnail, уже может не быть в списке.
				if( request.ItemIndex < Items.Count )
				{	 
					ListViewItem item = Items[ request.ItemIndex ];
					// проверка, что thumbnail и элемент соответствуют одному файлу
					if( ( string )item.Tag == request.FilePath )
					{
						// добавляем thumbnail в ImageList и используем его для элемента
						Thumbnails.Images.Add( request.FilePath, thumbnail );
						item.ImageKey = request.FilePath;
					}
				}
			}
		}

		/// <summary>
		/// Задание на загрузку thumbnail'а, которое передаём загрузчику.
		/// Добавлены дополнительные поля, которые используем в обработчике загрузки thumbnail'а.
		/// </summary>
		class ThumbnailRequest : Loader.ThumbnailRequest
		{
			/// <summary>
			/// Позиция элемента ListView, которому необходимо проставить загруженный thumbnail
			/// </summary>
			public int ItemIndex;
		}
	}
}
