{
  "Title": "Txt2Img",
  "Base": "Flux",
  "Mode": "txt2img",
  "Assets": [
    { "parameter": "Model", "label": "Model", "type": "DiffusionModel", "default": "flux1-krea-dev_fp8_scaled.safetensors", "order": 1, "columnSize": 3 },
    { "parameter": "Clip1", "label": "CLIP T5", "type": "Clip", "default": "t5xxl_fp8_e4m3fn_scaled.safetensors", "order": 2, "columnSize": 3 },
    { "parameter": "Clip2", "label": "CLIP ViT", "type": "Clip", "default": "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors", "order": 3, "columnSize": 3 },
    { "parameter": "VAE", "label": "VAE", "type": "Vae", "default": "ae.safetensors", "order": 4, "columnSize": 3 }
  ],
  "Pipeline": [
    {
      "id": "loader_flux",
      "fragment": "flux/load-flux.liquid",
      "parameters": {
        "unet_name": {{ Model | json }},
        "clip_name1": {{ Clip1 | json }},
        "clip_name2": {{ Clip2 | json }},
        "vae_name": {{ VAE | json }},
        "positive": {{ positive | json }},
        "guidance": {{ guidance | default: 3.5 | json }},
        "reflux_enabled": true,
        "scaling": "exponential",
        "max_shift": 1.35,
        "base_shift": 0.85,
        "width": {{ width | default: 872 | json }},
        "height": {{ height | default: 1248 | json }},
        "batch_size": {{ batch_size | default: 1 | json }}
      }
    },
    {
      "id": "main_sampler",
      "fragment": "sampler.liquid",
      "parameters": {
        "title": "Main Sampler",
        "sampler_id": "sampler_main",
        "sampler_name": {{ sampler_name | default: "multistep/res_2m" | json }},
        "scheduler": {{ scheduler | default: "beta" | json }},
        "steps": {{ steps | default: 20 | json }},
        "cfg": 1,
        "seed": {{ seed | default: 42 | json }}
      }
    },
    {
      "id": "upscale",
      "fragment": "upscale.liquid",
      "parameters": {
        "upscale_model": {{ upscale_model | default: "4x-UltraSharpV2.safetensors" | json }},
        "upscale_width": {{ upscale_width | default: 1744 | json }},
        "upscale_height": {{ upscale_height | default: 2496 | json }},
        "upscale_steps": {{ upscale_steps | default: 20 | json }},
        "upscale_denoise": {{ upscale_denoise | default: 1 | json }},
        "sampler_name": {{ sampler_name | default: "multistep/res_2m" | json }},
        "scheduler": {{ scheduler | default: "beta" | json }},
        "seed": {{ seed | default: 42 | json }},
        "cfg": 1
      }
    },
    {
      "id": "vae_decode",
      "fragment": "vae-decode.liquid",
      "parameters": {}
    },
    {
      "id": "loader_detailer",
      "fragment": "flux/load-flux.liquid",
      "parameters": {
        "scope": "detailer_",
        "scope_title": "Detailer ",
        "unet_name": {{ detailer_checkpoint | default: Model | default: "flux1-krea-dev_fp8_scaled.safetensors" | json }},
        "clip_name1": {{ Clip1 | default: "t5xxl_fp8_e4m3fn_scaled.safetensors" | json }},
        "clip_name2": {{ Clip2 | default: "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors" | json }},
        "vae_name": {{ VAE | default: "ae.safetensors" | json }},
        "positive": {{ detailer_prompt | default: positive | json }},
        "guidance": {{ guidance | default: 3.5 | json }},
        "reflux_enabled": true,
        "scaling": "exponential",
        "max_shift": 1.35,
        "base_shift": 0.85,
        "width": {{ width | default: 872 | json }},
        "height": {{ height | default: 1248 | json }},
        "batch_size": {{ batch_size | default: 1 | json }}
      }
    },
    {
      "id": "detailer",
      "fragment": "detailer-core.liquid",
      "parameters": {
        "scope": "detailer_",
        "detailer_detection_model": {{ detailer_model | default: "bbox/face_yolov8m.pt" | json }},
        "detailer_sampler": {{ detailer_sampler | default: "dpmpp_2m" | json }},
        "detailer_scheduler": {{ detailer_scheduler | default: "beta" | json }},
        "detailer_seed": {{ detailer_seed | default: seed | default: 42 | json }},
        "detailer_steps": {{ detailer_steps | default: 20 | json }},
        "detailer_cfg": {{ detailer_cfg | default: 1 | json }},
        "detailer_denoise": {{ detailer_denoise | default: 0.65 | json }},
        "detailer_feather": {{ detailer_feather | default: 5 | json }},
        "detailer_bbox_threshold": {{ detailer_bbox_threshold | default: 0.7 | json }},
        "detailer_bbox_dilation": {{ detailer_bbox_dilation | default: 10 | json }},
        "detailer_bbox_crop_factor": {{ detailer_bbox_crop_factor | default: 3 | json }},
        "detailer_drop_size": {{ detailer_drop_size | default: 70 | json }},
        "detailer_guide_size": {{ detailer_guide_size | default: 512 | json }},
        "detailer_max_size": {{ detailer_max_size | default: 1024 | json }},
        "detailer_cycle": {{ detailer_cycle | default: 1 | json }}
      }
    },
    {
      "id": "save",
      "fragment": "save.liquid",
      "parameters": {}
    }
  ]
}
