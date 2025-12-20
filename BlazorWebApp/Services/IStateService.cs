using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

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
        
        /// <summary>
        /// Gets the current generation parameters (new unified model).
        /// </summary>
        GenerationParameters GenerationParameters { get; }

        // State management
        Task LoadState();
        Task LoadState(int stateId);
        Task SaveState();
        void InitializeParameters(ModeType[] modes);
        
        /// <summary>
        /// Initializes GenerationParameters with default values from settings.
        /// Called when starting fresh (no saved state) or resetting parameters.
        /// </summary>
        void InitializeGenerationParameters();

        // Parameter loading from images
        Task LoadParametersFromImage(Image image, ModeType mode);
        void SetParameterFromImage(Image image, string parameter, ModeType mode);
        
        /// <summary>
        /// Loads all parameters from an image entity into GenerationParameters (new flow).
        /// This populates the fragments with values from the saved image.
        /// </summary>
        Task LoadGenerationParametersFromImage(Image image);

        // Workflow management
        void SetWorkflowBase(ModelBase workflowBase);
        void MigrateLegacySettings();
    }
}
