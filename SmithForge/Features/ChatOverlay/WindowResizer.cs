using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace SmithForge.Features.ChatOverlay
{
    public static class WindowResizer
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTLEFT = 0xA;
        private const int HTRIGHT = 0xB;
        private const int HTTOP = 0xC;
        private const int HTTOPLEFT = 0xD;
        private const int HTTOPRIGHT = 0xE;
        private const int HTBOTTOM = 0xF;
        private const int HTBOTTOMLEFT = 0x10;
        private const int HTBOTTOMRIGHT = 0x11;

        public static void ResizeWindow(Window window, string direction)
        {
            ReleaseCapture();
            int hitTest = direction switch
            {
                "Left" => HTLEFT,
                "Right" => HTRIGHT,
                "Top" => HTTOP,
                "Bottom" => HTBOTTOM,
                "TopLeft" => HTTOPLEFT,
                "TopRight" => HTTOPRIGHT,
                "BottomLeft" => HTBOTTOMLEFT,
                "BottomRight" => HTBOTTOMRIGHT,
                _ => HTLEFT
            };

            SendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle,
                WM_NCLBUTTONDOWN, hitTest, 0);
        }
    }
}