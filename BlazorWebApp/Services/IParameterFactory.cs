using BlazorWebApp.Data.Dtos.WebUI;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Factory for creating script parameter objects with default values from settings.
    /// Centralizes script parameter initialization logic.
    /// </summary>
    public interface IParameterFactory
    {
        ScriptParametersControlNet CreateControlNet();
        ScriptParametersCutoff CreateCutoff();
        ScriptParametersDynamicPrompts CreateDynamicPrompts();
        ScriptParametersUltimateUpscale CreateUltimateUpscale();
        ScriptParametersMultiDiffusionTiledDiffusion CreateMultiDiffusionTiledDiffusion();
        ScriptParametersMultiDiffusionTiledVae CreateMultiDiffusionTiledVae();
        ScriptParametersRegionalPrompter CreateRegionalPrompter();
        ScriptParametersXYZPlot CreateXYZPlot();
        ScriptParametersADetailer CreateADetailer();
        ScriptParametersIncantations CreateIncantationsModel();
    }
}
