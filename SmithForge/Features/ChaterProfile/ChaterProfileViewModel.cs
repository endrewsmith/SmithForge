using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;

namespace SmithForge.Features.ChaterProfile
{
    public partial class ChaterProfileViewModel : ObservableObject
    {
        [ObservableProperty]
        private Chater _chater;

        [ObservableProperty]
        private bool _isAdmin = false;

        [ObservableProperty]
        private string _avatarPath;

        public ICommand SaveCommand { get; }
        public ICommand ChangeAvatarCommand { get; }

        public ChaterProfileViewModel(Chater chater)
        {
            _chater = chater;
            SaveCommand = new RelayCommand(SaveChanges);
            ChangeAvatarCommand = new RelayCommand(ChangeAvatar);

            // Загружаем путь к аватару
            UpdateAvatarPath();
        }

        private void UpdateAvatarPath()
        {
            try
            {
                if (Chater == null)
                {
                    AvatarPath = null;
                    return;
                }

                // Используем FullAvatarPath из Chater
                string fullPath = Chater.FullAvatarPath;
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    AvatarPath = fullPath;
                }
                else
                {
                    AvatarPath = null;
                }

                System.Diagnostics.Debug.WriteLine($"[Profile] Аватар загружен: {AvatarPath}");
                OnPropertyChanged(nameof(AvatarPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Profile] Ошибка загрузки аватара: {ex.Message}");
                AvatarPath = null;
            }
        }

        private void SaveChanges()
        {
            try
            {
                // Обновляем аватар после изменений
                Chater.RefreshAvatar();

                // Сохраняем изменения в хранилище и базу данных
                ChaterStorage.AddOrUpdate(Chater);
                DatabaseService.SaveChater(Chater);

                // Показываем индикатор сохранения
                Chater.IsRecentlySaved = true;
                
                // Сбрасываем флаг через 2 секунды
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Chater.IsRecentlySaved = false;
                });

                System.Diagnostics.Debug.WriteLine($"[Profile] Сохранены изменения для {Chater.DisplayName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Profile] Ошибка сохранения: {ex.Message}");
            }
        }

        private void ChangeAvatar()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите аватар",
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Все файлы (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Путь для сохранения аватара (custom папка, файл с именем Id.png)
                    string avatarFolder = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "SF_Data", "Assets", "Avatars", "custom");

                    if (!Directory.Exists(avatarFolder))
                        Directory.CreateDirectory(avatarFolder);

                    // Сохраняем файл с именем ID пользователя (как в методе GetAvatarPath)
                    string newAvatarPath = Path.Combine(avatarFolder, $"{Chater.Id}.png");

                    // Конвертируем в PNG если нужно (опционально)
                    // Просто копируем выбранный файл
                    File.Copy(openFileDialog.FileName, newAvatarPath, true);

                    // Обновляем путь в ViewModel
                    AvatarPath = newAvatarPath;

                    // Обновляем аватар в модели Chater (вызываем RefreshAvatar)
                    Chater.RefreshAvatar();

                    System.Diagnostics.Debug.WriteLine($"[Profile] Аватар обновлен для {Chater.DisplayName}: {newAvatarPath}");
                    System.Diagnostics.Debug.WriteLine($"[Profile] Debug info: {Chater.GetAvatarDebugInfo()}");

                    // Обновляем привязку
                    OnPropertyChanged(nameof(AvatarPath));
                    OnPropertyChanged(nameof(Chater));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Profile] Ошибка при сохранении аватара: {ex.Message}");
                }
            }
        }
    }
}