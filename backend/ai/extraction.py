"""Page-preserving PDF extraction and indexing; originals are never executed."""
from pathlib import Path
import unicodedata

from embeddings import MODEL_ID, get_model, page_chunks


def index_pdf(path):
    from pypdf import PdfReader
    from tokenizers import Tokenizer
    model = get_model()
    # Clone the pinned model's tokenizer so disabling truncation cannot alter embedding inference.
    tokenizer = Tokenizer.from_str(model.model.tokenizer.to_str())
    tokenizer.no_truncation()
    tokenizer.no_padding()
    reader = PdfReader(Path(path))
    if reader.is_encrypted or len(reader.pages) > 500:
        raise ValueError('unsupported_pdf')
    chunks = []
    pages_with_text = 0
    for page_number, page in enumerate(reader.pages, 1):
        text = unicodedata.normalize('NFC', (page.extract_text() or '').replace('\x00', ''))
        if text.strip():
            pages_with_text += 1
        for content in page_chunks(text, tokenizer):
            chunks.append({'page': page_number, 'text': content})
    if len(chunks) > 10_000:
        raise ValueError('document_too_large')
    if chunks:
        vectors = model.passage_embed([chunk['text'] for chunk in chunks], batch_size=8)
        for chunk, vector in zip(chunks, vectors, strict=True):
            chunk['vector'] = vector.tolist()
    return {'ok': True, 'model': MODEL_ID, 'pages': len(reader.pages), 'pagesWithText': pages_with_text, 'chunks': chunks}
