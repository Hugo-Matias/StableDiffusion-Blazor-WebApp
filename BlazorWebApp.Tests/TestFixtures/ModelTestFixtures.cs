using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.TestFixtures
{
    /// <summary>
    /// Test fixtures for Model-related tests
    /// </summary>
    public static class ModelTestFixtures
    {
        public static List<SDModel> GetSampleCheckpointModels()
        {
            return new List<SDModel>
            {
                new SDModel { Model_name = "sd_xl_base_1.0.safetensors", Title = "sd_xl_base_1.0.safetensors [31e35c80fc]", Hash = "31e35c80fc" },
                new SDModel { Model_name = "v1-5-pruned-emaonly.safetensors", Title = "v1-5-pruned-emaonly.safetensors [6ce0161689]", Hash = "6ce0161689" },
                new SDModel { Model_name = "realisticVisionV60B1_v51VAE.safetensors", Title = "realisticVisionV60B1_v51VAE.safetensors [15012c538f]", Hash = "15012c538f" }
            };
        }

        public static List<SDModel> GetSampleDiffusionModels()
        {
            return new List<SDModel>
            {
                new SDModel { Model_name = "flux1-dev.safetensors", Title = "flux1-dev.safetensors [a6bd8c16ff]", Hash = "a6bd8c16ff" },
                new SDModel { Model_name = "flux1-schnell.safetensors", Title = "flux1-schnell.safetensors [d06dad7f10]", Hash = "d06dad7f10" }
            };
        }

        public static List<string> GetSampleVAEModels()
        {
            return new List<string>
            {
                "vae-ft-mse-840000-ema-pruned.safetensors",
                "sdxl_vae.safetensors",
                "Automatic"
            };
        }

        public static List<string> GetSampleClipModels()
        {
            return new List<string>
            {
                "clip_l.safetensors",
                "t5xxl_fp8_e4m3fn.safetensors"
            };
        }

        public static List<string> GetSampleClipVisionModels()
        {
            return new List<string>
            {
                "clip_vision_g.safetensors",
                "clip_vision_h.safetensors"
            };
        }

        public static List<string> GetSampleADetailerModels()
        {
            return new List<string>
            {
                "bbox/face_yolov8n.pt",
                "bbox/hand_yolov8n.pt",
                "bbox/person_yolov8n-seg.pt"
            };
        }

        public static Workflow GetSampleCheckpointWorkflow()
        {
            return new Workflow
            {
                Id = Guid.NewGuid(),
                Title = "SDXL Base",
                Base = ModelBase.StableDiffusion,
                Mode = ModeType.Txt2Img,
                Assets = new List<WorkflowAsset>
                {
                    new WorkflowAsset
                    {
                        Parameter = "Model",
                        Type = AssetType.CheckpointModel,
                        DefaultValue = "sd_xl_base_1.0.safetensors",
                        Order = 0
                    },
                    new WorkflowAsset
                    {
                        Parameter = "Vae",
                        Type = AssetType.Vae,
                        DefaultValue = "sdxl_vae.safetensors",
                        Order = 1
                    }
                }
            };
        }

        public static Workflow GetSampleDiffusionWorkflow()
        {
            return new Workflow
            {
                Id = Guid.NewGuid(),
                Title = "Flux Dev",
                Base = ModelBase.Flux,
                Mode = ModeType.Txt2Img,
                Assets = new List<WorkflowAsset>
                {
                    new WorkflowAsset
                    {
                        Parameter = "Model",
                        Type = AssetType.DiffusionModel,
                        DefaultValue = "flux1-dev.safetensors",
                        Order = 0
                    },
                    new WorkflowAsset
                    {
                        Parameter = "Clip",
                        Type = AssetType.Clip,
                        DefaultValue = "clip_l.safetensors",
                        Order = 1
                    },
                    new WorkflowAsset
                    {
                        Parameter = "Vae",
                        Type = AssetType.Vae,
                        DefaultValue = "ae.safetensors",
                        Order = 2
                    }
                }
            };
        }

        public static Workflow GetWorkflowWithNoAssets()
        {
            return new Workflow
            {
                Id = Guid.NewGuid(),
                Title = "No Assets Workflow",
                Base = ModelBase.StableDiffusion,
                Mode = ModeType.Txt2Img,
                Assets = new List<WorkflowAsset>()
            };
        }

        public static Workflow GetImg2VidWorkflow()
        {
            return new Workflow
            {
                Id = Guid.NewGuid(),
                Title = "Img2Vid Workflow",
                Base = ModelBase.Wan,
                Mode = ModeType.Img2Vid,
                Assets = new List<WorkflowAsset>
                {
                    new WorkflowAsset
                    {
                        Parameter = "HighModel",
                        Type = AssetType.DiffusionModel,
                        DefaultValue = "wan22_high.safetensors",
                        Order = 0
                    },
                    new WorkflowAsset
                    {
                        Parameter = "LowModel",
                        Type = AssetType.DiffusionModel,
                        DefaultValue = "wan22_low.safetensors",
                        Order = 1
                    }
                }
            };
        }

        public static Dictionary<string, string> GetSampleWorkflowAssets()
        {
            return new Dictionary<string, string>
            {
                { "Model", "sd_xl_base_1.0.safetensors" },
                { "Vae", "sdxl_vae.safetensors" }
            };
        }

        public static Dictionary<string, string> GetSampleWorkflowAssetsForImg2Vid()
        {
            return new Dictionary<string, string>
            {
                { "HighModel", "wan22_high.safetensors" },
                { "LowModel", "wan22_low.safetensors" },
                { "Vae", "wan_vae.safetensors" }
            };
        }
    }
}
