using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Builds a face-region noise mask for the V2V Just-Talk pipeline so the
/// sampler only re-paints the mouth/face area while every other pixel is
/// taken verbatim from the encoded source video latent.
///
/// Pipeline:
///   <c>FaceSegment</c> (KJNodes)
///   -&gt; <c>BlockifyMask</c> (KJNodes)
///   -&gt; <c>ResizeImageMaskNode</c> (KJNodes, scale by 0.5)
///   -&gt; <c>LTXVPreprocessMasks</c> (Lightricks)
///   -&gt; <c>LTXVSetVideoLatentNoiseMasks</c> (Lightricks).
///
/// Reads:
/// <c>{scope}{ImagesInputName}</c> (image batch the segmenter inspects),
/// <c>{scope}{LatentInputName}</c> (the video latent to mark masks on),
/// <c>{scope}{VaeInputName}</c>.
///
/// Re-registers <c>{scope}video_latent</c> as the masked latent.
/// </summary>
public class LtxFaceMaskFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_face_mask",
        Type = FragmentType.Latent,
        Title = "LTX Face Mask",
        IsHidden = true
    };

    public class Parameters
    {
        public string ImagesInputName { get; set; } = "loaded_video_images";
        public string LatentInputName { get; set; } = "video_latent";
        public string VaeInputName { get; set; } = "vae_output";
        public string? LatentOutputName { get; set; }

        // FaceSegment widgets (mirrors upstream V2V Just Talk widgets):
        // [skin, l_brow, r_brow, l_eye, r_eye, eye_g, l_ear, r_ear,
        //  ear_r, nose, mouth, u_lip, l_lip, neck, neck_l, image_size,
        //  invert_mask, blur, blur_alt, output_format, hex_color]
        public bool Skin { get; set; } = true;
        public bool LeftBrow { get; set; } = true;
        public bool RightBrow { get; set; } = false;
        public bool LeftEye { get; set; } = true;
        public bool RightEye { get; set; } = true;
        public bool EyeGlasses { get; set; } = true;
        public bool LeftEar { get; set; } = true;
        public bool RightEar { get; set; } = true;
        public bool EarRing { get; set; } = true;
        public bool Nose { get; set; } = true;
        public bool Mouth { get; set; } = true;
        public bool UpperLip { get; set; } = true;
        public bool LowerLip { get; set; } = false;
        public bool Neck { get; set; } = false;
        public bool NeckLine { get; set; } = false;
        public int ImageSize { get; set; } = 512;
        public int InvertMask { get; set; } = 0;
        public int Blur { get; set; } = 10;
        public bool BlurAlt { get; set; } = false;
        public string OutputFormat { get; set; } = "Alpha";
        public string HexColor { get; set; } = "#222222";

        // BlockifyMask widgets.
        public int BlockifyBlockSize { get; set; } = 12;
        public string BlockifyDevice { get; set; } = "cpu";

        // ResizeImageMaskNode widgets (scale by multiplier).
        public string ResizeMode { get; set; } = "scale by multiplier";
        public double ResizeMultiplier { get; set; } = 0.5;
        public string ResizeInterpolation { get; set; } = "nearest-exact";

        // LTXVPreprocessMasks widgets.
        public bool InvertInputMasks { get; set; } = false;
        public bool IgnoreFirstMask { get; set; } = false;
        public string PoolingMethod { get; set; } = "max";
        public int GrowMask { get; set; } = 0;
        public bool TaperedCorners { get; set; } = true;
        public double ClampMin { get; set; } = 0.5;
        public double ClampMax { get; set; } = 1.0;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var faceId = $"{scope}ltx_face_segment";
        var blockifyId = $"{scope}ltx_blockify_mask";
        var resizeId = $"{scope}ltx_resize_face_mask";
        var preprocessId = $"{scope}ltx_preprocess_masks";
        var applyId = $"{scope}ltx_set_latent_noise_masks";

        var imagesRef = registry.GetRef($"{scope}{p.ImagesInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");
        var vaeRef = registry.GetRef($"{scope}{p.VaeInputName}");

        builder.AddNode(faceId, node => node
            .Type("FaceSegment")
            .Title($"{scopeTitle}Face Segment")
            .InputRef("images", imagesRef)
            .Input("skin", p.Skin)
            .Input("l_brow", p.LeftBrow)
            .Input("r_brow", p.RightBrow)
            .Input("l_eye", p.LeftEye)
            .Input("r_eye", p.RightEye)
            .Input("eye_g", p.EyeGlasses)
            .Input("l_ear", p.LeftEar)
            .Input("r_ear", p.RightEar)
            .Input("ear_r", p.EarRing)
            .Input("nose", p.Nose)
            .Input("mouth", p.Mouth)
            .Input("u_lip", p.UpperLip)
            .Input("l_lip", p.LowerLip)
            .Input("neck", p.Neck)
            .Input("neck_l", p.NeckLine)
            .Input("image_size", p.ImageSize)
            .Input("invert_mask", p.InvertMask)
            .Input("blur", p.Blur)
            .Input("blur_alt", p.BlurAlt)
            .Input("output_format", p.OutputFormat)
            .Input("hex_color", p.HexColor));

        // FaceSegment outputs: image[0], mask[1].
        builder.AddNode(blockifyId, node => node
            .Type("BlockifyMask")
            .Title($"{scopeTitle}Blockify Mask")
            .InputFromNode("masks", faceId, 1)
            .Input("block_size", p.BlockifyBlockSize)
            .Input("device", p.BlockifyDevice));

        builder.AddNode(resizeId, node => node
            .Type("ResizeImageMaskNode")
            .Title($"{scopeTitle}Resize Mask")
            .InputFromNode("input", blockifyId, 0)
            .Input("resize_type", p.ResizeMode)
            .Input("multiplier", p.ResizeMultiplier)
            .Input("interpolation", p.ResizeInterpolation));

        builder.AddNode(preprocessId, node => node
            .Type("LTXVPreprocessMasks")
            .Title($"{scopeTitle}LTXV Preprocess Masks")
            .InputFromNode("masks", resizeId, 0)
            .InputRef("vae", vaeRef)
            .Input("invert_input_masks", p.InvertInputMasks)
            .Input("ignore_first_mask", p.IgnoreFirstMask)
            .Input("pooling_method", p.PoolingMethod)
            .Input("grow_mask", p.GrowMask)
            .Input("tapered_corners", p.TaperedCorners)
            .Input("clamp_min", p.ClampMin)
            .Input("clamp_max", p.ClampMax));

        builder.AddNode(applyId, node => node
            .Type("LTXVSetVideoLatentNoiseMasks")
            .Title($"{scopeTitle}LTXV Set Video Latent Noise Masks")
            .InputRef("samples", latentRef)
            .InputFromNode("masks", preprocessId, 0));

        registry.Register($"{scope}{p.LatentOutputName ?? p.LatentInputName}", applyId, 0);
    }
}
