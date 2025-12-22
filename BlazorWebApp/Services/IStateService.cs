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
        
        /// <summary>
        /// Gets the current generation parameters (unified model).
        /// </summary>
        GenerationParameters GenerationParameters { get; }

        // State management
        Task LoadState();
        Task LoadState(int stateId);
        Task SaveState();
        
        /// <summary>
        /// Initializes GenerationParameters with default values from settings.
        /// Called when starting fresh (no saved state) or resetting parameters.
        /// </summary>
        void InitializeGenerationParameters();

        // Parameter loading from images
        /// <summary>
        /// Loads all parameters from an image entity into GenerationParameters.
        /// This populates the fragments with values from the saved image.
        /// </summary>
        Task LoadGenerationParametersFromImage(Image image);

        // Workflow management
        void SetWorkflowBase(ModelBase workflowBase);
        void MigrateLegacySettings();
    }
}
