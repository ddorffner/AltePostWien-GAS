## Custom Nodes

#### CUSTOM_NODES.zip
The CUSTOM_NODES.zip folder contains all custom_nodes.
Some are not needed anymore but are left for safety and lazyness reasons.
This folder can be used to restore the ComfyUI environment, in case of total loss.
NOTE: The models are not backed up in the repo

#### comfyui-sha-save-image
The folder comfyui-sha-save-image contains a custom Save Image Dynamic node.
Also contained in the zip but duplicated here for visibility.

All images should be saved with this node:
- it can write to arbitrary pathes
- allows to set absolute pathes to store directly in NAS
- allows to set relative path if needed for quick testing
- writes the comfy workflow into the PNG.
This functionality is copied from the core SaveImage node