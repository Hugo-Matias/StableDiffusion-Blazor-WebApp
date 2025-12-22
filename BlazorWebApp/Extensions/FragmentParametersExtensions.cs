using BlazorWebApp.Models;
using BlazorWebApp.Models.Fragments;

namespace BlazorWebApp.Extensions
{
    /// <summary>
    /// Extension methods for type-safe access to FragmentParameters values.
    /// Provides strongly-typed wrappers that implement value interfaces.
    /// </summary>
    public static class FragmentParametersExtensions
    {
        /// <summary>
        /// Gets a strongly-typed sampler values accessor for this fragment.
        /// </summary>
        public static ISamplerValues AsSampler(this FragmentParameters fragment)
            => new SamplerValuesAccessor(fragment);

        /// <summary>
        /// Gets a strongly-typed latent values accessor for this fragment.
        /// </summary>
        public static ILatentValues AsLatent(this FragmentParameters fragment)
            => new LatentValuesAccessor(fragment);

        /// <summary>
        /// Gets a strongly-typed prompts values accessor for this fragment.
        /// </summary>
        public static IPromptsValues AsPrompts(this FragmentParameters fragment)
            => new PromptsValuesAccessor(fragment);

        /// <summary>
        /// Gets a strongly-typed detailer values accessor for this fragment.
        /// </summary>
        public static IDetailerValues AsDetailer(this FragmentParameters fragment)
            => new DetailerValuesAccessor(fragment);

        /// <summary>
        /// Gets a strongly-typed upscale values accessor for this fragment.
        /// </summary>
        public static IUpscaleValues AsUpscale(this FragmentParameters fragment)
            => new UpscaleValuesAccessor(fragment);

        #region Accessor Implementations

        private sealed class SamplerValuesAccessor : ISamplerValues
        {
            private readonly FragmentParameters _fragment;

            public SamplerValuesAccessor(FragmentParameters fragment)
                => _fragment = fragment;

            public int Steps
            {
                get => _fragment.GetValueOrDefault("steps", 20);
                set => _fragment.SetValue("steps", value);
            }

            public double Cfg
            {
                get => _fragment.GetValueOrDefault("cfg", 7.0);
                set => _fragment.SetValue("cfg", value);
            }

            public long Seed
            {
                get => _fragment.GetValueOrDefault("seed", -1L);
                set => _fragment.SetValue("seed", value);
            }

            public string SamplerName
            {
                get => _fragment.GetValueOrDefault("sampler_name", "euler");
                set => _fragment.SetValue("sampler_name", value);
            }

            public string Scheduler
            {
                get => _fragment.GetValueOrDefault("scheduler", "normal");
                set => _fragment.SetValue("scheduler", value);
            }

            public double Denoise
            {
                get => _fragment.GetValueOrDefault("denoise", 1.0);
                set => _fragment.SetValue("denoise", value);
            }
        }

        private sealed class LatentValuesAccessor : ILatentValues
        {
            private readonly FragmentParameters _fragment;

            public LatentValuesAccessor(FragmentParameters fragment)
                => _fragment = fragment;

            public int Width
            {
                get => _fragment.GetValueOrDefault("width", 512);
                set => _fragment.SetValue("width", value);
            }

            public int Height
            {
                get => _fragment.GetValueOrDefault("height", 512);
                set => _fragment.SetValue("height", value);
            }

            public int BatchSize
            {
                get => _fragment.GetValueOrDefault("batch_size", 1);
                set => _fragment.SetValue("batch_size", value);
            }
        }

        private sealed class PromptsValuesAccessor : IPromptsValues
        {
            private readonly FragmentParameters _fragment;

            public PromptsValuesAccessor(FragmentParameters fragment)
                => _fragment = fragment;

            public string Positive
            {
                get => _fragment.GetValueOrDefault("positive", "");
                set => _fragment.SetValue("positive", value);
            }

            public string Negative
            {
                get => _fragment.GetValueOrDefault("negative", "");
                set => _fragment.SetValue("negative", value);
            }
        }

        private sealed class DetailerValuesAccessor : IDetailerValues
        {
            private readonly FragmentParameters _fragment;

            public DetailerValuesAccessor(FragmentParameters fragment)
                => _fragment = fragment;

            public bool IsActive
            {
                get => _fragment.IsActive;
                set => _fragment.IsActive = value;
            }

            public double DetailerCfg
            {
                get => _fragment.GetValueOrDefault("detailer_cfg", 8.0);
                set => _fragment.SetValue("detailer_cfg", value);
            }

            public double DetailerDenoise
            {
                get => _fragment.GetValueOrDefault("detailer_denoise", 0.5);
                set => _fragment.SetValue("detailer_denoise", value);
            }

            public int DetailerSteps
            {
                get => _fragment.GetValueOrDefault("detailer_steps", 20);
                set => _fragment.SetValue("detailer_steps", value);
            }

            public string DetectionModel
            {
                get => _fragment.GetValueOrDefault("detection_model", "");
                set => _fragment.SetValue("detection_model", value);
            }
        }

        private sealed class UpscaleValuesAccessor : IUpscaleValues
        {
            private readonly FragmentParameters _fragment;

            public UpscaleValuesAccessor(FragmentParameters fragment)
                => _fragment = fragment;

            public bool IsActive
            {
                get => _fragment.IsActive;
                set => _fragment.IsActive = value;
            }

            public double UpscaleFactor
            {
                get => _fragment.GetValueOrDefault("upscale_factor", 2.0);
                set => _fragment.SetValue("upscale_factor", value);
            }

            public string UpscaleModel
            {
                get => _fragment.GetValueOrDefault("upscale_model", "");
                set => _fragment.SetValue("upscale_model", value);
            }
        }

        #endregion
    }
}
