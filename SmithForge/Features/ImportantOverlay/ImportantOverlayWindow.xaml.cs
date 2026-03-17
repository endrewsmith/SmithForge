using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTBOTTOMRIGHT = 0x11;

        public ImportantOverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                ReleaseCapture();
                SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    WM_NCLBUTTONDOWN, HTBOTTOMRIGHT, 0);
            }
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
    }
}