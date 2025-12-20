namespace BlazorWebApp.Models
{
    /// <summary>
    /// Centralized registry of fragment and parameter identifiers.
    /// These values are derived from the fragment .sbn schemas and serve as
    /// compile-time constants for type-safe fragment/parameter access.
    /// 
    /// IMPORTANT: These values must match the fragment file definitions.
    /// The fragment files are the source of truth - update them first,
    /// then regenerate these constants.
    /// </summary>
    public static class FragmentKeys
    {
        /// <summary>
        /// Well-known fragment IDs used in workflow Pipeline[].id.
        /// These match the convention: filename without extension, dashes replaced with underscores.
        /// </summary>
        public static class Fragments
        {
            // Core fragments (order 0-49)
            public const string Prompts = "prompts";
            public const string Latent = "latent";
            public const string EmptyLatent = "empty_latent";
            public const string LoadCheckpoint = "load_checkpoint";
            public const string LoadDiffusion = "load_diffusion";
            
            // Primary fragments (order 50-99)
            public const string MainSampler = "main_sampler";
            public const string Sampler = "sampler";
            public const string SamplerStandard = "sampler_standard";
            
            // Enhancement fragments (order 100-149)
            public const string Upscale = "upscale";
            public const string Detailer = "detailer";
            public const string DetailerCore = "detailer_core";
            public const string RefinerSampler = "refiner_sampler";
            
            // Utility fragments (order 150+)
            public const string Save = "save";
            public const string VaeDecode = "vae_decode";
            public const string CleanVram = "clean_vram";
            
            // Model-specific fragments
            public const string LoadFlux = "loader_flux";
            public const string LoadDetailer = "loader_detailer";
            
            // Video generation fragments (Wan)
            public const string FrameInterpolation = "frame_interpolation";
            public const string LoadWanModel = "load_wan_model";
            public const string SamplerWan = "sampler_wan";
            
            // Optional feature fragments
            public const string SeedVR2 = "seed_vr2";
            public const string ConditioningVariation = "conditioning_variation";
            public const string SeedVarianceEnhancer = "seed_variance_enhancer";
            public const string Llm = "llm";
            
            // Qwen/Edit fragments
            public const string LoadQwenEdit = "load_qwen_edit";
            public const string EncodeEdit = "encode_edit";
        }

        /// <summary>
        /// Well-known parameter names used in fragment Values dictionaries.
        /// These match the parameter keys defined in fragment #meta.ui.parameters.
        /// </summary>
        public static class Params
        {
            // Prompts fragment parameters
            public const string Positive = "positive";
            public const string Negative = "negative";
            
            // Sampler fragment parameters
            public const string SamplerName = "sampler_name";
            public const string Scheduler = "scheduler";
            public const string Steps = "steps";
            public const string Seed = "seed";
            public const string Cfg = "cfg";
            public const string Denoise = "denoise";
            public const string Eta = "eta";
            
            // Latent/Resolution fragment parameters
            public const string Width = "width";
            public const string Height = "height";
            public const string BatchSize = "batch_size";
            
            // Common model parameters (used in load fragments)
            public const string UnetName = "unet_name";
            public const string ClipName = "clip_name";
            public const string VaeName = "vae_name";
            
            // Upscale parameters
            public const string UpscaleModel = "upscale_model";
            public const string UpscaleWidth = "upscale_width";
            public const string UpscaleHeight = "upscale_height";
            public const string UpscaleSteps = "upscale_steps";
            public const string UpscaleDenoise = "upscale_denoise";
            
            // Detailer parameters
            public const string DetailerDetectionModel = "detailer_detection_model";
            public const string DetailerSampler = "detailer_sampler";
            public const string DetailerScheduler = "detailer_scheduler";
            public const string DetailerSeed = "detailer_seed";
            public const string DetailerSteps = "detailer_steps";
            public const string DetailerCfg = "detailer_cfg";
            public const string DetailerDenoise = "detailer_denoise";
            public const string DetailerFeather = "detailer_feather";
            public const string DetailerBBoxThreshold = "detailer_bbox_threshold";
            public const string DetailerBBoxDilation = "detailer_bbox_dilation";
            public const string DetailerBBoxCropFactor = "detailer_bbox_crop_factor";
            public const string DetailerDropSize = "detailer_drop_size";
            public const string DetailerGuideSize = "detailer_guide_size";
            public const string DetailerMaxSize = "detailer_max_size";
            public const string DetailerCycle = "detailer_cycle";
            
            // Flux-specific parameters
            public const string Guidance = "guidance";
            public const string RefluxEnabled = "reflux_enabled";
            public const string Scaling = "scaling";
            public const string MaxShift = "max_shift";
            public const string BaseShift = "base_shift";
            
            // SeedVR2 parameters
            public const string BlocksToSwap = "blocks_to_swap";
            public const string VaeTileSize = "vae_tile_size";
            public const string VaeTileOverlap = "vae_tile_overlap";
            public const string InputNoiseScale = "input_noise_scale";
            public const string LatentNoiseScale = "latent_noise_scale";
            public const string Scale = "scale";
            
            // Conditioning variation parameters
            public const string SwitchPoint = "switch_point";
            
            // Seed variance enhancer parameters
            public const string RandomizePercent = "randomize_percent";
            public const string Strength = "strength";
            public const string NoiseInsert = "noise_insert";
            public const string StepsSwitchoverPercent = "steps_switchover_percent";
            public const string MaskStartsAt = "mask_starts_at";
            public const string MaskPercent = "mask_percent";
            
            // Video/Wan parameters
            public const string VideoLength = "video_length";
            public const string FrameRate = "frame_rate";
            public const string MotionAmplitude = "motion_amplitude";
            public const string Shift = "shift";
            public const string Multiplier = "multiplier";
            public const string RifeModel = "rife_model";
            public const string ScaleBy = "scale_by";
            
            // Scope parameters (used for scoped fragments)
            public const string Scope = "scope";
            public const string ScopeTitle = "scope_title";
            public const string Title = "title";
            public const string SamplerId = "sampler_id";
            public const string ClassType = "class_type";
            public const string LatentClass = "latent_class";
        }

        /// <summary>
        /// Well-known asset parameter names defined in workflow template Assets[].parameter.
        /// These are used in GenerationParameters.Assets dictionary.
        /// </summary>
        public static class Assets
        {
            public const string Model = "Model";
            public const string Vae = "VAE";
            public const string Clip = "Clip";
            public const string Clip1 = "Clip1";
            public const string Clip2 = "Clip2";
            public const string ClipVision = "ClipVision";
            
            // Wan/Video model assets
            public const string HighModel = "HighModel";
            public const string LowModel = "LowModel";
            
            // Detailer assets
            public const string DetailerCheckpoint = "DetailerCheckpoint";
        }

        /// <summary>
        /// Well-known source IDs defined in workflow template Sources[].id.
        /// These are used in GenerationParameters.Sources dictionary.
        /// </summary>
        public static class Sources
        {
            public const string SourceImage = "source_image";
            public const string ReferencePose = "reference_pose";
            public const string MaskImage = "mask_image";
            public const string ControlImage = "control_image";
            public const string InputVideo = "input_video";
        }

        /// <summary>
        /// Fragment file names (relative to Workflows/Fragments/).
        /// Used when creating new fragments or looking up schemas.
        /// </summary>
        public static class Files
        {
            public const string Prompts = "prompts.sbn";
            public const string EmptyLatent = "empty-latent.sbn";
            public const string Latent = "empty-latent.sbn"; // Alias for EmptyLatent
            public const string Sampler = "sampler.sbn";
            public const string SamplerStandard = "sampler-standard.sbn";
            public const string Save = "save.sbn";
            public const string Upscale = "upscale.sbn";
            public const string Detailer = "detailer.sbn";
            public const string DetailerCore = "detailer-core.sbn";
            public const string LoadCheckpoint = "load-checkpoint.sbn";
            public const string LoadDiffusion = "load-diffusion.sbn";
            public const string CleanVram = "clean-vram.sbn";
            public const string LoraLoader = "lora-loader.sbn";
            public const string VaeDecode = "vae-decode.sbn";
            
            // Flux fragments
            public const string FluxLoad = "flux/load-flux.sbn";
            
            // Wan fragments
            public const string WanLoadModel = "wan/load-wan-model.sbn";
            public const string WanSampler = "wan/sampler-wan.sbn";
            public const string WanFrameInterpolation = "wan/frame-interpolation.sbn";
            public const string WanPrompts = "wan/prompts.sbn";
            
            // Optional feature fragments
            public const string SeedVR2 = "seed-vr2.sbn";
            public const string ConditioningVariation = "conditioning-variation.sbn";
            public const string SeedVarianceEnhancer = "seed-variance-enhancer.sbn";
            public const string Llm = "llm.sbn";
        }
    }
}
