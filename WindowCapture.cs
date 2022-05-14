using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace m8_flow
{
    class WindowCapture
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClientRect(IntPtr hWnd, ref Rect rect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static Bitmap CaptureWindow(Process captureWindow)
        {
            Rectangle bounds;
            var windowHandle = captureWindow.MainWindowHandle;
            var rect = new Rect();

            Point p = new System.Drawing.Point(0, 0);
            GetClientRect(windowHandle, ref rect);
            ClientToScreen(windowHandle, ref p);

            bounds = new Rectangle(p.X, p.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
            Bitmap capture = new Bitmap(bounds.Width, bounds.Height);

            using (var g = Graphics.FromImage(capture))
            {
                g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
            }

            return capture;
        }
    }
}