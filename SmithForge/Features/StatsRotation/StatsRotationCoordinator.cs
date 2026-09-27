using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace SmithForge.Features.StatsRotation
{
    public sealed partial class StatsRotationCoordinator : ObservableObject
    {
        private readonly StatsRotationService? _service;
        private readonly AppSettings _settings;
        private readonly DispatcherTimer _countdownTimer;

        // ============================================================
        // СВОЙСТВА ДЛЯ UI
        // ============================================================
        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private int _silenceInterval;

        [ObservableProperty]
        private string _status = "⏹ Остановлена";

        [ObservableProperty]
        private int _totalRules;

        [ObservableProperty]
        private string _lastShownRule = "";

        /// <summary>Секунд до следующего показа.</summary>
        [ObservableProperty]
        private int _secondsUntilNext;

        /// <summary>Строка таймера вида "00:24".</summary>
        [ObservableProperty]
        private string _countdownText = "--:--";

        /// <summary>Прогресс 0..1 (сколько прошло от интервала).</summary>
        [ObservableProperty]
        private double _countdownProgress;

        /// <summary>Чек-боксы правил для окна настроек алертов.</summary>
        public ObservableCollection<StatsRuleToggle> Rules { get; } = new();

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public StatsRotationCoordinator(WebServerService? webServer, AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _isEnabled = settings.StatsRotationEnabled;
            _silenceInterval = settings.StatsSilenceIntervalSeconds;
            _totalRules = StatsRuleRegistry.All.Count;

            // Заполняем список правил из реестра
            foreach (var rule in StatsRuleRegistry.All)
            {
                Rules.Add(new StatsRuleToggle
                {
                    Key = rule.Key,
                    Title = rule.Title,
                    IsEnabled = settings.EnabledStatsRules.Contains(rule.Key)
                });
            }

            // Если веб-сервер есть — создаём сервис. Если нет — координатор живёт без него.
            if (webServer != null)
            {
                _service = new StatsRotationService(webServer, settings);
                _service.RuleShown += (s, key) =>
                {
                    LastShownRule = key;
                    UpdateStatus();
                };

                if (_isEnabled)
                {
                    _service.Start(_silenceInterval);
                }
            }

            // ✅ Тик каждую секунду для обратного отсчёта
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += (s, e) => Tick();
            _countdownTimer.Start();

            UpdateStatus();
            Tick();
        }

        // ============================================================
        // ТИК РАЗ В СЕКУНДУ — пересчёт обратного отсчёта
        // ============================================================
        private void Tick()
        {
            if (_service == null)
            {
                CountdownText = "--:--";
                CountdownProgress = 0;
                return;
            }

            var silent = _service.SecondsSinceLastActivity;
            var target = _silenceInterval;
            var remaining = Math.Max(0, target - (int)silent);

            SecondsUntilNext = remaining;

            int mm = remaining / 60;
            int ss = remaining % 60;
            CountdownText = $"{mm:D2}:{ss:D2}";

            // Прогресс 0..1 (сколько прошло от интервала)
            CountdownProgress = target > 0
                ? Math.Min(1.0, silent / target)
                : 0;
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ СО СЛУЖЕБНЫМ СЛОЕМ
        // ============================================================
        partial void OnIsEnabledChanged(bool value)
        {
            _settings.StatsRotationEnabled = value;
            ConfigService.Save(_settings);

            if (value) _service?.Start(_silenceInterval);
            else _service?.Stop();

            UpdateStatus();
            Tick();
        }

        partial void OnSilenceIntervalChanged(int value)
        {
            _settings.StatsSilenceIntervalSeconds = value;
            ConfigService.Save(_settings);
            _service?.SetSilenceInterval(value);
            UpdateStatus();
            Tick();
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void Start()
        {
            IsEnabled = true;
        }

        [RelayCommand]
        private void Stop()
        {
            IsEnabled = false;
        }

        [RelayCommand]
        private void NotifyActivity()
        {
            _service?.OnUserActivity();
            UpdateStatus();
            Tick();
        }

        [RelayCommand]
        private void ToggleRule(StatsRuleToggle? toggle)
        {
            if (toggle == null) return;

            if (toggle.IsEnabled)
            {
                if (!_settings.EnabledStatsRules.Contains(toggle.Key))
                    _settings.EnabledStatsRules.Add(toggle.Key);
            }
            else
            {
                _settings.EnabledStatsRules.Remove(toggle.Key);
            }

            ConfigService.Save(_settings);
            Debug.WriteLine($"[StatsRotation] Правило '{toggle.Key}': {(toggle.IsEnabled ? "вкл" : "выкл")}");
        }

        // ============================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ (для MainViewModel)
        // ============================================================
        public void NotifyUserActivity() => _service?.OnUserActivity();

        public void UpdateStatus()
        {
            Status = !_isEnabled
                ? "⏹ Остановлена"
                : $"🎬 Тишина {_silenceInterval}с / Правил: {Rules.Count(r => r.IsEnabled)}";
        }
    }

    // ================================================================
    // Вспомогательный класс — чек-бокс правила
    // ================================================================
    public sealed class StatsRuleToggle : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }
}