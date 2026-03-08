using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SmithForge.Main.Models;
using SmithForge.Features.ChatOverlay;

namespace SmithForge.Main.Services
{
    public class OverlayService
    {
        private ChatOverlayWindow? _overlayWindow;
        private ChatOverlayViewModel? _viewModel;
        private double _top;
        private double _left;

        public OverlayService()
        {
            // Создаем окно сразу в конструкторе (ОДИН РАЗ)
            CreateOverlay();
        }

        private void CreateOverlay()
        {
            if (_overlayWindow == null)
            {
                _viewModel = new ChatOverlayViewModel();
                _overlayWindow = new ChatOverlayWindow
                {
                    DataContext = _viewModel
                };

                _overlayWindow.LocationChanged += (s, e) =>
                {
                    if (_overlayWindow != null)
                    {
                        _top = _overlayWindow.Top;
                        _left = _overlayWindow.Left;
                    }
                };

                _overlayWindow.Show();
            }
        }

        public void Initialize(double top, double left)
        {
            _top = top;
            _left = left;

            if (_overlayWindow != null)
            {
                _overlayWindow.Top = top;
                _overlayWindow.Left = left;
            }
        }

        public void SetMode(bool isSetupMode)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.SetClickThrough(!isSetupMode);
            }
            if (_viewModel != null)
            {
                _viewModel.IsSetupMode = isSetupMode;
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            _viewModel?.AddMessage(user, msg);
        }

        public void SavePosition(AppSettings settings)
        {
            settings.OverlayTop = _top;
            settings.OverlayLeft = _left;
        }
    }
}