using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayWindow : Window
    {
        private bool _isHidden = false;
        // Импорт WinAPI для изменения размера окна "на лету"
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;

        // Константы направлений изменения размера
        private const int HTLEFT = 0xA;
        private const int HTRIGHT = 0xB;
        private const int HTTOP = 0xC;
        private const int HTTOPLEFT = 0xD;
        private const int HTTOPRIGHT = 0xE;
        private const int HTBOTTOM = 0xF;
        private const int HTBOTTOMLEFT = 0x10;
        private const int HTBOTTOMRIGHT = 0x11;

        public ImportantOverlayWindow()
        {
            InitializeComponent();
            this.SizeChanged += OnSizeChanged;
            this.LocationChanged += OnLocationChanged;
        }
        public void SetHidden(bool isHidden)  // ← ДОБАВИТЬ ЭТОТ МЕТОД
        {
            _isHidden = isHidden;
        }
        private void OnLocationChanged(object sender, EventArgs e)
        {
            // Не сохраняем позицию, если окно скрыто за экраном
            if (_isHidden)
            {
                Debug.WriteLine("[ImportantWindow] LocationChanged: окно скрыто, сохранение пропущено");
                return;
            }

            if (DataContext is ImportantOverlayViewModel vm &&
                Application.Current.MainWindow?.DataContext is SmithForge.ViewModels.MainViewModel mainVm)
            {
                mainVm.SaveImportantPosition();
                System.Diagnostics.Debug.WriteLine($"[ImportantWindow] Позиция изменена: {this.Left}, {this.Top}");
            }
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ✅ Добавить эту проверку
            if (_isHidden)
            {
                Debug.WriteLine("[ImportantWindow] SizeChanged: окно скрыто, сохранение пропущено");
                return;
            }

            // При изменении размера сразу сохраняем в настройки
            if (DataContext is ImportantOverlayViewModel vm &&
                Application.Current.MainWindow?.DataContext is SmithForge.ViewModels.MainViewModel mainVm)
            {
                mainVm.SaveImportantPosition();
                System.Diagnostics.Debug.WriteLine($"[ImportantWindow] Размер изменен: {e.NewSize.Width}x{e.NewSize.Height}");
            }
        }
        public void SetClickThrough(bool isClickThrough)
        {
            if (isClickThrough)
            {
                this.IsHitTestVisible = false;
                DragArea.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.IsHitTestVisible = true;
                DragArea.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Работает только если включен режим настройки
            if (!(DataContext is ImportantOverlayViewModel vm) || !vm.IsSetupMode)
                return;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);
                double t = 10; // Зона захвата края в пикселях

                bool left = pos.X <= t;
                bool right = pos.X >= this.ActualWidth - t;
                bool top = pos.Y <= t;
                bool bottom = pos.Y >= this.ActualHeight - t;

                // Если мышка на краю - запускаем системный ресайз
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
                    // Если в центре - просто перетаскиваем окно
                    this.DragMove();
                }
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

            SendMessage(new WindowInteropHelper(this).Handle, WM_NCLBUTTONDOWN, hitTest, 0);
        }

        // Синхронизируем физический размер окна со свойствами WPF для сохранения в конфиг
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged) this.Width = sizeInfo.NewSize.Width;
            if (sizeInfo.HeightChanged) this.Height = sizeInfo.NewSize.Height;
        }
    }
}
