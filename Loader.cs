using System;
using System.Drawing;

namespace ImageViewer
{
	/// <summary>
	/// Базовый класс загрузчика, для возможности разделения синхронной и асинхронной реализации
	///   1) загрузки thumbnail'ов изображений из файлов
	///   2) загрузки изображения в полном размере из файла
	/// </summary>
	abstract class Loader
	{
		/// <summary>
		/// Линейный размер thumbnail'а в пикселах
		/// </summary>
		public int ThumbnailSize { get { return 24; } }

		/// <summary>
		/// Запрос загрузки thumbnail'а
		/// </summary>
		public class ThumbnailRequest
		{
			/// <summary>
			/// Полный путь к файлу
			/// </summary>
			public string FilePath;
		}

		/// <summary>
		/// Обработчик загрузки thumbnail'а
		/// </summary>
		/// <param name="request">Исходный запрос</param>
		/// <param name="thumbnail">Загруженный thumbnail</param>
		public delegate void ThumbnailLoadedHandler( ThumbnailRequest request, Image thumbnail );

		/// <summary>
		/// Обработчик загрузки thumbnail'а
		/// </summary>
		public ThumbnailLoadedHandler ThumbnailLoaded;

		/// <summary>
		/// Загрузить список thumbnail'ов.
		/// Отменяет предыдущий запрос на загрузку thumbnail'ов
		/// Результаты возвращаются через обработчик ThumbnailLoaded.
		/// </summary>
		/// <param name="requests"></param>
		public abstract void LoadThumbnails( ThumbnailRequest[] requests );

		/// <summary>
		/// Загрузить изображение из указанного файла.
		/// Как возвращается загруженное изображение, определяется в классе-наследнике.
		/// </summary>
		/// <param name="filepath">Полный путь к файлу</param>
		public abstract void LoadImage( string filepath );

		/// <summary>
		/// Отменить загрузку изображения.
		/// Вызов этого метода отменяет действие метода LoadImage, в особенности
		/// когда загрузка изображения ещё не успела выполниться.
		/// </summary>
		public abstract void CancelLoadImage();

		/// <summary>
		/// Загрузить thumbnail из файла.
		/// Вспомогательный метод для наследников
		/// </summary>
		/// <param name="request">Запрос на загрузку thumbnail'а</param>
		/// <returns>Thumbnail</returns>
		protected Image DoLoadThumbnail( ThumbnailRequest request )
		{
			Image image = Image.FromFile( request.FilePath );

			// Для использования thumbnail'а в ListView / ImageList важно, чтобы thumbnail
			// был всегда указанного размера, даже если изображение на самом деле прямоугольное,
			// иначе ImageList будет самостоятельно растягивать изображение.
			Bitmap thumbnail = new Bitmap( ThumbnailSize, ThumbnailSize );

			int width;
			int height;
			if( image.Width < ThumbnailSize && image.Height < ThumbnailSize )
			{
				// изображение масштабировать не нужно
				width = image.Width;
				height = image.Height;
			}
			else
			{
				// определяем коэффициент масштабирования (во сколько раз надо сжать изображение)
				double xScale = ( ( double )image.Width ) / ThumbnailSize;
				double yScale = ( ( double )image.Height ) / ThumbnailSize;
				double scale = Math.Max( xScale, yScale );
				width = ( int )( image.Width / scale );
				height = ( int )( image.Height / scale );
			}

			// подготовка thumbnail'а
			using( Graphics g = Graphics.FromImage( thumbnail ) )
				g.DrawImage( image, new Rectangle(
					( ThumbnailSize - width ) / 2,
					( ThumbnailSize - height ) / 2,
					width, height ) );

			image.Dispose();
			return thumbnail;
		}
	}
}
