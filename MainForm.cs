using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// Главное окно программы
	/// </summary>
	public partial class MainForm : Form
	{
		private Loader Loader;

		public MainForm()
		{
			InitializeComponent();

			Loader = CreateAsyncLoader();
			//Loader = CreateSyncLoader();

			// загрузка и инициализация дерева папок
			DirectoriesTree.Load();
			DirectoriesTree.AfterDirectorySelect += DirectoriesTree_AfterDirectorySelect;

			// инициализация списка файлов в выбранной папке
			ImagesList.SelectedIndexChanged += ImagesList_SelectedIndexChanged;

			// добавление картинок
			ImageList imageList = new ImageList();
			imageList.Images.Add( "folder-opened", LoadEmbeddedImage( "ImageViewer.res.folder-opened.png" ) );
			imageList.Images.Add( "folder-closed", LoadEmbeddedImage( "ImageViewer.res.folder-closed.png" ) );

			DirectoriesTree.ImageList = imageList;
			DirectoriesTree.ImageKey = "folder-closed";
			DirectoriesTree.SelectedImageKey = "folder-opened";
		}

		/// <summary>
		/// Создать экземпляр синхронного "загрузчика"
		/// </summary>
		/// <returns></returns>
		private Loader CreateSyncLoader()
		{
			SyncLoader loader = new SyncLoader( PictureBox );
			loader.FilesLoaded += Loader_FilesLoaded;
			return loader;
		}

		/// <summary>
		/// Создать экезмпляр асинхронного "загрузчика"
		/// </summary>
		/// <returns></returns>
		private Loader CreateAsyncLoader()
		{
			AsyncLoader loader = new AsyncLoader( this );
			loader.FilesLoaded += Loader_FilesLoaded;
			loader.ImageLoaded += Loader_ImageLoaded;
			return loader;
		}

		/// <summary>
		/// Получить изображение, внедрённое в программу как ресурс
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		private Image LoadEmbeddedImage( string name )
		{
			return Image.FromStream( Assembly.GetExecutingAssembly().GetManifestResourceStream( name ) );
		}

		/// <summary>
		/// Обработчик пункта "Выход" в главном меню.
		/// Вызывает закрытие главного окна и завершение приложения.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ExitMenuItem_Click( object sender, System.EventArgs e )
		{
			Close();
		}

		/// <summary>
		/// Обработчик выбора папки в дереве папок.
		/// Использует загрузчик для загрузки списка файлов в папке
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void DirectoriesTree_AfterDirectorySelect( object sender, DirectoryTreeViewEventArgs args )
		{
			Loader.LoadImageFiles( args.DirectoryPath );
		}

		/// <summary>
		/// Обработчик выбора файла в списке файлов.
		/// Использует загрузчик для загрузки изображения из файла.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ImagesList_SelectedIndexChanged( object sender, EventArgs e )
		{
			if( ImagesList.SelectedItems.Count == 1 )
			{
				string filepath = ( string )ImagesList.SelectedItems[0].Tag;
				Loader.LoadImage( filepath );
			}
			else
			{
				PictureBox.Image = null;
				Loader.CancelLoadImage();
			}
		}

		/// <summary>
		/// Обработчик, принимающий от загрузчика список файлов в папке
		/// </summary>
		/// <param name="files"></param>
		private void Loader_FilesLoaded( FileInfo[] files)
		{
			// показываем список файлов в ImagesList
			ImagesList.Items.Clear();
			foreach( FileInfo file in files )
			{
				ListViewItem item = new ListViewItem( file.Name );
				item.Tag = file.FullName;
				ImagesList.Items.Add( item );
			}

			// руками вызываем обработчик изменения выбранного элемента списка
			ImagesList_SelectedIndexChanged( null, null );
		}

		/// <summary>
		/// Обработчик, принимающий от загрузчика изображение, загруженное из файла
		/// </summary>
		/// <param name="image"></param>
		private void Loader_ImageLoaded( Image image )
		{
			PictureBox.Image = image;
		}
	}
}
