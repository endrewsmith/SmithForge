using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Diagnostics;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // МЕНЕДЖЕР СЕССИЙ
        // ============================================================
        private StreamSessionManager _streamSessionManager = null!;

        [ObservableProperty]
        private StreamSession? _currentSession;

        [ObservableProperty]
        private int _lastStreamNumber;

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void NextStream()
        {
            _streamSessionManager.NextStream(CurrentSession?.Title ?? "Без названия", (number, title) =>
            {
                LastStreamNumber = number;
                Settings.LastStreamNumber = number;
                ConfigService.Save(Settings);
            });
        }
    }
}