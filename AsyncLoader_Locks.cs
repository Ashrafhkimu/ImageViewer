using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace ImageViewer
{
	class AsyncLoader_Locks : AsyncLoaderBase
	{
		struct DataStruct
		{
			public bool NeedLoadImageFiles;
			public bool NeedLoadImage;
			public string ImageFilesDir;
			public string ImageFilePath;
		}

		private DataStruct Data;
		
		private object _lock = new object();

		public AsyncLoader_Locks( Control context ) : base( context )
		{
			Thread thread = new Thread( ThreadFunction );
			thread.IsBackground = true;
			thread.Start();
		}

		public override void LoadImageFiles( string dirpath )
		{
			lock( _lock )
			{
				Data.ImageFilesDir = dirpath;
				Data.NeedLoadImageFiles = true;
				Monitor.Pulse( _lock );
			}
		}

		public override void LoadImage( string filepath )
		{
			lock( _lock )
			{
				Data.ImageFilePath = filepath;
				Data.NeedLoadImage = true;
				Monitor.Pulse( _lock );
			}
		}

		public override void CancelLoadImage()
		{
			lock( _lock )
			{
				Data.ImageFilePath = null;
				Data.NeedLoadImage = true;
				Monitor.Pulse( _lock );
			}
		}

		private void ThreadFunction()
		{
			while( true )
			{
				DataStruct dataCopy;
				lock( _lock )
				{
					if( ! Data.NeedLoadImageFiles && ! Data.NeedLoadImage )
						Monitor.Wait( _lock );
				
					dataCopy = Data;
					Data.NeedLoadImageFiles = false;
					Data.NeedLoadImage = false;
				}

				if( dataCopy.NeedLoadImageFiles )
					DoLoadFiles( dataCopy.ImageFilesDir );

				if( dataCopy.NeedLoadImage )
				{
					if( dataCopy.ImageFilePath == null )
						DoCancelLoadImage();
					else
						DoLoadImage( dataCopy.ImageFilePath );
				}
			}
		}
	}
}
