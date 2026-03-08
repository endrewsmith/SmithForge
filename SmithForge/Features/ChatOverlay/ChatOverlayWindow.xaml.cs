using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace SmithForge.Features.ChatOverlay
{
    public partial class ChatOverlayWindow : Window
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

        private bool _isDragging = false;

        public ChatOverlayWindow()
        {
            InitializeComponent();
        }

        public void SetClickThrough(bool isClickThrough)
        {
            if (isClickThrough)
            {
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.IsHitTestVisible = false;
                DragArea.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.IsHitTestVisible = true;
                DragArea.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(DataContext is ChatOverlayViewModel vm) || !vm.IsSetupMode)
                return;

            Point pos = e.GetPosition(this);
            double tolerance = 10;

            bool left = pos.X <= tolerance;
            bool right = pos.X >= this.Width - tolerance;
            bool top = pos.Y <= tolerance;
            bool bottom = pos.Y >= this.Height - tolerance;

            // Если на краю - меняем размер
            if (left || right || top || bottom)
            {
                if (top && left)
                    ResizeWindow("TopLeft");
                else if (top && right)
                    ResizeWindow("TopRight");
                else if (bottom && left)
                    ResizeWindow("BottomLeft");
                else if (bottom && right)
                    ResizeWindow("BottomRight");
                else if (left)
                    ResizeWindow("Left");
                else if (right)
                    ResizeWindow("Right");
                else if (top)
                    ResizeWindow("Top");
                else if (bottom)
                    ResizeWindow("Bottom");
            }
            else
            {
                // Если в центре - перетаскиваем
                this.DragMove();
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (DataContext is ChatOverlayViewModel vm && vm.IsSetupMode)
            {
                Point pos = e.GetPosition(this);
                double tolerance = 10;

                bool left = pos.X <= tolerance;
                bool right = pos.X >= this.Width - tolerance;
                bool top = pos.Y <= tolerance;
                bool bottom = pos.Y >= this.Height - tolerance;

                if (top && left)
                    this.Cursor = Cursors.SizeNWSE;
                else if (top && right)
                    this.Cursor = Cursors.SizeNESW;
                else if (bottom && left)
                    this.Cursor = Cursors.SizeNESW;
                else if (bottom && right)
                    this.Cursor = Cursors.SizeNWSE;
                else if (left || right)
                    this.Cursor = Cursors.SizeWE;
                else if (top || bottom)
                    this.Cursor = Cursors.SizeNS;
                else
                    this.Cursor = Cursors.Arrow;
            }
        }

        private void ResizeWindow(string direction)
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

            SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                WM_NCLBUTTONDOWN, hitTest, 0);
        }
    }
}