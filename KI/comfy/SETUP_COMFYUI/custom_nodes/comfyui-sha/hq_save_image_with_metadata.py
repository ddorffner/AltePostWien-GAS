import os
import json

from PIL import Image
from PIL.PngImagePlugin import PngInfo

import torch
import numpy as np

from comfy.cli_args import args
import folder_paths
from comfy.utils import PROGRESS_BAR_ENABLED, ProgressBar


class SaveImageDynamic:
    def __init__(self):
        self.type = "output"
        self.compress_level = 4

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "filepath": ("STRING", {"default": "absolute or relative .png path"}),
                "overwrite": ("BOOLEAN", {"default": True}),
            },
            "optional": {
                "image": ("IMAGE",),
                "alpha": ("MASK",),
            },
            "hidden": {
                "prompt": "PROMPT",
                "extra_pnginfo": "EXTRA_PNGINFO",
            },
        }

    RETURN_TYPES = ()
    OUTPUT_NODE = True
    FUNCTION = "save"
    CATEGORY = "HQ-Image-Save"

    def save(self, filepath, overwrite, image=None, alpha=None, prompt=None, extra_pnginfo=None):
        assert image is not None, "image is required"
        out_path = os.path.normpath(filepath)
        if not os.path.isabs(out_path):
            # Fallback: join with ComfyUI output directory
            out_dir = folder_paths.get_output_directory()
            out_path = os.path.normpath(os.path.join(out_dir, out_path))
        base, ext = os.path.splitext(out_path)
        if ext.lower() != ".png":
            raise Exception("filepath must point to a .png file")

        os.makedirs(os.path.dirname(out_path), exist_ok=True)

        # Only save the first image (exact path)
        img_tensor = image[0].float().detach().cpu().numpy()
        # Ensure channel order
        if img_tensor.ndim == 3 and img_tensor.shape[0] in (1,3,4):
            img_tensor = np.transpose(img_tensor, (1,2,0))


        # Sanitize and clamp
        img_tensor = np.nan_to_num(img_tensor, nan=0.0, posinf=1.0, neginf=0.0)
        img_tensor = np.clip(img_tensor, 0.0, 1.0)

        # Apply gamma correction (sRGB)
        # img_tensor = np.power(img_tensor, 1.0 / 2.2)

        # Convert to 8-bit
        img_uint8 = (img_tensor * 255.0 + 0.5).astype(np.uint8)
        pil_img = Image.fromarray(img_uint8)
        #img_uint8 = np.clip(img_tensor * 255.0, 0, 255).astype(np.uint8)
        #pil_img = Image.fromarray(img_uint8)

        if alpha is not None:
            a = alpha[0].detach().cpu().numpy() if alpha.ndim == 3 else alpha.detach().cpu().numpy()
            a_uint8 = np.clip(a * 255.0, 0, 255).astype(np.uint8)
            pil_alpha = Image.fromarray(a_uint8)
            pil_img.putalpha(pil_alpha)

        metadata = None
        if not args.disable_metadata:
            metadata = PngInfo()
            if prompt is not None:
                metadata.add_text("prompt", json.dumps(prompt))
            if extra_pnginfo is not None:
                for k in extra_pnginfo:
                    metadata.add_text(k, json.dumps(extra_pnginfo[k], ensure_ascii=False))

        if os.path.exists(out_path) and not overwrite:
            print(f"[SaveImageDynamic] Skipped existing file: {out_path}")
            return {"ui": {"images": []}}

        try:
            pil_img.save(out_path, pnginfo=metadata, compress_level=self.compress_level)
        except Exception as e:
            print(f"[SaveImageDynamic] Failed to save {out_path}: {e}")
            raise

        return {"ui": {"images": []}}


NODE_CLASS_MAPPINGS = {
    "SaveImageDynamic": SaveImageDynamic
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "SaveImageDynamic": "Save Image Dynamic"
}
