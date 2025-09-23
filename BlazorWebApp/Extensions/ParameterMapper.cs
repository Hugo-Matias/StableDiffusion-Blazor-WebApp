using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Models;
using Comfy = BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using M = BlazorWebApp.Models;

namespace BlazorWebApp.Extensions
{
    public static class ParameterMapper
    {
        public static Comfy.sd.Txt2ImgParameters ToSDTxt2ImgParameters(this M.Txt2ImgParameters src, string checkpoint, string vae)
        {
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
                Denoise = src.DenoisingStrength,
                Checkpoint = checkpoint,
                VAE = vae
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
                DenoisingStrength = comfy.Denoise,
            };
        }

        /// <summary>
        /// Maps a PromptResponse<TInput> into existing GeneratedImages model.
        /// </summary>
        public static GeneratedImages ToGeneratedImages<TInput>(this PromptResponse<TInput> comfy)
            where TInput : Comfy.SharedParameters
        {
            if (comfy == null) throw new ArgumentNullException(nameof(comfy));

            return new GeneratedImages
            {
                Images = comfy.Images ?? new List<string>(),
                Parameters = comfy.Input.ToSharedParameters(),
                Info = comfy.Prompt.ToString()
            };
        }
    }
}
