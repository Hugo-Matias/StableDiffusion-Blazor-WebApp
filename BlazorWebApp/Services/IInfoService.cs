namespace BlazorWebApp.Services;

public interface IInfoService
{
    InfoContent? CurrentInfo { get; }
    event Action? OnInfoChanged;
    void SetInfo(InfoContent content);
    void ClearInfo();
}

public record InfoContent(
    string Title,
    string Overview,
    List<ShortcutInfo> Shortcuts,
    List<string> Tips
);

public record ShortcutInfo(string Keys, string Description);
