import json
import math

import torch
import torch.nn.functional as F


def _first_rgb_image(image):
    if image is None:
        raise ValueError("image is required")

    if image.dim() == 4:
        image = image[0]

    if image.dim() != 3:
        raise ValueError(
            f"expected IMAGE tensor with 3 or 4 dimensions, got {image.dim()}"
        )

    if image.shape[-1] < 3:
        raise ValueError("expected IMAGE tensor with at least 3 channels")

    return image[..., :3].float().clamp(0.0, 1.0)


def _normalize_vector(vector):
    vector = vector.float()
    vector = vector - vector.mean()
    norm = torch.linalg.vector_norm(vector)
    if not torch.isfinite(norm) or norm.item() <= 0.0:
        return torch.zeros_like(vector)

    return vector / norm


def _to_sized_vector(vector, dimensions):
    if dimensions <= 0:
        raise ValueError("dimensions must be greater than zero")

    if vector.numel() == 0:
        raise ValueError("feature vector is empty")

    if vector.numel() < dimensions:
        repeat_count = math.ceil(dimensions / vector.numel())
        vector = vector.repeat(repeat_count)

    return vector[:dimensions].contiguous()


def _json_result(payload):
    text = json.dumps(payload, separators=(",", ":"))
    return {"ui": {"text": [text]}, "result": (text,)}


class BlazorCleanupImageEmbedding:
    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "model_key": (
                    ["blazor_stats_v1_512"],
                    {"default": "blazor_stats_v1_512"},
                ),
                "dimensions": (
                    "INT",
                    {"default": 512, "min": 16, "max": 2048, "step": 16},
                ),
                "normalize": ("BOOLEAN", {"default": True}),
            }
        }

    RETURN_TYPES = ("STRING",)
    RETURN_NAMES = ("json",)
    FUNCTION = "embed"
    OUTPUT_NODE = True
    CATEGORY = "Blazor/Cleanup"

    def embed(
        self, image, model_key="blazor_stats_v1_512", dimensions=512, normalize=True
    ):
        rgb = _first_rgb_image(image)
        chw = rgb.permute(2, 0, 1).unsqueeze(0)

        features = []
        for size in (16, 8, 4):
            sample = F.interpolate(
                chw, size=(size, size), mode="bilinear", align_corners=False
            )
            features.append(sample.flatten())

        features.append(rgb.mean(dim=(0, 1)))
        features.append(rgb.std(dim=(0, 1), unbiased=False))

        vector = torch.cat(features)
        vector = _to_sized_vector(vector, int(dimensions))
        if normalize:
            vector = _normalize_vector(vector)

        payload = {
            "modelKey": str(model_key),
            "dimensions": int(dimensions),
            "embedding": [float(value) for value in vector.detach().cpu().tolist()],
        }
        return _json_result(payload)


class BlazorCleanupImageScore:
    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "model_key": (["blazor_quality_v1"], {"default": "blazor_quality_v1"}),
                "score_name": ("STRING", {"default": "quality"}),
                "min_score": (
                    "FLOAT",
                    {"default": 0.0, "min": -100.0, "max": 100.0, "step": 0.01},
                ),
                "max_score": (
                    "FLOAT",
                    {"default": 1.0, "min": -100.0, "max": 100.0, "step": 0.01},
                ),
            }
        }

    RETURN_TYPES = ("STRING",)
    RETURN_NAMES = ("json",)
    FUNCTION = "score"
    OUTPUT_NODE = True
    CATEGORY = "Blazor/Cleanup"

    def score(
        self,
        image,
        model_key="blazor_quality_v1",
        score_name="quality",
        min_score=0.0,
        max_score=1.0,
    ):
        rgb = _first_rgb_image(image)
        gray = (rgb[..., 0] * 0.299) + (rgb[..., 1] * 0.587) + (rgb[..., 2] * 0.114)

        contrast = torch.clamp(gray.std(unbiased=False) * 3.0, 0.0, 1.0)
        exposure = torch.clamp(1.0 - torch.abs(gray.mean() - 0.5) * 2.0, 0.0, 1.0)

        dx = (
            torch.abs(gray[:, 1:] - gray[:, :-1]).mean()
            if gray.shape[1] > 1
            else torch.tensor(0.0, device=gray.device)
        )
        dy = (
            torch.abs(gray[1:, :] - gray[:-1, :]).mean()
            if gray.shape[0] > 1
            else torch.tensor(0.0, device=gray.device)
        )
        sharpness = torch.clamp((dx + dy) * 8.0, 0.0, 1.0)

        saturation = torch.clamp(rgb.std(dim=-1, unbiased=False).mean() * 4.0, 0.0, 1.0)
        raw_score = (
            (contrast * 0.30)
            + (exposure * 0.25)
            + (sharpness * 0.30)
            + (saturation * 0.15)
        )

        min_score = float(min_score)
        max_score = float(max_score)
        if max_score <= min_score:
            raise ValueError("max_score must be greater than min_score")

        score_value = min_score + (
            float(raw_score.detach().cpu()) * (max_score - min_score)
        )
        payload = {
            "modelKey": str(model_key),
            "scoreName": str(score_name),
            "score": score_value,
            "minScore": min_score,
            "maxScore": max_score,
        }
        return _json_result(payload)


NODE_CLASS_MAPPINGS = {
    "BlazorCleanupImageEmbedding": BlazorCleanupImageEmbedding,
    "BlazorCleanupImageScore": BlazorCleanupImageScore,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "BlazorCleanupImageEmbedding": "Blazor Cleanup Image Embedding",
    "BlazorCleanupImageScore": "Blazor Cleanup Image Score",
}
