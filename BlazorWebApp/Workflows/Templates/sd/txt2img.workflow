{
  "Title": "Txt2Img",
  "Base": "StableDiffusion",
  "Mode": "txt2img",
  "Assets": [
    { "parameter": "Model", "label": "Model", "type": "CheckpointModel", "default": "Base/v1-5-pruned-emaonly.safetensors", "order": 1, "columnSize": 6 }
  ],
  "Pipeline": [
    {
      "id": "loader_sd",
      "fragment": "load-checkpoint.liquid",
      "parameters": {
        "loader_id": "model_loader",
        "ckpt_name": {{ Model | json }},
        "prompt": {{ positive | json }},
        "negative": {{ negative | json }}
      }
    },
    {
      "id": "latent",
      "fragment": "empty-latent.liquid",
      "parameters": {
        "width": {{ width | default: 512 | json }},
        "height": {{ height | default: 768 | json }},
        "batch_size": {{ batch_size | default: 1 | json }},
        "latent_class": "EmptyLatentImage"
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
        "cfg": {{ cfg | default: 5.5 | json }},
        "seed": {{ seed | default: 42 | json }}
       }
     },
     {
      "id": "upscale",
      "fragment": "upscale.liquid",
      "parameters": {
        "upscale_model": {{ upscale_model | default: "4x-UltraSharpV2.safetensors" | json }},
        "upscale_width": {{ upscale_width | default: 1024 | json }},
        "upscale_height": {{ upscale_height | default: 1536 | json }},
        "upscale_steps": {{ upscale_steps | default: 20 | json }},
        "upscale_denoise": {{ upscale_denoise | default: 1 | json }},
        "sampler_name": {{ sampler_name | default: "multistep/res_2m" | json }},
        "scheduler": {{ scheduler | default: "beta" | json }},
        "seed": {{ seed | default: 42 | json }},
        "cfg": {{ cfg | default: 5.5 | json }}
      }
    },
    {
      "id": "vae_decode",
      "fragment": "vae-decode.liquid",
      "parameters": {}
    },
    {
      "id": "seed_vr2",
      "fragment": "upscale-seedvr2.liquid",
      "parameters": {
        "seedvr2_model": {{ seed_vr2_model | default: "seedvr2_ema_7b-Q4_K_M.gguf" | json }},
        "seedvr2_vae_model": {{ seed_vr2_vae_model | default: "ema_vae_fp16.safetensors" | json }},
        "seedvr2_seed": {{ seed | default: 42 | json }},
        "seedvr2_resolution": {{ seed_vr2_resolution | default: 2048 | json }},
        "seedvr2_batch_size": {{ seed_vr2_batch_size | default: 1 | json }},
        "seedvr2_input_noise_scale": {{ seed_vr2_input_noise_scale | default: 0.0 | json }},
        "seedvr2_latent_noise_scale": {{ seed_vr2_latent_noise_scale | default: 0.0 | json }},
        "blocks_to_swap": {{ seed_vr2_blocks_to_swap | default: 36 | json }},
        "vae_tile_size": {{ seed_vr2_vae_tile_size | default: 1024 | json }},
        "vae_tile_overlap": {{ seed_vr2_vae_tile_overlap | default: 128 | json }}
      }
    },
    {
      "id": "loader_detailer",
      "fragment": "load-checkpoint.liquid",
      "parameters": {
        "scope": "detailer_",
        "scope_title": "Detailer ",
        "loader_id": "model_loader",
        "latent_id": "latent_image",
        "ckpt_name": {{ detailer_checkpoint | default: Model | json }},
        "prompt": {{ detailer_prompt | default: positive | json }},
        "negative": {{ detailer_negative_prompt | default: negative | json }},
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
        "detailer_cfg": {{ detailer_cfg | default: 8 | json }},
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
