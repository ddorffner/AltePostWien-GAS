from .crop_image_percentage import CropImagePercentage
from .hq_save_image_with_metadata import SaveImageDynamic
from .pad_image_position import PadImagePosition


NODE_CLASS_MAPPINGS = {
    "SaveImageDynamic": SaveImageDynamic,
    "CropImagePercentage": CropImagePercentage,
    "PadImagePosition": PadImagePosition,
}


NODE_DISPLAY_NAME_MAPPINGS = {
    "SaveImageDynamic": "Save Image Dynamic",
    "CropImagePercentage": "Crop Image Percentage",
    "PadImagePosition": "Pad Image Position",
}


__all__ = [
    "NODE_CLASS_MAPPINGS",
    "NODE_DISPLAY_NAME_MAPPINGS",
]


