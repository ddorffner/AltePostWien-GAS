conda activate post


pip install -U "transformers==4.46.3" "tokenizers==0.20.3" "huggingface-hub==0.35.1" "accelerate>=1.10" "safetensors>=0.4.3"


set PKGPATH=%CONDA_PREFIX%\Lib\site-packages
rmdir /s /q "%PKGPATH%\huggingface_hub"
rmdir /s /q "%PKGPATH%\huggingface_hub-0.34.4.dist-info"
rmdir /s /q "%PKGPATH%\huggingface_hub-0.35.1.dist-info"
pip install --no-cache-dir --force-reinstall --no-deps huggingface-hub==0.35.1


python -c "import huggingface_hub, transformers, tokenizers; print(huggingface_hub.__version__); print(transformers.__version__); print(tokenizers.__version__)"


delete florence models in:
C:\Users\%USERNAME%\.cache\huggingface\hub\

will be redownloaded