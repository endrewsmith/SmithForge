using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SmithForge.Features.StreamsManager
{
    internal partial class StreamsViewModel : ObservableObject
    {
        private readonly List<StreamSession> _allSessions;

        [ObservableProperty]
        private ObservableCollection<StreamSession> _filteredSessions;

        [ObservableProperty]
        private StreamSession? _selectedSession;

        [ObservableProperty]
        private string _searchText = string.Empty;

        private string _currentChaterId = "system";

        public StreamsViewModel()
        {
            _allSessions = DatabaseService.GetAllSessions() ?? new List<StreamSession>();
            UpdateFilter();
        }

        partial void OnSearchTextChanged(string value) => UpdateFilter();

        private void UpdateFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredSessions = new ObservableCollection<StreamSession>(_allSessions);
            }
            else
            {
                var lowerSearch = SearchText.ToLower();
                var filtered = _allSessions.Where(s =>
                    (s.Title != null && s.Title.ToLower().Contains(lowerSearch)) ||
                    (s.Number.ToString().Contains(lowerSearch))
                ).ToList();

                FilteredSessions = new ObservableCollection<StreamSession>(filtered);
            }
        }

        [RelayCommand]
        private void OpenLogs()
        {
            if (SelectedSession == null) return;

            var win = new ChatLogWindow();
            win.DataContext = new ChatLogViewModel(SelectedSession.Id, _currentChaterId);
            win.Owner = System.Windows.Application.Current.Windows.OfType<StreamsWindow>().FirstOrDefault();
            win.ShowDialog();
        }

        // НОВАЯ КОМАНДА ДЛЯ УДАЛЕНИЯ
        [RelayCommand]
        private void DeleteSession(StreamSession session)
        {
            if (session == null) return;

            var result = MessageBox.Show(
                $"Удалить стрим #{session.Number} - {session.Title}?\n\n" +
                "Все сообщения этого стрима также будут удалены!",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Удаляем из БД
                    DatabaseService.DeleteSession(session.Id);

                    // Удаляем из списка
                    _allSessions.Remove(session);

                    // Обновляем отображение
                    UpdateFilter();

                    MessageBox.Show("Стрим успешно удален", "Удаление",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления стрима: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}