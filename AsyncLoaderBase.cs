using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

namespace ImageViewer
{
	abstract class AsyncLoaderBase : Loader
	{
		public delegate void ImageLoadedHandler( Image image );

		public ImageLoadedHandler ImageLoaded;

		private Control Context;

		public AsyncLoaderBase( Control context )
		{
			Context = context;
		}

		protected void DoLoadFiles( string dirpath )
		{
			FileInfo[] files = { };

			try { files = GetImageFiles( new DirectoryInfo( dirpath ) ); } catch { }

			Context.BeginInvoke( FilesLoaded, ( object )files );
		}

		protected void DoCancelLoadImage()
		{
			Context.BeginInvoke( ImageLoaded, ( Image )null );
		}

		protected void DoLoadImage( string filepath )
		{
			Image image = null;
			
			try { image = Image.FromFile( filepath ); } catch { }

			Context.BeginInvoke( ImageLoaded, image );
		}
	}
}
