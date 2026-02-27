import torch


class CropImagePercentage:
    """Crop an image tensor by percentage values on each edge."""

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "left": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 100.0, "step": 0.1}),
                "right": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 100.0, "step": 0.1}),
                "top": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 100.0, "step": 0.1}),
                "bottom": ("FLOAT", {"default": 0.0, "min": 0.0, "max": 100.0, "step": 0.1}),
            },
        }

    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "crop"
    CATEGORY = "image"

    def crop(self, image, left, right, top, bottom):
        if image is None:
            raise ValueError("image input is required")

        if not torch.isfinite(torch.tensor([left, right, top, bottom])).all():
            raise ValueError("Crop percentages must be finite numbers")

        batch, height, width, channels = image.shape

        left_px = self._percentage_to_pixels(width, left)
        right_px = self._percentage_to_pixels(width, right)
        top_px = self._percentage_to_pixels(height, top)
        bottom_px = self._percentage_to_pixels(height, bottom)

        if left_px + right_px >= width:
            raise ValueError("Left and right crops remove the entire image width")
        if top_px + bottom_px >= height:
            raise ValueError("Top and bottom crops remove the entire image height")

        x_start = left_px
        x_end = width - right_px
        y_start = top_px
        y_end = height - bottom_px

        if x_start >= x_end or y_start >= y_end:
            raise ValueError("Computed crop bounds are invalid")

        cropped = image[:, y_start:y_end, x_start:x_end, :]
        return (cropped,)

    @staticmethod
    def _percentage_to_pixels(size, percentage):
        percentage = max(0.0, min(float(percentage), 100.0))
        return int(round(size * (percentage / 100.0)))


NODE_CLASS_MAPPINGS = {
    "CropImagePercentage": CropImagePercentage,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "CropImagePercentage": "Crop Image Percentage",
}

