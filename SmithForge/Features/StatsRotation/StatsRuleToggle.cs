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