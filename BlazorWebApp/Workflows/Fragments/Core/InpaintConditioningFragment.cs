using BlazorWebApp.Workflows.Builders;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Converts an uploaded black/white mask image into a MASK and applies ComfyUI inpaint conditioning.
/// </summary>
public class InpaintConditioningFragment
{
    public class Parameters
    {
        public string MaskImage { get; set; } = string.Empty;
        public (string nodeId, int index) PixelsRef { get; set; }
        public double Megapixels { get; set; } = 1;
        public int ResolutionSteps { get; set; } = 1;
        public bool NoiseMask { get; set; } = true;
        public string Scope { get; set; } = string.Empty;
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.MaskImage))
        {
            throw new InvalidOperationException("Inpaint conditioning requires an uploaded mask image.");
        }

        var scope = parameters.Scope;
        var maskLoaderId = $"{scope}inpaint_mask_loader";
        var maskScaleId = $"{scope}inpaint_mask_scale";
        var imageToMaskId = $"{scope}inpaint_image_to_mask";
        var conditioningId = $"{scope}inpaint_conditioning";

        var vaeRef = registry.GetRef($"{scope}vae_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");

        builder.AddNode(maskLoaderId, node => node
            .Type("LoadImage")
            .Title("Load Inpaint Mask")
            .Input("image", parameters.MaskImage));

        builder.AddNode(maskScaleId, node => node
            .Type("ImageScaleToTotalPixels")
            .Title("Scale Inpaint Mask")
            .Input("upscale_method", "nearest-exact")
            .Input("megapixels", parameters.Megapixels)
            .Input("resolution_steps", parameters.ResolutionSteps)
            .InputFromNode("image", maskLoaderId, 0));

        builder.AddNode(imageToMaskId, node => node
            .Type("ImageToMask")
            .Title("Convert Inpaint Image To Mask")
            .InputFromNode("image", maskScaleId, 0)
            .Input("channel", "red"));

        builder.AddNode(conditioningId, node => node
            .Type("InpaintModelConditioning")
            .Title("Inpaint Model Conditioning")
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", vaeRef)
            .InputRef("pixels", parameters.PixelsRef)
            .InputFromNode("mask", imageToMaskId, 0)
            .Input("noise_mask", parameters.NoiseMask));

        registry.Register($"{scope}positive_output", conditioningId, 0);
        registry.Register($"{scope}negative_output", conditioningId, 1);
        registry.Register($"{scope}latent_output", conditioningId, 2);
        registry.Register($"{scope}mask_output", imageToMaskId, 0);
    }
}