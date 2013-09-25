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
		/// Номер первого по порядку элемента списка, для которого ещё не загрузили thumbnail.
		/// </summary>
		private int NextSequentialThumbnailIndex = 0;

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

			try
			{
				DirectoryInfo dir = new DirectoryInfo( dirpath );

				// обработка подпапок
				foreach( DirectoryInfo subdir in dir.GetDirectories() )
				{
					ListViewItem item = new ListViewItem( subdir.Name );
					item.ImageKey = "folder";
					item.Tag = new ItemInfo( subdir.FullName );
					Items.Add( item );
				}

				// обработка графических файлов в папке
				foreach( FileInfo file in dir.GetFiles() )
					if( Array.IndexOf( IMAGE_EXTENSIONS, Path.GetExtension( file.Name ) ) >= 0 )
					{
						ListViewItem item = new ListViewItem( file.Name );
						item.ImageKey = "image"; // пока не загружен thumbnail, используем thumbnail по умолчанию
						item.Tag = new ItemInfo( file.FullName );
						item = Items.Add( item );
					}
			}
			catch( Exception e )
			{
				Debug.WriteLine( e );
			}

			CurrentDirectoryPath = dirpath;

			Loader.CancelLoadThumbnails();
			NextSequentialThumbnailIndex = 0;
			for( int i = 0; i < Loader.RecommendedThumbnailTasksCount; i++ )
				LoadNextThumbnails();
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
						filepath = ( ( ItemInfo )item.Tag ).FilePath;
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
					 FolderActivated( ( ( ItemInfo )item.Tag ).FilePath );
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
		/// Добавляет очередную задачу по загрузке thumbnail'ов.
		/// </summary>
		/// <param name="responses"></param>
		private void Loader_ThumbnailsLoaded( Loader.ThumbnailResponse[] responses )
		{
			// признак того, что обработчик вызван устаревшей задачей
			bool outdated = true;

			foreach( var response in responses )
			{
				ThumbnailRequest request = ( ThumbnailRequest ) response.Request;
				using( response.Thumbnail )
				{
					// если обработчик вызван устаревшей задачей, содержимое окна поменялось и
					// элемента, для которого загрузили thumbnail, уже может не быть в списке.
					if( request.ItemIndex < Items.Count )
					{	 
						ListViewItem item = Items[ request.ItemIndex ];
						// проверка, что это именно тот элемент списка, для которого вызывали загрузку thumbnail'а
						if( request.Tag == item.Tag )
						{
							// добавляем thumbnail в ImageList и используем его для элемента
							Thumbnails.Images.Add( request.FilePath, response.Thumbnail );
							item.ImageKey = request.FilePath;
							outdated = false;
						}
					}
				}
			}

			// Добавление очередной задачи по загрузке thumbnail'ов.
			// Не делаем, если обработчик вызван устаревшей задачей,
			// потому что для нового содержимого списка уже создали новые задачи.
			if( ! outdated )
				LoadNextThumbnails();
		}

		/// <summary>
		/// Добавить очередную задачу по загрузке thumbnail'ов
		/// </summary>
		private void LoadNextThumbnails()
		{
			List<ThumbnailRequest> requests = new List<ThumbnailRequest>();

			// сначала добавляем задания на загрузку thumbnail'ов для файлов, которые видит пользователь
			ListViewItem topItem = GetItemAt( ClientRectangle.X + 10, ClientRectangle.Y + 10 ); 
			if( topItem != null )
			{
				int i = Math.Max( topItem.Index, NextSequentialThumbnailIndex );
				GetSequentialThumbnailRequests( requests, ref i );
				if( NextSequentialThumbnailIndex >= topItem.Index )
					NextSequentialThumbnailIndex = i;
			}

			// если для файлов, которые видит пользователи, thumbnail'ы уже загружены (или загружаются),
			// выполняем последовательную загрузку thumbnail'ов
			if( requests.Count == 0 )
				GetSequentialThumbnailRequests( requests, ref NextSequentialThumbnailIndex );

			if( requests.Count > 0 )
				Loader.LoadThumbnails( requests.ToArray() );
		}

		/// <summary>
		/// Добавить в указанный список запросы на загрузку thumbnail'ов для элементов списка начиная с `i`.
		/// Элементы списка, которые не являются графическими файлами или thumnail'ы для которых уже загружены
		/// (или загружаются), пропускаются.
		/// </summary>
		/// <param name="requests"></param>
		/// <param name="i"></param>
		private void GetSequentialThumbnailRequests( List<ThumbnailRequest> requests, ref int i )
		{
			while( i < Items.Count && requests.Count < Loader.RecommendedThumbnailTaskRequestsCount )
			{
				ListViewItem item = Items[i];
				if( item.ImageKey == "image" )
				{
					ItemInfo itemInfo = ( ItemInfo )item.Tag;
					if( ! itemInfo.ThumbnailLoaded )
					{
						requests.Add( new ThumbnailRequest { FilePath = itemInfo.FilePath, ItemIndex = i, Tag = item.Tag });
						itemInfo.ThumbnailLoaded = true;
					}
				}
				i++;
			}
		}

		/// <summary>
		/// Структура, описывающая элемент списка
		/// </summary>
		class ItemInfo
		{
			/// <summary>
			/// Полный путь к файлу или папке на файловой системе, которой соответствует элемент
			/// </summary>
			public string FilePath;

			/// <summary>
			/// Для этого элемента загружен (или загружается) thumbnail.
			/// </summary>
			public bool ThumbnailLoaded;

			/// <summary>
			/// Конструктор
			/// </summary>
			/// <param name="filepath">Полный путь к файлу или папке на файловой системе, которой соответствует элемент</param>
			public ItemInfo( string filepath )
			{
				FilePath = filepath;
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

			/// <summary>
			/// Вспомогательный объект для определения, что это именно тот
			/// элемент списка, для которого вызывали загрузку thumbnail'а
			/// </summary>
			public object Tag;
		}
	}
}
