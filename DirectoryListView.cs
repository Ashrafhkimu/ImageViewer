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
		/// Обработчик выбора элемента в окне
		/// </summary>
		/// <param name="filepath">
		/// Полный путь к объекту на файловой системе, которому соответствует
		/// выбранный элемент, или null если выбранного элемента нет
		/// </param>
		public delegate void ItemHandler( string filepath );

		/// <summary>
		/// Обработчик выбора графического файла в окне.
		/// В качестве параметра обработчику передаётся путь к файлу или null,
		/// если в списке ничего не выбрано, либо выбран не графический файл.
		/// </summary>
		public event ItemHandler ImageActivated;

		/// <summary>
		/// Обработчик выбора папки в окне.
		/// В качестве параметра обработчику передаётся путь к папке.
		/// </summary>
		public event ItemHandler FolderActivated;

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
		/// Thumbnail для папок
		/// </summary>
		private Image FolderThumbnail;

		/// <summary>
		/// ImageList для thumbnail'ов
		/// </summary>
		private ImageList Thumbnails;

		/// <summary>
		/// Путь к текущей отображённой папке
		/// </summary>
		private string CurrentDirectoryPath = "";

		/// <summary>
		/// Проинициализировать компонент.
		/// Необходимо вызвать этот метод перед использованием компонента
		/// </summary>
		/// <param name="loader">Загрузчик</param>
		/// <param name="defaultImageThumbnail">Thumbnail по умолчанию для графических файлов</param>
		/// <param name="folderThumbnail">Thumbnail для папок</param>
		public void InitializeComponent( Loader loader, Image defaultImageThumbnail, Image folderThumbnail )
		{
			Loader = loader;
			DefaultImageThumbnail = defaultImageThumbnail;
			FolderThumbnail = folderThumbnail;

			Thumbnails = new ImageList();
			Thumbnails.ImageSize = new Size( Loader.ThumbnailSize, Loader.ThumbnailSize );
			LargeImageList = Thumbnails;

			Loader.ThumbnailsLoaded += Loader_ThumbnailsLoaded;
		}

		/// <summary>
		/// Отобразить содержимое указанной папки
		/// </summary>
		/// <param name="dirpath">Полный путь к папке</param>
		public void LoadDirectory( string dirpath )
		{
			// игнорируем повторный вызов
			if( CurrentDirectoryPath.TrimEnd( Path.DirectorySeparatorChar ) == dirpath.TrimEnd( Path.DirectorySeparatorChar ) )
				return;

			Items.Clear();

			// очистка старых thumbnail'ов, чтобы не занимали память
			Thumbnails.Images.Clear();
			Thumbnails.Images.Add( "image", DefaultImageThumbnail );
			Thumbnails.Images.Add( "folder", FolderThumbnail );

			List<ThumbnailRequest> requests = new List<ThumbnailRequest>();

			try
			{
				DirectoryInfo dir = new DirectoryInfo( dirpath );

				// обработка подпапок
				foreach( DirectoryInfo subdir in dir.GetDirectories() )
				{
					ListViewItem item = new ListViewItem( subdir.Name );
					item.ImageKey = "folder";
					item.Tag = subdir.FullName;
					Items.Add( item );
				}

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

			CurrentDirectoryPath = dirpath;
		}

		/// <summary>
		/// При возникновении события SelectedIndexChanged вызываем наше собственное ImageActivated,
		/// обработчику которого передаём полный путь к выбранному графическому файлу
		/// </summary>
		/// <param name="e"></param>
		protected override void OnSelectedIndexChanged( EventArgs e )
		{
			if( ImageActivated != null )
			{
				string filepath = null;
				if( SelectedItems.Count > 0 )
				{
					// в списке может быть выбран только один элемент, поэтому достаточно проверить его
					ListViewItem item = SelectedItems[0];
					if( ! ItemIsFolder( item ) )
						filepath = ( string )item.Tag;
				}
				ImageActivated( filepath );
			}

			base.OnSelectedIndexChanged( e );
		}

		/// <summary>
		/// При возникновении события ItemActivate на элементе, соответствующей папке на файловой системе,
		/// вызываем событие FolderActivated
		/// </summary>
		/// <param name="e"></param>
		protected override void OnItemActivate( EventArgs e )
		{
			if( FolderActivated != null )
			{
				ListViewItem item = SelectedItems[0];
				if( ItemIsFolder( item ) )
					 FolderActivated( ( string )item.Tag );
			}

			base.OnItemActivate( e );
		}

		/// <summary>
		/// Соответствует ли элемент списка папке на файловой системе?
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		private bool ItemIsFolder( ListViewItem item )
		{
			return item.ImageKey == "folder";
		}

		/// <summary>
		/// Обработчик загрузки thumbnail'ов.
		/// Проставляет thumbnail'ы соответствующим элементам списка.
		/// </summary>
		/// <param name="responses"></param>
		private void Loader_ThumbnailsLoaded( Loader.ThumbnailResponse[] responses )
		{
			foreach( var response in responses )
			{
				ThumbnailRequest request = ( ThumbnailRequest ) response.Request;
				using( response.Thumbnail )
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
							Thumbnails.Images.Add( request.FilePath, response.Thumbnail );
							item.ImageKey = request.FilePath;
						}
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
