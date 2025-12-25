{
  "Title": "Img2Img (Edit)",
  "Base": "Qwen",
  "Mode": "img2img",
  "Assets": [
    { "parameter": "Model", "label": "Model", "type": "DiffusionModel", "default": "qwen_image_edit_2509_fp8_e4m3fn.safetensors", "order": 1, "columnSize": 4 },
    { "parameter": "Clip", "label": "CLIP", "type": "Clip", "default": "qwen_2.5_vl_7b_fp8_scaled.safetensors", "order": 2, "columnSize": 4 },
    { "parameter": "Vae", "label": "VAE", "type": "Vae", "default": "qwen_image_vae.safetensors", "order": 3, "columnSize": 4 }
  ],
  "Sources": [
    { "id": "source_image", "label": "Source Image", "type": "image", "required": true }
  ],
  "Pipeline": [
    {
      "id": "load_image",
      "fragment": "load-image-scaled.liquid",
      "parameters": {
        "image": {{ Image | json }},
        "megapixels": {{ Megapixels | default: 1 | json }},
        "upscale_method": "lanczos"
      }
    },
    {
      "id": "loader_qwen_edit",
      "fragment": "qwen/load-qwen-edit.liquid",
      "parameters": {
        "unet_name": {{ Model | json }},
        "clip_name": {{ Clip | json }},
        "vae_name": {{ Vae | json }},
        "lora_name": {{ LightningLora | default: "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors" | json }},
        "lora_strength": {{ LoraStrength | default: 1 | json }},
        "model_shift": {{ ModelShift | default: 3 | json }},
        "cfg_norm_strength": {{ CfgNormStrength | default: 1 | json }}
      }
    },
    {
      "id": "vae_encode",
      "fragment": "vae-encode.liquid",
      "parameters": {}
    },
    {
      "id": "encode_edit",
      "fragment": "qwen/encode-edit.liquid",
      "parameters": {
        "positive": {{ Prompt | json }},
        "negative": {{ NegativePrompt | default: "" | json }}
      }
    },
    {
      "id": "main_sampler",
      "fragment": "sampler-standard.liquid",
      "parameters": {
        "title": "KSampler",
        "sampler_id": "sampler_main",
        "sampler_name": {{ SamplerName | default: "euler" | json }},
        "scheduler": {{ Scheduler | default: "simple" | json }},
        "steps": {{ Steps | default: 4 | json }},
        "cfg": {{ CfgScale | default: 1 | json }},
        "denoise": {{ Denoise | default: 1 | json }},
        "seed": {{ Seed | default: 42 | json }}
      }
    },
    {
      "id": "vae_decode",
      "fragment": "vae-decode.liquid",
      "parameters": {}
    },
    {
      "id": "save",
      "fragment": "save.liquid",
      "parameters": {}
    }
  ]
}
