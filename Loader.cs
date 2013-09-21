using System;
using System.IO;
using System.Linq;

namespace ImageViewer
{
	/// <summary>
	/// Базовый класс загрузчика, для возможности разделения синхронной и асинхронной реализации
	///   1) загрузки списка файлов-изображений в указанной папке
	///   2) загрузки изображения из файла
	/// </summary>
	abstract class Loader
	{
		/// <summary>
		/// Обработчик загрузки списка файлов
		/// </summary>
		/// <param name="files">Список файлов</param>
		public delegate void FilesLoadedHandler( FileInfo[] files );

		/// <summary>
		/// Обработчик загрузки списка файлов
		/// </summary>
		public FilesLoadedHandler FilesLoaded;

		/// <summary>
		/// Список расширений.  Если название файла имеет одно из
		/// перечисленных расширений, файл считается изображением
		/// </summary>
		protected string[] IMAGE_EXTENSIONS = { ".jpg", ".jpeg", ".png" };

		/// <summary>
		/// Загрузить список файлов-изображений в указанной папке.
		/// Результаты возвращаются через обработчик FilesLoaded.
		/// </summary>
		/// <param name="dirpath">Полный путь к папке</param>
		public abstract void LoadImageFiles( string dirpath );

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
		/// Получить список файлов-изображений в указанной папке.
		/// Вспомогательный метод для наследников
		/// </summary>
		/// <param name="dir">Папка</param>
		/// <returns>Список файлов-изображений</returns>
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
