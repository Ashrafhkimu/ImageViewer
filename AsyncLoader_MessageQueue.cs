using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ImageViewer
{
	class AsyncLoader_MessageQueue : Loader
	{
		public delegate void ImageLoadedHandler( Image image );

		public ImageLoadedHandler ImageLoaded;

		private Control Context;

		private Thread Thread;

		private int ThreadId;
		
		private const uint MSG_LOAD_FILES = PInvoke.WM_USER + 1;
		private const uint MSG_LOAD_IMAGE = PInvoke.WM_USER + 2;

		public AsyncLoader_MessageQueue( Control context )
		{
			Context = context;

			Thread = new Thread( ThreadFunction );
			Thread.IsBackground = true;
			Thread.Start();
			ThreadId = GetNativeThreadId( Thread );
		}

		public override void LoadImageFiles( string dirpath )
		{
			PInvoke.PostThreadMessage( ( uint )ThreadId, MSG_LOAD_FILES, Marshal.StringToBSTR( dirpath ), new IntPtr() );
		}

		public override void LoadImage( string filepath )
		{
			PInvoke.PostThreadMessage( ( uint )ThreadId, MSG_LOAD_IMAGE, Marshal.StringToBSTR( filepath ), new IntPtr() );
		}

		public override void CancelLoadImage()
		{
			PInvoke.PostThreadMessage( ( uint )ThreadId, MSG_LOAD_IMAGE, new IntPtr(), new IntPtr() );
		}

		// <http://stackoverflow.com/questions/1679243/getting-the-thread-id-from-a-thread>
		private static int GetNativeThreadId( Thread thread )
		{
			FieldInfo field = typeof( Thread ).GetField( "DONT_USE_InternalThread",
				BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
			IntPtr internalThread = (IntPtr)field.GetValue( thread );
			int nativeId = Marshal.ReadInt32( internalThread, ( IntPtr.Size == 8 ) ? 548 : 348 ); // found by analyzing the memory
			return nativeId;
		}

		private void ThreadFunction()
		{
			while( PInvoke.MsgWaitForMultipleObjects( 0, new IntPtr[0], false, PInvoke.INFINITE,
				( uint )PInvoke.Flags.QS_ALLPOSTMESSAGE ) != PInvoke.WAIT_FAILED )
			{
				bool needLoadImageFiles = false;
				string imageFilesDir = null;
				bool needLoadImage = false;
				string imageFilePath = null;

				PInvoke.NativeMessage message;
				while( PInvoke.PeekMessage( out message, new HandleRef(), 0, 0, ( int )PInvoke.Flags.PM_REMOVE ) )
				{
					if( message.msg == MSG_LOAD_FILES )
					{
						needLoadImageFiles = true;
						imageFilesDir = Marshal.PtrToStringBSTR( message.wParam );
						Marshal.FreeBSTR( message.wParam );
					}
					else if( message.msg == MSG_LOAD_IMAGE )
					{
						needLoadImage = true;
						if( ( int )message.wParam == 0 )
							imageFilePath = null;
						else
						{
							imageFilePath = Marshal.PtrToStringBSTR( message.wParam );
							Marshal.FreeBSTR( message.wParam );
						}
					}
				}

				if( needLoadImageFiles )
					DoLoadFiles( imageFilesDir );

				if( needLoadImage )
				{
					if( imageFilePath == null )
						Context.BeginInvoke( ImageLoaded, ( Image )null );
					else
						DoLoadImage( imageFilePath );
				}
			}
		}

		private void DoLoadFiles( string dirpath )
		{
			FileInfo[] files = { };

			try { files = GetImageFiles( new DirectoryInfo( dirpath ) ); } catch { }

			Context.BeginInvoke( FilesLoaded, ( object )files );
		}

		private void DoLoadImage( string filepath )
		{
			Image image = null;
			
			try { image = Image.FromFile( filepath ); } catch { }

			Context.BeginInvoke( ImageLoaded, image );
		}
	}
}
