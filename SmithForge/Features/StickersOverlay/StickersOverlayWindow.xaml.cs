using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SmithForge.Features.StickersOverlay
{
    public partial class StickersOverlayWindow : Window
    {
        // --- WinAPI для изменения размера ---
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

        public StickersOverlayWindow()
        {
            InitializeComponent();

            // Добавляем обработчики для отслеживания изменений размера и позиции
            this.SizeChanged += OnSizeChanged;
            this.LocationChanged += OnLocationChanged;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (DataContext is StickersOverlayViewModel vm &&
                Application.Current.MainWindow?.DataContext is SmithForge.ViewModels.MainViewModel mainVm)
            {
                mainVm.SaveStickersPosition();
                System.Diagnostics.Debug.WriteLine($"[StickersWindow] Позиция изменена: {this.Left}, {this.Top}");
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is StickersOverlayViewModel vm &&
                Application.Current.MainWindow?.DataContext is SmithForge.ViewModels.MainViewModel mainVm)
            {
                mainVm.SaveStickersPosition();
                System.Diagnostics.Debug.WriteLine($"[StickersWindow] Размер изменен: {e.NewSize.Width}x{e.NewSize.Height}");
            }
        }

        public void SetClickThrough(bool isClickThrough)
        {
            if (isClickThrough)
            {
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.IsHitTestVisible = false;
                if (DragArea != null) DragArea.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.IsHitTestVisible = true;
                if (DragArea != null) DragArea.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Работает только если включен режим настройки
            if (!(DataContext is StickersOverlayViewModel vm) || !vm.IsSetupMode)
                return;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);
                double t = 10; // Зона захвата края

                bool left = pos.X <= t;
                bool right = pos.X >= this.ActualWidth - t;
                bool top = pos.Y <= t;
                bool bottom = pos.Y >= this.ActualHeight - t;

                if (left || right || top || bottom)
                {
                    if (top && left) ResizeWindow("TopLeft");
                    else if (top && right) ResizeWindow("TopRight");
                    else if (bottom && left) ResizeWindow("BottomLeft");
                    else if (bottom && right) ResizeWindow("BottomRight");
                    else if (left) ResizeWindow("Left");
                    else if (right) ResizeWindow("Right");
                    else if (top) ResizeWindow("Top");
                    else if (bottom) ResizeWindow("Bottom");
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void ResizeWindow(string direction)
        {
            ReleaseCapture();  // Строка 91
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

            SendMessage(new WindowInteropHelper(this).Handle, WM_NCLBUTTONDOWN, hitTest, 0);
        }

        // Синхронизация для сохранения размеров
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged) this.Width = sizeInfo.NewSize.Width;
            if (sizeInfo.HeightChanged) this.Height = sizeInfo.NewSize.Height;
        }
    }
}