using System;
using System.Runtime.InteropServices;

namespace ImageViewer
{
	class PInvoke
	{
		[Flags]
		public enum Flags : uint
		{
			// MsgWaitForMultipleObjectsParams
			QS_KEY = 0x1,
			QS_MOUSEMOVE = 0x2,
			QS_MOUSEBUTTON = 0x4,
			QS_MOUSE = QS_MOUSEMOVE | QS_MOUSEBUTTON,
			QS_INPUT = QS_MOUSE | QS_KEY,
			QS_POSTMESSAGE = 0x8,
			QS_TIMER = 0x10,
			QS_PAINT = 0x20,
			QS_SENDMESSAGE = 0x40,
			QS_HOTKEY = 0x80,
			QS_REFRESH = QS_HOTKEY | QS_KEY | QS_MOUSEBUTTON | QS_PAINT,
			QS_ALLEVENTS = QS_INPUT | QS_POSTMESSAGE | QS_TIMER | QS_PAINT | QS_HOTKEY,
			QS_ALLINPUT = QS_SENDMESSAGE | QS_PAINT | QS_TIMER | QS_POSTMESSAGE | QS_MOUSEBUTTON | QS_MOUSEMOVE | QS_HOTKEY | QS_KEY,
			QS_ALLPOSTMESSAGE = 0x100,
			QS_RAWINPUT = 0x400,

			// PeekMessage
			PM_NOREMOVE = 0x0000,
			PM_REMOVE = 0x0001,
			PM_NOYIELD = 0x0002,
		}


		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool PostThreadMessage( uint threadId, uint msg, IntPtr wParam, IntPtr lParam );


		public const uint INFINITE = 0xFFFFFFFF;
		
		public const uint WAIT_FAILED = 0xFFFFFFFF;

		[DllImport("user32.dll")]
		public static extern uint MsgWaitForMultipleObjects( uint nCount, IntPtr[] pHandles,
		   bool bWaitAll, uint dwMilliseconds, uint dwWakeMask );

		
		[StructLayout( LayoutKind.Sequential )]
		public struct NativeMessage
		{
			public IntPtr handle;
			public uint msg;
			public IntPtr wParam;
			public IntPtr lParam;
			public uint time;
			public System.Drawing.Point p;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool PeekMessage( out NativeMessage lpMsg, HandleRef hWnd, uint wMsgFilterMin,
		   uint wMsgFilterMax, uint wRemoveMsg );


		public  const uint WM_USER = 0x0400;
	}
}
