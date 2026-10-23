"""Small quantized multilingual CPU embeddings. No hosted embedding credentials."""
import os
from pathlib import Path

MODEL = 'sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2'
MODEL_ID = MODEL + '@fastembed-0.9.0'
MODEL_REPOSITORY = 'Qdrant/paraphrase-multilingual-MiniLM-L12-v2-onnx-Q'
MODEL_REVISION = 'faf4aa4225822f3bc6376869cb1164e8e3feedd0'
# Bump MODEL_ID in Python and .NET if changing this immutable model revision.
_model = None


def get_model():
    global _model
    if _model is None:
        from fastembed import TextEmbedding
        cache = os.environ.get('FASTEMBED_CACHE_PATH', str(Path(__file__).resolve().parents[1] / '.local/models'))
        from huggingface_hub import snapshot_download
        os.environ.setdefault('HF_HUB_DISABLE_TELEMETRY', '1')
        location = snapshot_download(repo_id=MODEL_REPOSITORY, revision=MODEL_REVISION, cache_dir=cache,
                                     allow_patterns=['model_optimized.onnx', 'tokenizer.json',
                                                     'tokenizer_config.json', 'config.json', 'special_tokens_map.json'])
        _model = TextEmbedding(model_name=MODEL, cache_dir=cache, specific_model_path=location,
                               threads=1, providers=['CPUExecutionProvider'])
    return _model


def embed_query(text):
    if not isinstance(text, str) or not text.strip() or len(text) > 2000:
        raise ValueError('invalid_query')
    from tokenizers import Tokenizer
    model = get_model()
    tokenizer = Tokenizer.from_str(model.model.tokenizer.to_str())
    tokenizer.no_truncation()
    tokenizer.no_padding()
    if len(tokenizer.encode(text).ids) > 128:
        return {'ok': False, 'error': 'query_too_long', 'retryable': False}
    vector = next(model.query_embed(text)).tolist()
    return {'ok': True, 'model': MODEL_ID, 'vector': vector}


def page_chunks(text, tokenizer, chunk_tokens=110, overlap_tokens=20):
    """Use original character spans, preserving citation text and avoiding truncation."""
    tokens = tokenizer.encode(text, add_special_tokens=False)
    offsets = [(start, end) for start, end in tokens.offsets if end > start]
    for first in range(0, len(offsets), chunk_tokens - overlap_tokens):
        window = offsets[first:first + chunk_tokens]
        chunk = text[window[0][0]:window[-1][1]].strip()
        if chunk:
            yield chunk
        if first + chunk_tokens >= len(offsets):
            break
