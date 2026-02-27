ComfyUI SHA Save Image (workflow-embedded PNGs)

Nodes

- Save Image Dynamic: Save a single PNG to an absolute path. Embeds ComfyUI workflow (prompt + EXTRA_PNGINFO) in PNG metadata. Supports overwrite flag.

Install

1. Put this folder under ComfyUI/custom_nodes/
2. Restart ComfyUI or click Reload custom nodes

Search for

- Display name above, or ID: SaveImageDynamic

Notes

- Metadata writes use PIL PngInfo like the core Save Image node (prompt + each extra_pnginfo key as JSON).
- Overwriting is controlled by the `overwrite` boolean.
