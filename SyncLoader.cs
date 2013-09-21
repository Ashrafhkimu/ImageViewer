using System.IO;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// Синхронная реализация загрузчика.
	/// В этой реализации
	///   1) список файлов-изображений в указанной папке возвращается через обработчик FilesLoaded
	///   2) изображение, загруженное из файла, отображается в переданном контроле PictureBox
	/// </summary>
	class SyncLoader : Loader
	{
		/// <summary>
		/// Контрол для показа изображений, загруженных из файлов
		/// </summary>
		private PictureBox PictureBox;

		/// <summary>
		/// Конструктор
		/// </summary>
		/// <param name="pictureBox">Контрол для показа изображений, загруженных из файла</param>
		public SyncLoader( PictureBox pictureBox )
		{
			PictureBox = pictureBox;
		}

		/// <summary>
		/// Загрузить список файлов-изображений в указанной папке.
		/// Результаты возвращаются через обработчик FilesLoaded.
		/// </summary>
		/// <param name="dirpath">Полный путь к папке</param>
		public override void LoadImageFiles( string dirpath )
		{
			if( FilesLoaded == null )
				return;

			FileInfo[] files = { };
			
			try { files = GetImageFiles( new DirectoryInfo( dirpath ) ); } catch { }

			FilesLoaded( files );
		}

		/// <summary>
		/// Загрузить изображение из указанного файла в PictureBox
		/// </summary>
		/// <param name="filepath">Полный путь к файлу</param>
		public override void LoadImage( string filepath )
		{
			PictureBox.ImageLocation = filepath;
		}

		/// <summary>
		/// Отменить загрузку изображения.
		/// Вызов этого метода отменяет действие метода LoadImage
		/// </summary>
		public override void CancelLoadImage()
		{
			PictureBox.Image = null;
		}
	}
}
