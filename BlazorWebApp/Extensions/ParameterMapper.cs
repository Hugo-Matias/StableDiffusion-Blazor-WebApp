using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Dtos.WebUI;
using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Extensions
{
    public static class ParameterMapper
    {
        #region ComfyUI
        public static Txt2ImgComfyUI ToTxt2ImgComfyUI(this Txt2ImgParameters src, string model, string vae)
        {
            var d = src.Scripts.ADetailer.Model1;

            var param = new Txt2ImgComfyUI
            {
                Prompt = src.Prompt,
                NegativePrompt = src.NegativePrompt,
                Width = src.Width,
                Height = src.Height,
                Seed = src.Seed,
                Steps = src.Steps,
                CfgScale = src.CfgScale,
                Guidance = src.DistilledCfgScale,
                SamplerName = src.SamplerName,
                Scheduler = src.Scheduler,
                BatchSize = src.BatchSize,
                Model = model,
                VAE = vae,
                Upscale = new()
                {
                    IsActive = src.EnableHR,
                    Model = src.HRUpscaler,
                    Mult = src.HRScale,
                    Width = src.HRWidth == 0 ? (int)Math.Round((decimal)src.HRScale * src.Width.Value) : src.HRWidth,
                    Height = src.HRHeight == 0 ? (int)Math.Round((decimal)src.HRScale * src.Height.Value) : src.HRHeight,
                    Steps = src.HRSecondPassSteps > 0 ? src.HRSecondPassSteps : src.Steps,
                    Denoise = src.DenoisingStrength,
                },
                Detailer = new()
                {
                    IsActive = src.Scripts.ADetailer.IsEnabled,
                    Model = d.Model,
                    Prompt = string.IsNullOrWhiteSpace(d.Prompt) ? src.Prompt : d.Prompt,
                    NegativePrompt = string.IsNullOrWhiteSpace(d.NegativePrompt) ? src.NegativePrompt : d.NegativePrompt,
                    Sampler = d.Sampler,
                    Scheduler = d.Scheduler,
                    Denoise = d.DenoisingStrength,
                    BBoxThreshold = d.Confidence,
                    Feather = d.MaskBlur,
                    DropSize = d.DropSize,
                    Checkpoint = d.UseCheckpoint && !string.IsNullOrWhiteSpace(d.Checkpoint) ? d.Checkpoint : model,
                    Steps = d.UseSteps ? d.Steps : src.Steps,
                    CfgScale = d.UseCFGScale ? d.CFGScale : src.CfgScale,
                    GuideSize = d.GuideSize,
                    MaxSize = d.MaxSize,
                    BBoxDilation = d.BBoxDilation,
                    BBoxCropFactor = d.BBoxCropFactor,
                    Seed = src.Seed,
                    Cycle = d.Cycle <= 0 ? 1 : d.Cycle,
                    Loras = d.Loras ?? []
                }
            };

            param.Detailer.ParseComfyDetailerLoras();
            return param;
        }

        public static SharedParameters ToSharedParameters(this SharedComfyUI comfy)
        {
            return new SharedParameters
            {
                Prompt = comfy.Prompt,
                NegativePrompt = comfy.NegativePrompt,
                Width = comfy.Width,
                Height = comfy.Height,
                Seed = comfy.Seed,
                Steps = comfy.Steps,
                CfgScale = comfy.CfgScale,
                SamplerName = comfy.SamplerName,
                Scheduler = comfy.Scheduler,
                BatchSize = comfy.BatchSize,
            };
        }

        /// <summary>
        /// Maps a PromptResponse<TInput> into existing GeneratedImages model.
        /// </summary>
        public static GeneratedImages ToGeneratedImages<TInput>(this ComfyUIPromptResponse<TInput> comfy)
            where TInput : SharedComfyUI
        {
            if (comfy == null) throw new ArgumentNullException(nameof(comfy));

            return new GeneratedImages
            {
                Images = comfy.Images ?? new List<string>(),
                Parameters = comfy.Input.ToSharedParameters(),
                Info = WriteGeneratedImagesInfo(comfy)
            };
        }

        private static string WriteGeneratedImagesInfo<TInput>(ComfyUIPromptResponse<TInput> response)
        {
            var merged = new
            {
                input = response.Input,
                prompt = response.Prompt
            };

            return JsonSerializer.Serialize(
                merged,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
        #endregion

        #region WebUI
        public static Txt2ImgWebUI ToTxt2ImgWebUI(this Txt2ImgParameters src)
        {
            return new Txt2ImgWebUI
            {
                Prompt = src.Prompt,
                NegativePrompt = src.NegativePrompt,
                Styles = src.Styles,
                Seed = src.Seed,
                Subseed = src.Subseed,
                SubseedStrength = src.SubseedStrength,
                SeedResizeFromH = src.SeedResizeFromH,
                SeedResizeFromW = src.SeedResizeFromW,
                SamplerName = src.SamplerName,
                Scheduler = src.Scheduler,
                BatchSize = src.BatchSize,
                NIter = src.NIter,
                Steps = src.Steps,
                CfgScale = src.CfgScale,
                DistilledCfgScale = src.DistilledCfgScale,
                DenoisingStrength = src.DenoisingStrength,
                Width = src.Width,
                Height = src.Height,
                RestoreFaces = src.RestoreFaces,
                Tiling = src.Tiling,
                Eta = src.Eta,
                SChurn = src.SChurn,
                STmax = src.STmax,
                STmin = src.STmin,
                SNoise = src.SNoise,
                SamplerIndex = src.SamplerIndex,
                RefinerCheckpoint = src.RefinerCheckpoint,
                RefinerSwitchAt = src.RefinerSwitchAt,
                AlwaysOnScripts = src.AlwaysOnScripts,
                ScriptName = src.ScriptName,
                ScriptArgs = src.ScriptArgs,
                EnableHR = src.EnableHR,
                FirstphaseWidth = src.FirstphaseWidth,
                FirstphaseHeight = src.FirstphaseHeight,
                HRScale = src.HRScale,
                HRUpscaler = src.HRUpscaler,
                HRSecondPassSteps = src.HRSecondPassSteps,
                HRWidth = src.HRWidth,
                HRHeight = src.HRHeight,
                Scripts = new()
                {
                    ControlNet = src.Scripts.ControlNet,
                    Cutoff = src.Scripts.Cutoff,
                    DynamicPrompts = src.Scripts.DynamicPrompts,
                    MultiDiffusionTiledDiffusion = src.Scripts.MultiDiffusionTiledDiffusion,
                    MultiDiffusionTiledVae = src.Scripts.MultiDiffusionTiledVae,
                    RegionalPrompter = src.Scripts.RegionalPrompter,
                    XYZPlot = src.Scripts.XYZPlot,
                    ADetailer = src.Scripts.ADetailer,
                    Incantations = src.Scripts.Incantations,
                },
            };
        }

        public static Img2ImgWebUI ToImg2ImgWebUI(this Img2ImgParameters src)
        {
            return new Img2ImgWebUI
            {
                Prompt = src.Prompt,
                NegativePrompt = src.NegativePrompt,
                Styles = src.Styles,
                Seed = src.Seed,
                Subseed = src.Subseed,
                SubseedStrength = src.SubseedStrength,
                SeedResizeFromH = src.SeedResizeFromH,
                SeedResizeFromW = src.SeedResizeFromW,
                SamplerName = src.SamplerName,
                Scheduler = src.Scheduler,
                BatchSize = src.BatchSize,
                NIter = src.NIter,
                Steps = src.Steps,
                CfgScale = src.CfgScale,
                DistilledCfgScale = src.DistilledCfgScale,
                DenoisingStrength = src.DenoisingStrength,
                Width = src.Width,
                Height = src.Height,
                RestoreFaces = src.RestoreFaces,
                Tiling = src.Tiling,
                Eta = src.Eta,
                SChurn = src.SChurn,
                STmax = src.STmax,
                STmin = src.STmin,
                SNoise = src.SNoise,
                SamplerIndex = src.SamplerIndex,
                RefinerCheckpoint = src.RefinerCheckpoint,
                RefinerSwitchAt = src.RefinerSwitchAt,
                AlwaysOnScripts = src.AlwaysOnScripts,
                ScriptName = src.ScriptName,
                ScriptArgs = src.ScriptArgs,
                InitImages = src.InitImages,
                Mask = src.Mask,
                MaskBlur = src.MaskBlur,
                ResizeMode = src.ResizeMode,
                InpaintFullRes = src.InpaintFullRes,
                InpaintFullResPadding = src.InpaintFullResPadding,
                InpaintingFill = src.InpaintingFill,
                InpaintingMaskInvert = src.InpaintingMaskInvert,
                Scripts = new()
                {
                    ControlNet = src.Scripts.ControlNet,
                    Cutoff = src.Scripts.Cutoff,
                    DynamicPrompts = src.Scripts.DynamicPrompts,
                    MultiDiffusionTiledDiffusion = src.Scripts.MultiDiffusionTiledDiffusion,
                    MultiDiffusionTiledVae = src.Scripts.MultiDiffusionTiledVae,
                    RegionalPrompter = src.Scripts.RegionalPrompter,
                    XYZPlot = src.Scripts.XYZPlot,
                    ADetailer = src.Scripts.ADetailer,
                    Incantations = src.Scripts.Incantations,
                },
            };
        }
        #endregion
    }
}
