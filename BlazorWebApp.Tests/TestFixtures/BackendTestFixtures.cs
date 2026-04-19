using BlazorWebApp.Models;
using Moq;

namespace BlazorWebApp.Tests.TestFixtures
{
    /// <summary>
    /// Test fixtures for Backend-related tests
    /// </summary>
    public static class BackendTestFixtures
    {
        public static Options GetSampleOptions()
        {
            return new Options
            {
                ClipSkip = 2,
                SaveTxt = true,
                GridSave = false,
                SamplesSave = true,
                SamplesFormat = "png"
            };
        }

        public static List<BlazorWebApp.Models.Sampler> GetSampleSamplers()
        {
            return new List<BlazorWebApp.Models.Sampler>
            {
                new BlazorWebApp.Models.Sampler { Name = "Euler" },
                new BlazorWebApp.Models.Sampler { Name = "Euler a" },
                new BlazorWebApp.Models.Sampler { Name = "DPM++ 2M Karras" },
                new BlazorWebApp.Models.Sampler { Name = "DPM++ SDE Karras" }
            };
        }

        public static List<Scheduler> GetSampleSchedulers()
        {
            return new List<Scheduler>
            {
                new Scheduler { Name = "normal" },
                new Scheduler { Name = "karras" },
                new Scheduler { Name = "exponential" },
                new Scheduler { Name = "sgm_uniform" }
            };
        }

        public static List<Upscaler> GetSampleUpscalers()
        {
            return new List<Upscaler>
            {
                new Upscaler { Name = "None" },
                new Upscaler { Name = "Lanczos" },
                new Upscaler { Name = "ESRGAN_4x" },
                new Upscaler { Name = "R-ESRGAN 4x+" }
            };
        }
    }
}
