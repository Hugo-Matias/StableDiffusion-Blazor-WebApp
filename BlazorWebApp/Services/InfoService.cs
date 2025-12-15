namespace BlazorWebApp.Services;

public class InfoService : IInfoService
{
    private InfoContent? _currentInfo;

    public InfoContent? CurrentInfo => _currentInfo;

    public event Action? OnInfoChanged;

    public void SetInfo(InfoContent content)
    {
        _currentInfo = content;
        OnInfoChanged?.Invoke();
    }

    public void ClearInfo()
    {
        _currentInfo = null;
        OnInfoChanged?.Invoke();
    }
}
