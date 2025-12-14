using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Extensions
{
    public static class ParameterMapper
    {
        #region ComfyUI
        public static Txt2ImgComfyUI ToTxt2ImgComfyUI(this Txt2ImgParameters src, string model, string vae)
        {
            var param = new Txt2ImgComfyUI
            {
                Loras = src.Loras ?? new List<Lora>(),
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
                Vae = vae,
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
                Detailer = null, // ADetailer removed - Scripts system no longer supported
                SeedVR2 = src.SeedVR2,
                ConditioningVariation = src.ConditioningVariation
            };

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

        // ToTxt2ImgWebUI and ToImg2ImgWebUI methods removed - WebUI no longer supported
    }
}
