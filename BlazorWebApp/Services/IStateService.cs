using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing application state and generation parameters.
    /// Handles persistence, initialization, and normalization of state data.
    /// </summary>
    public interface IStateService
    {
        // State properties
        AppState State { get; }
        Txt2ImgParameters ParametersTxt2Img { get; }
        Img2ImgParameters ParametersImg2Img { get; }
        UpscaleParameters ParametersUpscale { get; }
        Img2VidParameters ParametersImg2Vid { get; }

        // State management
        Task LoadState();
        Task SaveState();
        void InitializeParameters(ModeType[] modes);
    }
}
