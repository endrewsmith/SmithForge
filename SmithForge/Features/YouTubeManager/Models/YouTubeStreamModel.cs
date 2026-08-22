using System.ComponentModel;

namespace SmithForge.Features.YouTubeManager.Models;

/// <summary>
/// Модель для отображения информации о YouTube стриме в UI
/// </summary>
public class YouTubeStreamModel : INotifyPropertyChanged
{
    private string _videoId = string.Empty;
    private string _title = string.Empty;
    private bool _isShorts;
    private string _channelName = string.Empty;
    private bool _isSelected;
    private bool _isLive;
    private int _viewerCount;
    private string _thumbnailUrl = string.Empty;

    /// <summary>
    /// ID видео (11 символов)
    /// </summary>
    public string VideoId
    {
        get => _videoId;
        set
        {
            if (_videoId != value)
            {
                _videoId = value;
                OnPropertyChanged(nameof(VideoId));
            }
        }
    }

    /// <summary>
    /// Название стрима
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    /// <summary>
    /// Является ли стрим Shorts (вертикальный формат)
    /// </summary>
    public bool IsShorts
    {
        get => _isShorts;
        set
        {
            if (_isShorts != value)
            {
                _isShorts = value;
                OnPropertyChanged(nameof(IsShorts));
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(StreamTypeIcon));
                OnPropertyChanged(nameof(StreamTypeText));
            }
        }
    }

    /// <summary>
    /// Название канала
    /// </summary>
    public string ChannelName
    {
        get => _channelName;
        set
        {
            if (_channelName != value)
            {
                _channelName = value;
                OnPropertyChanged(nameof(ChannelName));
            }
        }
    }

    /// <summary>
    /// Выбран ли стрим для подключения
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary>
    /// Активен ли стрим в данный момент
    /// </summary>
    public bool IsLive
    {
        get => _isLive;
        set
        {
            if (_isLive != value)
            {
                _isLive = value;
                OnPropertyChanged(nameof(IsLive));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    /// <summary>
    /// Количество зрителей
    /// </summary>
    public int ViewerCount
    {
        get => _viewerCount;
        set
        {
            if (_viewerCount != value)
            {
                _viewerCount = value;
                OnPropertyChanged(nameof(ViewerCount));
                OnPropertyChanged(nameof(ViewerCountText));
            }
        }
    }

    /// <summary>
    /// URL миниатюры
    /// </summary>
    public string ThumbnailUrl
    {
        get => _thumbnailUrl;
        set
        {
            if (_thumbnailUrl != value)
            {
                _thumbnailUrl = value;
                OnPropertyChanged(nameof(ThumbnailUrl));
            }
        }
    }

    /// <summary>
    /// Отображаемый текст (название + иконка типа)
    /// </summary>
    public string DisplayText => $"{Title} {(IsShorts ? "🎬 [SHORTS]" : "📺")}";

    /// <summary>
    /// Иконка типа стрима
    /// </summary>
    public string StreamTypeIcon => IsShorts ? "🎬" : "📺";

    /// <summary>
    /// Текст типа стрима
    /// </summary>
    public string StreamTypeText => IsShorts ? "Shorts" : "Обычный";

    /// <summary>
    /// Текст статуса
    /// </summary>
    public string StatusText => IsLive ? "🟢 LIVE" : "⚪ Офлайн";

    /// <summary>
    /// Цвет статуса
    /// </summary>
    public string StatusColor => IsLive ? "#4CAF50" : "#9E9E9E";

    /// <summary>
    /// Текст с количеством зрителей
    /// </summary>
    public string ViewerCountText => ViewerCount > 0 ? $"👁 {ViewerCount}" : "";

    /// <summary>
    /// Полный путь к видео на YouTube
    /// </summary>
    public string VideoUrl => $"https://www.youtube.com/watch?v={VideoId}";

    /// <summary>
    /// URL для встраивания (embed)
    /// </summary>
    public string EmbedUrl => $"https://www.youtube.com/embed/{VideoId}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Создание копии модели
    /// </summary>
    public YouTubeStreamModel Clone()
    {
        return new YouTubeStreamModel
        {
            VideoId = VideoId,
            Title = Title,
            IsShorts = IsShorts,
            ChannelName = ChannelName,
            IsSelected = IsSelected,
            IsLive = IsLive,
            ViewerCount = ViewerCount,
            ThumbnailUrl = ThumbnailUrl
        };
    }

    /// <summary>
    /// Сравнение по VideoId
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is YouTubeStreamModel other)
        {
            return VideoId == other.VideoId;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return VideoId?.GetHashCode() ?? 0;
    }

    public override string ToString()
    {
        return DisplayText;
    }
}