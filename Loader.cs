using System;
using System.IO;
using System.Linq;

namespace ImageViewer
{
	abstract class Loader
	{
		public delegate void FilesLoadedHandler( FileInfo[] files );

		public FilesLoadedHandler FilesLoaded;

		protected string[] IMAGE_EXTENSIONS = { ".jpg", ".jpeg", ".png" };

		public abstract void LoadImageFiles( string dirpath );

		public abstract void LoadImage( string filepath );

		public abstract void CancelLoadImage();

		protected FileInfo[] GetImageFiles( DirectoryInfo dir )
		{
			return (
				from file in dir.GetFiles()
				where Array.IndexOf( IMAGE_EXTENSIONS, Path.GetExtension( file.Name ) ) >= 0
				select file
			).ToArray();
		}
	}
}
