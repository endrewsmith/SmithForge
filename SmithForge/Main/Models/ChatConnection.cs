using CommunityToolkit.Mvvm.Input; // Добавить using
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SmithForge.Main.Models
{
    public enum ChatMode
    {
        Normal,
        Shorts
    }

    public enum YouTubeConnectionMethod
    {
        Auto,           // Сначала API, потом парсинг
        ApiOnly,        // Только API (тратит квоту)
        HtmlOnly,       // Только парсинг HTML (бесплатно)
        ManualVideoId   // Ручной Video ID
    }

    public class ChatConnection : INotifyPropertyChanged
    {
        private string _chatName = string.Empty;
        private string _status = "Не подключен";
        private string _lastConnectionError = string.Empty;
        private int _messageCount;
        private bool _isConnected;

        public string VideoIdDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(VideoId))
                    return string.Empty;
                return $"📺 {VideoId}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ✅ Команда для подключения/отключения
        public ICommand ConnectCommand { get; }

        // ✅ Событие для уведомления внешнего кода о необходимости подключения/отключения
        public event EventHandler? ConnectRequested;
        public event EventHandler? DisconnectRequested;

        public ChatConnection()
        {
            ConnectCommand = new RelayCommand(OnConnect);
        }


        private void OnConnect()
        {
            if (IsConnected)
            {
                DisconnectRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ConnectRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public string ChatName
        {
            get => _chatName;
            set
            {
                _chatName = value;
                OnPropertyChanged();
                RefreshDisplayName();
            }
        }

        public string Platform { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        private string _videoId = string.Empty;

        public string VideoId
        {
            get => _videoId;
            set
            {
                if (_videoId != value)
                {
                    _videoId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VideoIdDisplay));
                }
            }
        }
        public string ApiKey { get; set; } = string.Empty;

        private string? _displayNameOverride;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_displayNameOverride))
                    return _displayNameOverride;

                string baseName;
                if (!string.IsNullOrEmpty(_chatName))
                {
                    baseName = _chatName;
                }
                else if (!string.IsNullOrEmpty(ChannelId))
                {
                    baseName = $"YouTube: {ChannelId}";
                }
                else
                {
                    baseName = "YouTube чат";
                }

                if (Mode == ChatMode.Shorts)
                {
                    baseName += " 📱";
                }

                string methodIndicator = PreferredMethod switch
                {
                    YouTubeConnectionMethod.ApiOnly => "🔑",
                    YouTubeConnectionMethod.HtmlOnly => "🌐",
                    _ => ""
                };

                return $"{methodIndicator} {baseName}";
            }
            set
            {
                _displayNameOverride = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShortStatus));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                Debug.WriteLine($"[ChatConnection] IsConnected СТАРОЕ: {_isConnected}, НОВОЕ: {value}, Поток: {Thread.CurrentThread.ManagedThreadId}");

                if (_isConnected != value)
                {
                    _isConnected = value;

                    // Вызываем в UI потоке
                    Application.Current.Dispatcher.Invoke(() => {
                        Debug.WriteLine($"[ChatConnection] UI поток: обновляем свойства");
                        OnPropertyChanged(nameof(IsConnected));
                        OnPropertyChanged(nameof(ShortStatus));
                        OnPropertyChanged(nameof(StatusColor));
                        OnPropertyChanged(nameof(StatusIcon));

                        // Принудительно обновляем команду
                        if (ConnectCommand is RelayCommand relayCmd)
                        {
                            Debug.WriteLine("[ChatConnection] Вызываем NotifyCanExecuteChanged");
                            relayCmd.NotifyCanExecuteChanged();
                        }
                        else
                        {
                            Debug.WriteLine($"[ChatConnection] ConnectCommand НЕ является RelayCommand, тип: {ConnectCommand?.GetType()}");
                        }
                    });
                }
            }
        }

        public string LastConnectionError
        {
            get => _lastConnectionError;
            set
            {
                _lastConnectionError = value;
                OnPropertyChanged();
            }
        }

        public int MessageCount
        {
            get => _messageCount;
            set
            {
                _messageCount = value;
                OnPropertyChanged();
            }
        }

        private ChatMode _mode = ChatMode.Normal;
        private string _modeDisplay = "📺 Normal";

        public ChatMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    _modeDisplay = value == ChatMode.Shorts ? "📱 Shorts" : "📺 Normal";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ModeDisplay));
                    RefreshDisplayName();
                }
            }
        }

        public string ModeDisplay => _modeDisplay;

        private YouTubeConnectionMethod _preferredMethod = YouTubeConnectionMethod.Auto;
        private string _methodDisplay = "";

        public YouTubeConnectionMethod PreferredMethod
        {
            get => _preferredMethod;
            set
            {
                if (_preferredMethod != value)
                {
                    _preferredMethod = value;
                    _methodDisplay = value switch
                    {
                        YouTubeConnectionMethod.ApiOnly => "🔑 API Only",
                        YouTubeConnectionMethod.HtmlOnly => "🌐 Парсинг HTML",
                        YouTubeConnectionMethod.ManualVideoId => "📺 Video ID",
                        _ => ""
                    };
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MethodDisplay));
                    RefreshDisplayName();
                }
            }
        }

        public string MethodDisplay => _methodDisplay;

        public YouTubeConnectionMethod LastUsedMethod { get; set; } = YouTubeConnectionMethod.Auto;

        public string PlatformIcon => Platform switch
        {
            "youtube" => "🎥",
            "twitch" => "🟣",
            "goodgame" => "🟢",
            _ => "❓"
        };

        public bool IsYouTube => Platform?.ToLower() == "youtube";

        public string StatusColor => Status switch
        {
            string s when s.Contains("Подключен") || s.Contains("✅") => "#4CAF50",
            string s when s.Contains("Ошибка") || s.Contains("❌") => "#F44336",
            string s when s.Contains("Подключение") || s.Contains("🔄") => "#FFC107",
            string s when s.Contains("Отключен") || s.Contains("⏹") => "#9E9E9E",
            _ => "#9E9E9E"
        };

        public string StatusIcon => Status switch
        {
            string s when s.Contains("Подключен") || s.Contains("✅") => "🟢",
            string s when s.Contains("Ошибка") || s.Contains("❌") => "🔴",
            string s when s.Contains("Подключение") || s.Contains("🔄") => "🟡",
            string s when s.Contains("Отключен") || s.Contains("⏹") => "⚪",
            _ => "⚪"
        };

        public string ShortStatus => Status switch
        {
            string s when s.Contains("Подключен") || s.Contains("✅") => "Подключен",
            string s when s.Contains("Ошибка") || s.Contains("❌") => "Ошибка",
            string s when s.Contains("Подключение") || s.Contains("🔄") => "Подключение...",
            string s when s.Contains("Отключен") || s.Contains("⏹") => "Отключен",
            _ => "Не подключен"
        };

        public void RefreshDisplayName()
        {
            _displayNameOverride = null;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(VideoIdDisplay)); // ✅ Добавить
        }
    }
}