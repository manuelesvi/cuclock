
from huggingface_hub import hf_hub_download

hf_hub_download(repo_id="sentence-transformers/all-MiniLM-L6-v2", filename="tokenizer.json")

print("Downloaded tokenizer.json from Hugging Face Hub to ~/.cache/huggingface/hub")