using System.IO;
using System.Windows.Forms;
using System;
using System.Drawing;

namespace ImageViewer
{
	public partial class MainForm : Form
	{
		Loader Loader;

		public MainForm()
		{
			InitializeComponent();

			//Loader = CreateAsyncLoader();
			Loader = CreateSyncLoader();

			DirectoriesTree.Load();
			DirectoriesTree.AfterDirectorySelect += DirectoriesTree_AfterDirectorySelect;

			ImagesList.SelectedIndexChanged += ImagesList_SelectedIndexChanged;
		}

		private Loader CreateSyncLoader()
		{
			SyncLoader loader = new SyncLoader( PictureBox );
			loader.FilesLoaded += Loader_FilesLoaded;
			return loader;
		}

		private Loader CreateAsyncLoader()
		{
			AsyncLoader_MessageQueue loader = new AsyncLoader_MessageQueue( this );
			loader.FilesLoaded += Loader_FilesLoaded;
			loader.ImageLoaded += Loader_ImageLoaded;
			return loader;
		}

		private void ExitMenuItem_Click( object sender, System.EventArgs e )
		{
			Close();
		}

		private void DirectoriesTree_AfterDirectorySelect( object sender, DirectoryTreeViewEventArgs args )
		{
			Loader.LoadImageFiles( args.DirectoryPath );
		}

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

		private void Loader_FilesLoaded( FileInfo[] files)
		{
			ImagesList.Items.Clear();
			foreach( FileInfo file in files )
			{
				ListViewItem item = new ListViewItem( file.Name );
				item.Tag = file.FullName;
				ImagesList.Items.Add( item );
			}
		}

		private void Loader_ImageLoaded( Image image )
		{
			PictureBox.Image = image;
		}
	}
}
