namespace BlazorWebApp.Services;

public class InfoService : IInfoService
{
    private InfoContent? _currentInfo;
    private string? _highlightedSectionId;

    public InfoContent? CurrentInfo => _currentInfo;
    public string? HighlightedSectionId => _highlightedSectionId;

    public event Action? OnInfoChanged;

    public void SetInfo(InfoContent content)
    {
        _currentInfo = content;
        OnInfoChanged?.Invoke();
    }

    public void ClearInfo()
    {
        _currentInfo = null;
        _highlightedSectionId = null;
        OnInfoChanged?.Invoke();
    }

    public void HighlightSection(string? sectionId)
    {
        if (_highlightedSectionId == sectionId) return;
        _highlightedSectionId = sectionId;
        OnInfoChanged?.Invoke();
    }
}

