using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;

namespace SmithForge.Features.StreamsManager
{
    internal partial class ChatLogViewModel : ObservableObject
    {
        private List<ChatLogMessage> _allLogs = new();

        [ObservableProperty]
        private ObservableCollection<ChatLogMessage> _filteredLogs;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _currentChaterId; // ID текущего пользователя

        [ObservableProperty]
        private string _sessionId;

        // Команды
        public ICommand LikeCommand { get; }
        public ICommand DislikeCommand { get; }
        public ICommand RefreshCommand { get; }

        // Конструктор
        public ChatLogViewModel(string sessionId, string currentChaterId)
        {
            _sessionId = sessionId;
            _currentChaterId = currentChaterId;

            // Инициализация команд
            LikeCommand = new RelayCommand<ChatLogMessage>(Like);
            DislikeCommand = new RelayCommand<ChatLogMessage>(Dislike);
            RefreshCommand = new RelayCommand(Refresh);

            // Загрузка сообщений
            LoadMessages();
        }

        private void LoadMessages()
        {
            try
            {
                // Загружаем данные из БД с реакциями пользователя
                _allLogs = DatabaseService.GetChatLogsWithReactions(_sessionId, _currentChaterId) ?? new List<ChatLogMessage>();
                UpdateFilter();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сообщений: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnSearchTextChanged(string value) => UpdateFilter();

        private void UpdateFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredLogs = new ObservableCollection<ChatLogMessage>(_allLogs);
            }
            else
            {
                var lowerSearch = SearchText.ToLower();
                var filtered = _allLogs.Where(log =>
                    (log.Message != null && log.Message.ToLower().Contains(lowerSearch))
                ).ToList();

                FilteredLogs = new ObservableCollection<ChatLogMessage>(filtered);
            }
        }

        private void Like(ChatLogMessage? message)
        {
            if (message == null) return;

            try
            {
                DatabaseService.LikeMessage(message.Id, _currentChaterId);
                LoadMessages(); // Перезагружаем обновленные данные
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при лайке: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Dislike(ChatLogMessage? message)
        {
            if (message == null) return;

            try
            {
                DatabaseService.DislikeMessage(message.Id, _currentChaterId);
                LoadMessages(); // Перезагружаем обновленные данные
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при дизлайке: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh()
        {
            LoadMessages();
        }
    }
}