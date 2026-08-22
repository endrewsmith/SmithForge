using SmithForge.Features.ChaterManager;
using SmithForge.Features.ChatManager;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.ViewModels;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace SmithForge.Main.Views
{
    public partial class MainWindow : Window
    {
        // WinAPI для глобальной горячей клавиши
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        // Модификаторы клавиш
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        // ========== НИЗКОУРОВНЕВЫЙ ХУК ДЛЯ ЛЕВОГО CTRL ==========
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Левая клавиша Ctrl (код 0xA2)
                if (vkCode == 0xA2)
                {
                    Debug.WriteLine("[Hotkey] Нажат левый Ctrl");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var settings = ConfigService.Load();
                        if (settings.ImportantPlaybackMode == ImportantPlaybackMode.Manual)
                        {
                            var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
                            vm?.PlayNextImportantCommand.Execute(null);
                        }
                    });
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void StartKeyboardHook()
        {
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
            Debug.WriteLine("[Hotkey] Клавиатурный хук запущен (левый Ctrl)");
        }

        private void StopKeyboardHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                Debug.WriteLine("[Hotkey] Клавиатурный хук остановлен");
            }
        }
        // ========== КОНЕЦ ХУКА ==========

        public MainWindow()
        {
            InitializeComponent();
            var vm = new SmithForge.ViewModels.MainViewModel();
            DataContext = vm;
            WindowStateService.Bind(this, vm.Settings);

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterGlobalHotkey();
            StartKeyboardHook(); // Запускаем хук для левого Ctrl
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            UnregisterGlobalHotkey();
            StopKeyboardHook(); // Останавливаем хук
        }

        private void RegisterGlobalHotkey()
        {
            try
            {
                var settings = ConfigService.Load();
                var hotkey = settings.ImportantPlaybackHotkey;

                if (Enum.TryParse(hotkey, out Key key))
                {
                    uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                    uint modifiers = MOD_CONTROL | MOD_ALT | MOD_NOREPEAT;

                    var helper = new WindowInteropHelper(this);
                    if (RegisterHotKey(helper.Handle, HOTKEY_ID, modifiers, virtualKey))
                    {
                        Debug.WriteLine($"[Hotkey] Глобальная клавиша зарегистрирована: Ctrl+Alt+{hotkey}");
                    }
                    else
                    {
                        Debug.WriteLine($"[Hotkey] Не удалось зарегистрировать клавишу: Ctrl+Alt+{hotkey}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey] Ошибка регистрации: {ex.Message}");
            }
        }

        private void UnregisterGlobalHotkey()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                Debug.WriteLine("[Hotkey] Глобальная клавиша отменена");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Hotkey] Ошибка отмены: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            HwndSource.FromHwnd(helper.Handle)?.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                var settings = ConfigService.Load();

                if (settings.ImportantPlaybackMode == ImportantPlaybackMode.Manual)
                {
                    var vm = DataContext as MainViewModel;
                    vm?.PlayNextImportantCommand.Execute(null);
                    handled = true;
                    Debug.WriteLine("[Hotkey] Глобальная комбинация сработала!");
                }
            }
            return IntPtr.Zero;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ничего не делаем здесь - всё сохраняется в OnClosed
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                if (vm.IsProcessRunning)
                {
                    vm.StopCommand.Execute(null);
                }

                vm.Settings.WindowTop = this.Top;
                vm.Settings.WindowLeft = this.Left;
                vm.Settings.WindowHeight = this.Height;
                vm.Settings.WindowWidth = this.Width;
                vm.Settings.IsOverlaySetupMode = vm.IsOverlaySetupMode;

                vm.SaveOverlayPosition();
                vm.SaveShortsPosition();
                vm.SaveImportantPosition();
                vm.SaveStickersPosition();

                ConfigService.Save(vm.Settings);
            }
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void OpenChaters_Click(object sender, RoutedEventArgs e)
        {
            var win = new ChatersWindow();
            win.DataContext = new ChatersViewModel();
            win.Owner = this;
            win.ShowDialog();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(newText);
        }

        private void TextBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste)
                e.Handled = true;
        }

        private void OpenStreams_Click(object sender, RoutedEventArgs e)
        {
            var win = new SmithForge.Features.StreamsManager.StreamsWindow();
            win.DataContext = new SmithForge.Features.StreamsManager.StreamsViewModel();
            win.Owner = this;
            win.ShowDialog();
        }

        private void ToggleDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleDashboardCommand?.Execute(null);
            }
        }

        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void DecimalValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            string content = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            Regex regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(content);
        }

        private void ToggleShortsOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleShortsOverlayCommand.Execute(null);
            }
        }

        private void ToggleImportantOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleImportantOverlayCommand?.Execute(null);
            }
        }

        private void ToggleStickersOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleStickersOverlayCommand.Execute(null);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var settings = ConfigService.Load();

            if (settings.ImportantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                if (e.Key.ToString() == settings.ImportantPlaybackHotkey)
                {
                    (DataContext as MainViewModel)?.PlayNextImportantCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void OpenYouTubeManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем View и ViewModel
                var view = new SmithForge.Features.YouTubeManager.Views.YouTubeManagerView();
                var viewModel = new SmithForge.Features.YouTubeManager.ViewModels.YouTubeManagerViewModel();
                view.DataContext = viewModel;

                // Открываем в отдельном окне
                var window = new Window
                {
                    Title = "YouTube Manager - Добавление чата",
                    Content = view,
                    Width = 900,
                    Height = 700,
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия YouTube Manager: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenChatManager_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var window = new ChatManagerWindow();
            // Используем существующий _chatManager из MainViewModel
            window.DataContext = vm.GetChatManagerViewModel();  // ← нужно добавить метод
            window.Owner = this;
            window.ShowDialog();
        }
    }
}