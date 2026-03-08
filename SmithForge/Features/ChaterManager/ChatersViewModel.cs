using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SmithForge.Features.ChaterManager
{
    public partial class ChatersViewModel : ObservableObject
    {
        // ========== КОЛЛЕКЦИИ ==========
        [ObservableProperty]
        private ObservableCollection<Chater> _chaters = new();

        [ObservableProperty]
        private ObservableCollection<Chater> _filteredChaters = new();

        [ObservableProperty]
        private Chater? _selectedChater;

        // Автоматическое обновление команд при изменении выбранного пользователя
        partial void OnSelectedChaterChanged(Chater? value)
        {
            Debug.WriteLine($"[VM] SelectedChater изменен: {(value == null ? "null" : value.EffectiveName)}");

            // Явно уведомляем команды об изменении
            SaveChaterCommand?.NotifyCanExecuteChanged();
            DeleteChaterCommand?.NotifyCanExecuteChanged();
            AddAccountCommand?.NotifyCanExecuteChanged();

            // Также обновляем глобально
            CommandManager.InvalidateRequerySuggested();
        }

        [ObservableProperty]
        private string _searchText = string.Empty;

        // ========== ДЛЯ ДОБАВЛЕНИЯ НОВЫХ АККАУНТОВ ==========
        [ObservableProperty]
        private ObservableCollection<string> _availablePlatforms = new() { "tw", "yt", "gg" };

        [ObservableProperty]
        private string _newAccountPlatform = "tw";

        [ObservableProperty]
        private string _newAccountLogin = string.Empty;

        // ========== КОМАНДЫ ==========
        public IRelayCommand CreateNewCommand { get; }
        public IRelayCommand AddAccountCommand { get; }
        public IRelayCommand<ExternalAccount> RemoveAccountCommand { get; }
        public IRelayCommand SaveChaterCommand { get; }
        public IRelayCommand DeleteChaterCommand { get; }

        // ========== КОНСТРУКТОР ==========
        public ChatersViewModel()
        {
            Debug.WriteLine("[VM] Конструктор ChatersViewModel");

            // Инициализация команд с CanExecute
            CreateNewCommand = new RelayCommand(CreateNew);
            AddAccountCommand = new RelayCommand(AddAccount, CanAddAccount);
            RemoveAccountCommand = new RelayCommand<ExternalAccount>(RemoveAccount);
            SaveChaterCommand = new RelayCommand(Save, () => SelectedChater != null);
            DeleteChaterCommand = new RelayCommand(Delete, () => SelectedChater != null);

            // Загрузка данных из БД
            LoadAllFromDatabase();

            Debug.WriteLine("[VM] Конструктор завершен");
        }

        // ========== МЕТОДЫ ПРОВЕРКИ ДЛЯ КОМАНД ==========
        private bool CanAddAccount()
        {
            return SelectedChater != null && !string.IsNullOrWhiteSpace(NewAccountLogin);
        }

        // ========== ЗАГРУЗКА ИЗ БД ==========
        private void LoadAllFromDatabase()
        {
            try
            {
                Debug.WriteLine("[VM] Загрузка данных из БД...");
                var allChaters = DatabaseService.LoadAll();
                Chaters = new ObservableCollection<Chater>(allChaters);
                FilterChaters();
                Debug.WriteLine($"[VM] Загружено {Chaters.Count} чаттеров");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VM] Ошибка загрузки данных: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== ПОИСК ==========
        partial void OnSearchTextChanged(string value)
        {
            FilterChaters();
        }

        private void FilterChaters()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredChaters = new ObservableCollection<Chater>(Chaters);
                return;
            }

            var filtered = Chaters.Where(c =>
                (c.DisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (c.EffectiveName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                c.Accounts.Any(a => a.OriginalName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                c.Accounts.Any(a => a.Platform?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

            FilteredChaters = new ObservableCollection<Chater>(filtered);
        }

        // ========== СОЗДАНИЕ НОВОГО ЧАТТЕРА ==========
        private void CreateNew()
        {
            Debug.WriteLine("[VM] Создание нового чаттера");

            var newChater = new Chater
            {
                Id = Guid.NewGuid().ToString(),
                Login = $"user_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                DisplayName = "Новый зритель",
                MessageCount = 0,
                FirstSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastMessageTime = 0,
                TotalKarma = 0,
                Karma = 0,
                Rank = 0,
                AvatarFileName = "default.png"
            };

            Chaters.Add(newChater);
            SelectedChater = newChater;
            FilterChaters();

            Debug.WriteLine($"[VM] Новый чаттер создан с ID: {newChater.Id}");
        }

        // ========== ДОБАВЛЕНИЕ АККАУНТА ==========
        private void AddAccount()
        {
            Debug.WriteLine("[VM] Добавление нового аккаунта");

            if (!CanAddAccount())
            {
                if (SelectedChater == null)
                    MessageBox.Show("Сначала выберите зрителя", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                else if (string.IsNullOrWhiteSpace(NewAccountLogin))
                    MessageBox.Show("Введите логин", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, нет ли уже такого аккаунта
            if (SelectedChater.Accounts.Any(a =>
                a.Platform == NewAccountPlatform &&
                a.OriginalName.Equals(NewAccountLogin, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Такой аккаунт уже привязан", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Создаем новый внешний аккаунт
            var newAccount = new ExternalAccount
            {
                ExternalId = $"{NewAccountPlatform}:{NewAccountLogin}".ToLower(),
                Platform = NewAccountPlatform,
                OriginalName = NewAccountLogin
            };

            // Добавляем в коллекцию
            SelectedChater.Accounts.Add(newAccount);

            // Если это первый аккаунт и DisplayName пустой, устанавливаем его из логина
            if (SelectedChater.Accounts.Count == 1 && string.IsNullOrEmpty(SelectedChater.DisplayName))
            {
                SelectedChater.DisplayName = NewAccountLogin;
            }

            // ОЧИЩАЕМ ПОЛЯ ввода
            NewAccountLogin = string.Empty;
            NewAccountPlatform = "tw";

            // Обновляем отображение
            OnPropertyChanged(nameof(SelectedChater));
            FilterChaters();

            // Обновляем состояние команды AddAccount
            AddAccountCommand.NotifyCanExecuteChanged();

            Debug.WriteLine("[VM] Аккаунт добавлен");
        }

        partial void OnNewAccountLoginChanged(string value)
        {
            AddAccountCommand?.NotifyCanExecuteChanged();
        }

        // ========== УДАЛЕНИЕ АККАУНТА ==========
        private void RemoveAccount(ExternalAccount? account)
        {
            if (SelectedChater == null || account == null) return;

            Debug.WriteLine($"[VM] Удаление аккаунта: {account.ExternalId}");

            var result = MessageBox.Show($"Отвязать аккаунт {account.Platform}:{account.OriginalName}?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Удаляем из БД запись ExternalAccount
                    DatabaseService.DeleteExternalAccount(account.ExternalId);
                    Debug.WriteLine($"[VM] Аккаунт {account.ExternalId} удален из БД");

                    // 2. Удаляем из коллекции в памяти
                    SelectedChater.Accounts.Remove(account);

                    // 3. Если не осталось аккаунтов, очищаем отображаемое имя
                    if (SelectedChater.Accounts.Count == 0)
                    {
                        SelectedChater.DisplayName = string.Empty;
                    }

                    // 4. Обновляем чаттера в БД (на случай изменения DisplayName)
                    DatabaseService.SaveChater(SelectedChater);

                    // 5. Обновляем кэш
                    ChaterStorage.AddOrUpdate(SelectedChater);

                    Debug.WriteLine($"[VM] Аккаунт {account.ExternalId} успешно удален");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VM] Ошибка при удалении аккаунта: {ex.Message}");
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Обновляем отображение
                OnPropertyChanged(nameof(SelectedChater));
                FilterChaters();
            }
        }

        // ========== СОХРАНЕНИЕ ИЗМЕНЕНИЙ ==========
        private void Save()
        {
            Debug.WriteLine("========== СОХРАНЕНИЕ ==========");

            if (SelectedChater == null)
            {
                Debug.WriteLine("SelectedChater is null - выход");
                return;
            }

            Debug.WriteLine($"ID: {SelectedChater.Id}");
            Debug.WriteLine($"DisplayName: '{SelectedChater.DisplayName}'");
            Debug.WriteLine($"EffectiveName: '{SelectedChater.EffectiveName}'");
            Debug.WriteLine($"Accounts count: {SelectedChater.Accounts.Count}");

            try
            {
                Debug.WriteLine("Вызов DatabaseService.SaveChater...");
                DatabaseService.SaveChater(SelectedChater);
                Debug.WriteLine("DatabaseService.SaveChater выполнен");

                Debug.WriteLine("Вызов ChaterStorage.AddOrUpdate...");
                ChaterStorage.AddOrUpdate(SelectedChater);
                Debug.WriteLine("ChaterStorage.AddOrUpdate выполнен");

                SelectedChater.IsRecentlySaved = true;
                Debug.WriteLine("IsRecentlySaved = true");

                var timer = new System.Timers.Timer(2000);
                timer.Elapsed += (s, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (SelectedChater != null)
                        {
                            SelectedChater.IsRecentlySaved = false;
                            OnPropertyChanged(nameof(SelectedChater));
                            Debug.WriteLine("IsRecentlySaved = false (таймер)");
                        }
                    });
                    timer.Dispose();
                };
                timer.Start();
                timer.AutoReset = false;

                FilterChaters();
                Debug.WriteLine("Сохранение завершено успешно");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ОШИБКА: {ex.Message}");
                Debug.WriteLine($"Stack: {ex.StackTrace}");
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Debug.WriteLine("========== КОНЕЦ СОХРАНЕНИЯ ==========");
        }

        // ========== УДАЛЕНИЕ ЧАТТЕРА ==========
        private void Delete()
        {
            Debug.WriteLine("========== DELETE METHOD CALLED ==========");
            Debug.WriteLine($"SelectedChater == null: {SelectedChater == null}");

            if (SelectedChater == null)
            {
                Debug.WriteLine("Delete: SelectedChater is null, exiting");
                return;
            }

            Debug.WriteLine($"Delete: Удаляем чаттера: {SelectedChater.EffectiveName} (ID: {SelectedChater.Id})");

            var result = MessageBox.Show($"Удалить профиль {SelectedChater.EffectiveName}?\nЭто действие нельзя отменить!",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            Debug.WriteLine($"Delete: MessageBox result = {result}");

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Debug.WriteLine("Delete: Начинаем удаление...");

                    // 1. Сначала удаляем из БД (аккаунты удалятся автоматически благодаря CASCADE)
                    DatabaseService.DeleteChater(SelectedChater.Id);
                    Debug.WriteLine($"Delete: Чаттер {SelectedChater.Id} удален из БД");

                    // 2. Удаляем аккаунты из кэша
                    foreach (var account in SelectedChater.Accounts.ToList())
                    {
                        Debug.WriteLine($"Delete: Удаляем аккаунт {account.ExternalId} из кэша");
                        ChaterStorage.Remove(account.ExternalId);
                    }

                    // 3. Удаляем из коллекции в памяти
                    Debug.WriteLine("Delete: Удаляем из коллекции Chaters");
                    Chaters.Remove(SelectedChater);

                    // 4. Очищаем выделение
                    Debug.WriteLine("Delete: Очищаем SelectedChater");
                    SelectedChater = null;

                    // 5. Обновляем фильтр
                    Debug.WriteLine("Delete: Обновляем фильтр");
                    FilterChaters();

                    Debug.WriteLine("Delete: Удаление завершено успешно");

                    MessageBox.Show("Профиль успешно удален", "Удаление",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Delete: ОШИБКА - {ex.Message}");
                    Debug.WriteLine($"Delete: StackTrace - {ex.StackTrace}");
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Debug.WriteLine("Delete: Удаление отменено пользователем");
            }

            Debug.WriteLine("========== DELETE METHOD END ==========");
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
        public void RefreshList()
        {
            Debug.WriteLine("[VM] Обновление списка");
            LoadAllFromDatabase();
        }
    }
}