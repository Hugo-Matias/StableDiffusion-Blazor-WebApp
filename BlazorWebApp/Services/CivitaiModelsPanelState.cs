using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Services;

public sealed class CivitaiModelsPanelState
{
    public CivitaiModelsDto? Models { get; set; }
    public CivitaiModelDto? SelectedModel { get; set; }
    public string? LastResultAnchorId { get; set; }
    public double LastResultsScrollTop { get; set; }

    public void ClearResults()
    {
        Models = null;
        SelectedModel = null;
        LastResultAnchorId = null;
        LastResultsScrollTop = 0;
    }
}