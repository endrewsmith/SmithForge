using SmithForge.ChatEngine.Platforms.Twitch;
using SmithForge.ChatEngine.Platforms.YouTube;
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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
                        if (vm?.Alerts == null) return;

                        bool isManualMode = vm.Alerts.ImportantPlaybackMode == ImportantPlaybackMode.Manual;
                        int queueSize = vm.Alerts.QueueSize;

                        if (!isManualMode || queueSize <= 0)
                        {
                            Debug.WriteLine($"[Hotkey] Ctrl проигнорирован " +
                                            $"(Manual={isManualMode}, queue={queueSize})");
                            return;
                        }

                        Debug.WriteLine($"[Hotkey] Нажат левый Ctrl — воспроизводим следующее (queue={queueSize})");
                        vm.Alerts.PlayNextImportantCommand?.Execute(null);
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

            // ✅ YouTube
            YoutubeChatClient.RegisterDelegates(
                checkExists: (code) => EmojiService.EmojiExists(code),
                register: (code, path) => EmojiService.AddEmojiToCache(code, path)
            );

            // ✅ Twitch
            TwitchConnector.RegisterDelegates(
                checkExists: (code) => EmojiService.EmojiExists(code),
                register: (code, path) => EmojiService.AddEmojiToCache(code, path, "Twitch")
            );

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
                var vm = DataContext as MainViewModel;
                if (vm?.Alerts == null) return IntPtr.Zero;

                if (vm.Alerts.ImportantPlaybackMode != ImportantPlaybackMode.Manual)
                    return IntPtr.Zero;

                if (vm.Alerts.QueueSize <= 0)
                    return IntPtr.Zero;

                vm.Alerts.PlayNextImportantCommand?.Execute(null);
                handled = true;
                Debug.WriteLine($"[Hotkey Ctrl+Alt+F8] Воспроизводим (queue={vm.Alerts.QueueSize})");
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
            Debug.WriteLine("[MainWindow] Начинаем закрытие...");

            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                // 1. Останавливаем все чаты
                if (vm.IsProcessRunning)
                {
                    vm.StopCommand.Execute(null);
                }

                // 2. ✅ КОРРЕКТНОЕ ЗАКРЫТИЕ ВЕБ-СЕРВЕРА (ДО сохранения настроек)
                vm.ShutdownWebServer();

                // 3. Сохраняем настройки
                vm.Settings.WindowTop = this.Top;
                vm.Settings.WindowLeft = this.Left;
                vm.Settings.WindowHeight = this.Height;
                vm.Settings.WindowWidth = this.Width;
                vm.Settings.IsOverlaySetupMode = vm.Overlays.IsOverlaySetupMode;

                vm.SaveOverlayPosition();
                vm.SaveShortsPosition();
                vm.SaveImportantPosition();
                vm.SaveStickersPosition();
                vm.SaveAlertsPosition();

                ConfigService.Save(vm.Settings);
            }

            // 4. Отменяем глобальные хуки
            UnregisterGlobalHotkey();
            StopKeyboardHook();

            Debug.WriteLine("[MainWindow] Закрытие завершено");
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
                vm.Overlays?.ToggleDashboardCommand?.Execute(null);
            }
        }

        private void ToggleMediaDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.Overlays?.ToggleMediaDashboardCommand?.Execute(null);
            }
        }

        private void ToggleTechOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.Overlays?.ToggleTechOverlayCommand?.Execute(null);
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
                vm.Overlays?.ToggleShortsOverlayCommand?.Execute(null);
            }
        }

        private void ToggleImportantOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.Overlays?.ToggleImportantOverlayCommand?.Execute(null);
            }
        }

        private void ToggleStickersOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.Overlays?.ToggleStickersOverlayCommand?.Execute(null);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var vm = DataContext as MainViewModel;
            if (vm?.Alerts == null) return;

            // Читаем hotkey из уже загруженного объекта, не с диска
            string hotkey = vm.Settings.ImportantPlaybackHotkey;

            // Мгновенный выход, если нажата не наша горячая клавиша
            if (e.Key.ToString() != hotkey)
                return;

            bool isManualMode = vm.Alerts.ImportantPlaybackMode == ImportantPlaybackMode.Manual;
            int queueSize = vm.Alerts.QueueSize;

            if (!isManualMode || queueSize <= 0)
            {
                Debug.WriteLine($"[Hotkey F8] Проигнорирован (Manual={isManualMode}, queue={queueSize})");
                return;
            }

            Debug.WriteLine($"[Hotkey F8] Воспроизводим (queue={queueSize})");
            vm.Alerts.PlayNextImportantCommand?.Execute(null);
            e.Handled = true;
        }

        private void OpenChatManager_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var window = new ChatManagerWindow();
            // Используем ChatCoordinator
            window.DataContext = vm.ChatsManager.GetChatManagerViewModel();
            window.Owner = this;
            window.ShowDialog();
        }

        // ============================================================
        // 🔔 ОБРАБОТЧИКИ АЛЕРТОВ
        // ============================================================

        /// <summary>
        /// Открыть окно настроек алертов
        /// </summary>
        private async void OpenAlertsSettings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                if (vm.Alerts?.OpenAlertsSettingsCommand != null)
                {
                    await vm.Alerts.OpenAlertsSettingsCommand.ExecuteAsync(null);
                }
            }
        }

        /// <summary>
        /// Вкл/выкл оверлей алертов
        /// </summary>
        private void ToggleAlertsOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.Alerts?.ToggleAlertsOverlayCommand?.Execute(null);
            }
        }

        /// <summary>
        /// Открыть веб-оверлей алертов в браузере
        /// </summary>
        private void OpenAlertsWebOverlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "http://localhost:10881/alerts";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Открыт веб-оверлей алертов: {url}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть браузер: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}