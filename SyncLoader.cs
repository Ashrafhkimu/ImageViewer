using System.IO;
using System.Windows.Forms;

namespace ImageViewer
{
	class SyncLoader : Loader
	{
		PictureBox PictureBox;

		public SyncLoader( PictureBox pictureBox )
		{
			PictureBox = pictureBox;
		}

		public override void LoadImageFiles( string dirpath )
		{
			FileInfo[] files = { };
			
			try { files = GetImageFiles( new DirectoryInfo( dirpath ) ); } catch { }

			FilesLoaded( files );
		}

		public override void LoadImage( string filepath )
		{
			PictureBox.ImageLocation = filepath;
		}

		public override void CancelLoadImage()
		{
			PictureBox.Image = null;
		}
	}
}
