﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ImageViewer
{
	/// <summary>
	/// Синхронная реализация загрузчика.
	/// В этой реализации
	///   1) загруженные thumbnail'ы возвращаются через обработчик ThumbnailLoaded
	///   2) полноразмерное изображение отображается в переданном контроле PictureBox
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
		/// Добавить запросы в очередь на загрузку thumbnail'ов.
		/// Результаты возвращаются через обработчик ThumbnailsLoaded.
		/// </summary>
		/// <param name="requests"></param>
		public override void LoadThumbnails( ThumbnailRequest[] requests )
		{
			if( ThumbnailsLoaded != null )
				foreach( ThumbnailRequest request in requests )
				{
					Image thumbnail = null;
					try
					{
						thumbnail = DoLoadThumbnail( request );
					}
					catch( Exception e )
					{
						Debug.WriteLine( e );
					}

					if( thumbnail != null )
					{
						// для большей интерактивности вызываем обработчик на каждый загруженный thumbnail
						ThumbnailsLoaded( new[] { new ThumbnailResponse { Request = request, Thumbnail = thumbnail } } );
					}
				}
		}

		/// <summary>
		/// Отменить текущие задачи по загрузке thumbnail'ов
		/// </summary>
		public override void CancelLoadThumbnails()
		{
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
