## SETUP / RESTORE COMFYUI FROM EXISTING KI WORKER PC

### FAST RESTORE: COPY FILES

Only the ComfyUI folder and the conda env is needed.
They should be copied to the exact folders:

Copy from working PC or NAS to:
- C:\ComfyUI 
- C:\Users\SHA.ART\.conda\envs\post
- ensure pytorch with cuda is installed

DONE!

NOTE:
On autostart, robocopy syncs these folders from NAS:
- C:\ComfyUI\custom_nodes
- C:\ComfyUI\models


------------------------------------------------------------------
The rest of this document should only be needed when the FAST RESTORE does not work for some reason.

# RESTORE CONDA ENV

### A: CONDA ENV FROM zip

RESTORE CONDA ENV BACKUP
:: 1) unzip
C:\Users\SHA.ART\.conda\envs\post

:: 2) Fix embedded paths
C:\Users\SHA.ART\.conda\envs\post\Scripts\conda-unpack.exe

:: 3) Test
conda activate post
python --version
pip list

----

This is how the zip was created:

CREATE CONDA ENV BACKUP
:: 1) Ensure conda-pack is available
conda install -n base conda-pack -y

:: 2) Pack the environment (includes pip packages + exact builds)
:: Replace MYENV with your env name
conda pack -n MYENV -o MYENV-win64.zip --format zip
--

### B: CONDA ENV FROM YAML
This should not be needed, but another conda restore option is:

### In anaconda prompt:

Remove existing conda env:
conda remove -n post --all -y

Create conda env from yml file in repo:
C:\Users\SHA.ART\Documents\AltePost\AltePostWien\KI\comfy\SETUP_COMFYUI\post.yml
:: create a new env named "post" from the YAML
conda env create -n post -f "PATH_TO\post.yml"

:: or, if the env already exists and you want to update it to match the YAML
conda env update -n post -f "PATH_TO\post.yml" --prune

conda activate post

---------------------------------------------------------------------------------------
---------------------------------------------------------------------------------------

# INSTALL COMFY UI

## SETUP / RESTORE COMFYUI FROM NOTHING
This is only needed if files can not be copied from existing, working folder of a KI worker PC!

### 1: git clone comfyui into C:\ComfyUI
NOTE: latest master from 2025-09-26 worked.
If future versions make problems, consider checking out a version from this date.
go to C:\COMFYUI
git clone git@github.com:comfyanonymous/ComfyUI.git

### 2: Install Comfy requirements
### 3: copy custom_nodes from zip file in custom_nodes folder into C:\ComfyUI\custom_nodes
In the unlikely case that the custom_nodes folder is missing on all machines and the NAS:
see list of custom_nodes at end of this file. 

### 4: copy models folder from NAS/ComfyUI/models
In the unlikely case that the models folder is missing on all PCs and NAS:
open workflows and then find + download missing models for Flux, Supir, Controlnet, ...



---------------------------------------------------------------------

### Install gpu pytorch
Try to run, only if complaining about missing cuda, do:

pip uninstall -y torch torchvision torchaudio
pip install torch==2.7.1 torchvision==0.22.1 torchaudio==2.7.1 ^
  --index-url https://download.pytorch.org/whl/cu128 --no-cache-dir

### Verify cuda availability
python -c "import torch;print('Torch:',torch.__version__);print('CUDA available:',torch.cuda.is_available());print('CUDA build:',torch.version.cuda);print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else '')"
Should show:
Torch: 2.7.1+cu128
CUDA available: True
CUDA build: 12.8
NVIDIA GeForce RTX 4090

### Open AnacondaPrompt app + install comfyui requirements from inside C:\ComfyUI\
python pip install -r requirements.txt


-----------------------------------------------------------

# CUSTOM nodes
Open the generate + upscale_hof workflow to see what is missing first!

List of custom_nodes - not all are actively used.
marked with # are surely to be needed.

Batch-Condition-ComfyUI
cocotools_io
comfy-plasma
comfyui-custom-scripts
ComfyUI-Depth-Pro #
comfyui-depthanythingv2 #
comfyui-easy-use
comfyui-florence2 #
ComfyUI-GGUF #
ComfyUI-HQ-Image-Save
ComfyUI-Impact-Pack #
ComfyUI-IPAdapter-Flux #
comfyui-kjnodes #
comfyui-logicutils
ComfyUI-Manager
comfyui-preview360panorama
comfyui-pytorch360convert #
ComfyUI-Regex-Runner
comfyui-sha-save-image #
ComfyUI-SUPIR #
comfyui-tooling-nodes
ComfyUI_AdvancedRefluxControl
comfyui_controlnet_aux
comfyui_essentials
ComfyUI_FizzNodes
ComfyUI_Ib_CustomNodes
comfyui_ultimatesdupscale
rgthree-comfy #
tiled_ksampler
was-node-suite-comfyui
x-flux-comfyui