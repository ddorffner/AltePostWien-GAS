import torch
import torch.nn.functional as F

from nodes import MAX_RESOLUTION


class PadImagePosition:
    """Pad an image with configurable anchor position and optional mask feathering."""

    POSITIONS = (
        "center",
        "top_left",
        "top_right",
        "top_center",
        "bottom_left",
        "bottom_right",
        "center_left",
        "center_right",
        "bottom_center",
    )

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "pad_width": ("INT", {"default": 0, "min": 0, "max": MAX_RESOLUTION, "step": 1}),
                "pad_height": ("INT", {"default": 0, "min": 0, "max": MAX_RESOLUTION, "step": 1}),
                "position": (cls.POSITIONS, {"default": "center"}),
                "feathering": ("INT", {"default": 0, "min": 0, "max": MAX_RESOLUTION, "step": 1}),
            },
            "optional": {
                "bg_r": ("FLOAT", {"default": 0.5, "min": 0.0, "max": 1.0, "step": 0.01}),
                "bg_g": ("FLOAT", {"default": 0.5, "min": 0.0, "max": 1.0, "step": 0.01}),
                "bg_b": ("FLOAT", {"default": 0.5, "min": 0.0, "max": 1.0, "step": 0.01}),
                "mask": ("MASK",),
            },
        }

    RETURN_TYPES = ("IMAGE", "MASK")
    FUNCTION = "pad"
    CATEGORY = "image"

    def pad(self, image, pad_width, pad_height, position, feathering, bg_r=0.5, bg_g=0.5, bg_b=0.5, mask=None):
        if image is None:
            raise ValueError("image input is required")

        if position not in self.POSITIONS:
            raise ValueError(f"Unsupported position '{position}'")

        batch, height, width, channels = image.shape
        device = image.device
        dtype = image.dtype

        pad_left, pad_right, pad_top, pad_bottom, target_width, target_height = self._compute_offsets(
            width, height, pad_width, pad_height, position
        )

        new_height = target_height
        new_width = target_width

        if new_height <= 0 or new_width <= 0:
            raise ValueError("Padding produced a non-positive image size")

        color_components = torch.tensor(
            [
                self._to_scalar(bg_r, 0.5),
                self._to_scalar(bg_g, 0.5),
                self._to_scalar(bg_b, 0.5),
            ],
            dtype=dtype,
            device=device,
        ).clamp(0.0, 1.0)

        background = torch.full((channels,), 0.5, dtype=dtype, device=device)
        if channels == 1:
            background[0] = color_components.mean()
        else:
            limit = min(3, channels)
            background[:limit] = color_components[:limit]
            if channels >= 4:
                background[3] = 1.0

        new_image = torch.ones(
            (batch, new_height, new_width, channels),
            dtype=dtype,
            device=device,
        )
        new_image.mul_(background.view(1, 1, 1, channels))
        new_image[:, pad_top : pad_top + height, pad_left : pad_left + width, :] = image

        processed_mask = self._prepare_mask(mask, batch, height, width, device, pad_left, pad_right, pad_top, pad_bottom)
        result_mask = self._build_mask(
            processed_mask,
            batch,
            height,
            width,
            pad_left,
            pad_right,
            pad_top,
            pad_bottom,
            feathering,
            device,
        )

        return (new_image, result_mask)

    @staticmethod
    def _compute_offsets(width, height, target_width, target_height, position):
        width = max(int(width), 0)
        height = max(int(height), 0)
        target_width = max(int(target_width), width)
        target_height = max(int(target_height), height)

        extra_width = target_width - width
        extra_height = target_height - height

        if position == "center":
            pad_left = extra_width // 2
            pad_right = extra_width - pad_left
            pad_top = extra_height // 2
            pad_bottom = extra_height - pad_top
        elif position == "top_left":
            pad_left = 0
            pad_right = extra_width
            pad_top = 0
            pad_bottom = extra_height
        elif position == "top_right":
            pad_left = extra_width
            pad_right = 0
            pad_top = 0
            pad_bottom = extra_height
        elif position == "top_center":
            pad_left = extra_width // 2
            pad_right = extra_width - pad_left
            pad_top = 0
            pad_bottom = extra_height
        elif position == "bottom_left":
            pad_left = 0
            pad_right = extra_width
            pad_top = extra_height
            pad_bottom = 0
        elif position == "bottom_right":
            pad_left = extra_width
            pad_right = 0
            pad_top = extra_height
            pad_bottom = 0
        elif position == "center_left":
            pad_left = 0
            pad_right = extra_width
            pad_top = extra_height // 2
            pad_bottom = extra_height - pad_top
        elif position == "center_right":
            pad_left = extra_width
            pad_right = 0
            pad_top = extra_height // 2
            pad_bottom = extra_height - pad_top
        elif position == "bottom_center":
            pad_left = extra_width // 2
            pad_right = extra_width - pad_left
            pad_top = extra_height
            pad_bottom = 0
        else:
            raise ValueError(f"Unsupported position '{position}'")

        return pad_left, pad_right, pad_top, pad_bottom, target_width, target_height

    @staticmethod
    def _prepare_mask(mask, batch, height, width, device, pad_left, pad_right, pad_top, pad_bottom):
        if mask is None:
            return None

        mask = mask.to(device=device, dtype=torch.float32)
        if mask.dim() != 3:
            raise ValueError("mask input must be a 3D tensor (B, H, W)")

        if torch.allclose(mask, torch.zeros_like(mask)):
            print("Warning: The incoming mask is fully black. Handling it as None.")
            return None

        padded_mask = F.pad(mask, (pad_left, pad_right, pad_top, pad_bottom), mode="constant", value=0)
        return padded_mask

    @staticmethod
    def _build_mask(mask, batch, height, width, pad_left, pad_right, pad_top, pad_bottom, feathering, device):
        if mask is not None:
            return mask

        new_height = height + pad_top + pad_bottom
        new_width = width + pad_left + pad_right

        new_mask = torch.ones(
            (batch, new_height, new_width),
            dtype=torch.float32,
            device=device,
        )
        region = torch.zeros(
            (batch, height, width),
            dtype=torch.float32,
            device=device,
        )

        if feathering > 0 and feathering * 2 < height and feathering * 2 < width:
            for i in range(height):
                for j in range(width):
                    dt = i if pad_top != 0 else height
                    db = height - i if pad_bottom != 0 else height
                    dl = j if pad_left != 0 else width
                    dr = width - j if pad_right != 0 else width
                    d = min(dt, db, dl, dr)
                    if d >= feathering:
                        continue
                    v = (feathering - d) / feathering
                    region[:, i, j] = v * v

        new_mask[:, pad_top : pad_top + height, pad_left : pad_left + width] = region
        return new_mask

    @staticmethod
    def _to_scalar(value, default):
        if value is None:
            return default
        if isinstance(value, (int, float)):
            return float(value)
        if isinstance(value, torch.Tensor):
            if value.numel() == 1:
                return float(value.item())
            raise ValueError("Background color inputs must be scalar values")
        return float(value)


NODE_CLASS_MAPPINGS = {
    "PadImagePosition": PadImagePosition,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "PadImagePosition": "Pad Image Position",
}

