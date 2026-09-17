using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SmithForge.Features.AlertsOverlay
{
    /// <summary>
    /// Логика взаимодействия для AlertsOverlayWindow.xaml
    /// </summary>
    public partial class AlertsOverlayWindow : Window
    {
        // WinAPI для перетаскивания и ресайза окна
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

        public AlertsOverlayWindow()
        {
            InitializeComponent();

            // Подписываемся на события изменения размера и позиции
            SizeChanged += OnSizeChanged;
            LocationChanged += OnLocationChanged;
        }

        /// <summary>
        /// Включить / выключить "сквозной режим" (клики проходят сквозь окно)
        /// </summary>
        public void SetClickThrough(bool isClickThrough)
        {
            if (isClickThrough)
            {
                IsHitTestVisible = false;
                if (DragArea != null) DragArea.Visibility = Visibility.Collapsed;
            }
            else
            {
                IsHitTestVisible = true;
                if (DragArea != null) DragArea.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Перетаскивание и ресайз окна (только в режиме настройки)
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(DataContext is AlertsOverlayViewModel vm) || !vm.IsSetupMode)
                return;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(this);
                double t = 10; // Зона захвата края в пикселях

                bool left = pos.X <= t;
                bool right = pos.X >= ActualWidth - t;
                bool top = pos.Y <= t;
                bool bottom = pos.Y >= ActualHeight - t;

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
                    DragMove();
                }
            }
        }

        /// <summary>
        /// Системный ресайз окна через WinAPI
        /// </summary>
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

        /// <summary>
        /// Сохранение позиции при перетаскивании
        /// (раскомментируем на Шаге интеграции, когда добавим SaveAlertsPosition в MainViewModel)
        /// </summary>
        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainVm)
                mainVm.SaveAlertsPosition();
        }

        /// <summary>
        /// Сохранение размера при ресайзе
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainVm)
                mainVm.SaveAlertsPosition();
        }

        /// <summary>
        /// Синхронизируем физический размер окна со свойствами WPF (для сохранения в конфиг)
        /// </summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged) Width = sizeInfo.NewSize.Width;
            if (sizeInfo.HeightChanged) Height = sizeInfo.NewSize.Height;
        }
    }
}