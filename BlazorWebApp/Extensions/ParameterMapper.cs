using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Models;
using System.Text.Json;
using Comfy = BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using M = BlazorWebApp.Models;

namespace BlazorWebApp.Extensions
{
    public static class ParameterMapper
    {
        public static Comfy.sd.Txt2ImgParameters ToSDTxt2ImgParameters(this M.Txt2ImgParameters src, string checkpoint, string vae)
        {
            var d = src.Scripts.ADetailer.Model1;

            return new Comfy.sd.Txt2ImgParameters
            {
                Prompt = src.Prompt,
                NegativePrompt = src.NegativePrompt,
                Width = src.Width,
                Height = src.Height,
                Seed = src.Seed,
                Steps = src.Steps,
                CfgScale = src.CfgScale,
                SamplerName = src.SamplerName,
                Scheduler = src.Scheduler,
                BatchSize = src.BatchSize,
                Checkpoint = checkpoint,
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
                    Checkpoint = d.UseCheckpoint && !string.IsNullOrWhiteSpace(d.Checkpoint) ? d.Checkpoint : checkpoint,
                    Steps = d.UseSteps ? d.Steps : src.Steps,
                    CfgScale = d.UseCFGScale ? d.CFGScale : src.CfgScale,
                    GuideSize = d.GuideSize,
                    MaxSize = d.MaxSize,
                    BBoxDilation = d.BBoxDilation,
                    BBoxCropFactor = d.BBoxCropFactor,
                    Seed = src.Seed,
                    Cycle = d.Cycle <= 0 ? 1 : d.Cycle,
                }
            };
        }

        public static SharedParameters ToSharedParameters(this Comfy.SharedParameters comfy)
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
            where TInput : Comfy.SharedParameters
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
    }
}
