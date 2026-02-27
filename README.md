# AltePostWien
Please ensure any asset file like 3d models, images movies is tracked with git lfs

## Installation

### ML Server

download and install anaconda https://www.anaconda.com/

open anaconda terminal and type:

```cmd
conda create -n post python=3.12
conda activate post
cd /path/to/tools
mkdir Comfy
cd Comfy
git clone https://github.com/comfyanonymous/ComfyUI.git
cd ComfyUI 
pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu121
pip install -r requirements.txt
python main.py

```

with these commands you:

-  create the virtual environment "post"
-  activate the environment
-  change to your tools directory where comfyui should be installed
-  create top level directory and change into it (you can skip this step if you want)
-  clone git repo
-  change to repo
-  ínstall dependencies
-  run main.py

now generate an image at least once then close the server (ctrl + c)

```cmd
cd ComfyUI/custom_nodes
git clone https://github.com/ltdrdata/ComfyUI-Manager.git
```

Now the server is fully installed and TBD missing red nodes can be installed via the ComfyUI manager.

Start the server with the --listen arg and do not forget to activate the environment first.

## ML Models:

following model files must be downloaded and put in the corresponding folder:

### checkpoints
~~Turbo:
https://huggingface.co/stabilityai/sdxl-turbo/blob/main/sd_xl_turbo_1.0_fp16.safetensors~~ (deprecated)

Juggernaut:
https://huggingface.co/RunDiffusion/Juggernaut-XL-v9/tree/main

ICBINP:
https://civitai.com/models/229002/icbinp-xl

You can also use a SDXL checkpoint of your choice. 

### control/lora/clip/ipa/upscale

- `/ComfyUI/models/controlnet`
    - [TTPLANET_Controlnet_Tile_realistic_v2_fp16.safetensors](https://huggingface.co/TTPlanet/TTPLanet_SDXL_Controlnet_Tile_Realistic/resolve/main/TTPLANET_Controlnet_Tile_realistic_v2_fp16.safetensors),
    - [TTPLANET_Controlnet_Tile_realistic_v2_rank256.safetensors](https://huggingface.co/TTPlanet/TTPLanet_SDXL_Controlnet_Tile_Realistic/resolve/main/TTPLANET_Controlnet_Tile_realistic_v2_rank256.safetensors),
    - [control-lora-canny-rank256.safetensors](https://huggingface.co/stabilityai/control-lora/resolve/main/control-LoRAs-rank256/control-lora-canny-rank256.safetensors),
    - [control-lora-depth-rank256.safetensors](https://huggingface.co/stabilityai/control-lora/resolve/main/control-LoRAs-rank256/control-lora-depth-rank256.safetensors),
    - [control-lora-sketch-rank256.safetensors](https://huggingface.co/stabilityai/control-lora/resolve/main/control-LoRAs-rank256/control-lora-sketch-rank256.safetensors),
    - [qrcode_monster_sdxl.safetensors](https://huggingface.co/monster-labs/control_v1p_sdxl_qrcode_monster/blob/main/diffusion_pytorch_model.safetensors), download and rename
- `/ComfyUI/models/loras`
    - [LCM_LoRA_Weights_SDXL.safetensors](https://huggingface.co/latent-consistency/lcm-lora-sdxl/resolve/main/pytorch_lora_weights.safetensors), download and rename
- `/ComfyUI/models/clip_vision`
    - [CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors](https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors), download and rename
    - [CLIP-ViT-bigG-14-laion2B-39B-b160k.safetensors](https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/image_encoder/model.safetensors), download and rename
- `/ComfyUI/models/ipadapter`, create it if not present
    - [ip-adapter_sdxl_vit-h.safetensors](https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl_vit-h.safetensors), 
    - [ip-adapter-plus_sdxl_vit-h.safetensors](https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors), 
    - [ip-adapter_sdxl.safetensors](https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter_sdxl.safetensors),
- `/ComfyUI/models/upscale_models`
    - [realesrgan-x4plus](https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth),
    - [realesrgan-x2plus](https://huggingface.co/nateraw/real-esrgan/resolve/main/RealESRGAN_x2plus.pth)

## vvvv

### JSON Preparation

In JSON Api file saved from ComfyUI you need to implement variables that are interpreted in VVVV (status now, will maybe be changed to something more modular)

Following Variables need to be replaced in the JSON Api file:

Set Seeds > *=Seed=*

Set Prompt > *=Prompt=*

Set Path ControlNet 01 > *=PathControlNet01=*

Set Path ControlNet 02 > *=PathControlNet02=*

Set Strength ControlNet  01 > *=StrengthCN01=*

Set Strength ControlNet 02 > *=StrengthCN02=*

### SketchApp Setup -> root/SketchApp

all .vl Files which are loaded as Dependencies are saved in "SketchApp/vl" subfolder
in "SketchApp/xml" find StartPathes-01.xml. Before running SketchApp.vl define here the pathes for:
1. the comfyui root folder
2. the path to the workflow json which is loaded (currently: workflows/turbo_2cn_us_4096_api-VVVV-01.json, workflows/turbo_2cn_1024_api-VVVV-01.json is working well for me (Tobi))

### SketchApp UI

<img width="448" alt="image" src="https://github.com/ddorffner/AltePostWien/assets/37126448/43a009b8-d891-49b3-98f3-60320db1aadd">


**Button "Start Transition" (Boolean)** -> Runs a Transition between Scene A & B automatically 

**Filter Time (Float32)** -> Time the Transition takes to fade between the values 0 - 1 (0 = Scene A / 1 = Scene B)

**GlobalFade (Float32)** -> Fade the Transition by hand between Scene A & B

**In Transition (Integer)** -> is 1 when Transition is running


**Button "Save CN & Generate Scene A" (Boolean)** -> Saves the current Noise Texture in VVVV as Control Net image, starts the ai generation process and loads all images for Scene A. Circle next to Button is color code for process state: White = nothing happening, Red = in generation process, Green = Image saved, generation done

**Path Image A (String)** -> Path to the newly generated ai image for Scene A

**Path CN Image A (String)** -> Path to the Control Net image, which was used for ai generation

**StateActive Scene A (Integer)** -> gives information if whether the Scene A is active or not, meaning if it's currently shown in the renderer (0 = not active, 1 = active)


**Button "Save CN & Generate Scene B" (Boolean**) -> Saves the current Noise Texture in VVVV as Control Net image, starts the ai generation process and loads all images for Scene B. Circle next to Button is color code for process state: White = nothing happening, Red = in generation process, Green = Image saved, generation done

**Path Image B (String)** -> Path to the newly generated ai image for Scene B

**Path CN Image B (String)** -> Path to the Control Net image, which was used for ai generation

**StateActive Scene B (Integer)** -> gives information if whether the Scene A is active or not, meaning if it's currently shown in the renderer (0 = not active, 1 = active)

**Resolution (Vector2)** -> Currently not in use (so forget about it for now)

**Prompt (string)** -> Type the prompt for the ai generation here (limit to 600 digits)

**ControlNet01 (Spread<String>)** -> Choose the Control Net image which is used for the ai generation. Is used for Scene A and B. So if you want to change for each Scene, select the one to use befor pressing the generate Buttons

**Strength CN 01 (Float32)** -> Defines the intesity of the Control Net 01 (global value) 

**ControlNet02 (Spread<String>)** -> Choose from all available Control Nets 02, which were saved from VVVV. This is meant to be used for scene independent ai image generation which can be done through the button below

**Strength CN 02 (Float32)** -> Defines the intesity of the Control Net 02 (global value) 

**Button "Save ControlNet" (Boolean**) -> Saves the current Noise Texture in VVVV as Control Net image

**Button "Start Generator" (Boolean)** -> Starts an generation process with the Control Nets selected in "ControlNet01" and "ControlNet02". the output image path and ControlNet02 path will be loaded in the pathes of the scene which is currently NOT active


TBD

##Redis 

###Installation

https://redis.io/kb/doc/1hcec8xg9w/how-can-i-install-redis-on-docker

https://learn.microsoft.com/en-us/windows/wsl/install

https://redis.io/docs/latest/operate/oss_and_stack/install/install-redis/install-redis-on-windows/


##FLUX links
https://huggingface.co/city96/t5-v1_1-xxl-encoder-gguf
-> models\clip

https://huggingface.co/ffxvs/vae-flux/resolve/main/ae.safetensors
-> models\vae

https://huggingface.co/InstantX/FLUX.1-dev-IP-Adapter/tree/main
-> models\ipadapter-flux

https://huggingface.co/jasperai/Flux.1-dev-Controlnet-Upscaler/tree/main
-> models\controlnet
rename: jasper-flux-dev-controlnet-upscaler.safetensors

https://huggingface.co/XLabs-AI/flux-controlnet-collections/tree/main
-> models\controlnet
rename: flux-depth-controlnet-v3.safetensors




