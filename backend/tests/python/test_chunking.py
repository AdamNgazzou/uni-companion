import re
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'ai'))
from embeddings import page_chunks
from bridge import dispatch
from unittest.mock import MagicMock, patch
from types import SimpleNamespace
import embeddings


class TokenizerFixture:
    def encode(self, text, add_special_tokens=False):
        return type('Encoded', (), {'offsets': [(m.start(), m.end()) for m in re.finditer(r'\S+', text)]})()


class ChunkingTests(unittest.TestCase):
    def test_chunking_preserves_unicode_original_text_and_overlap(self):
        text = ' '.join(f'apprentissage{i}' for i in range(230))
        chunks = list(page_chunks(text, TokenizerFixture()))
        self.assertEqual([110, 110, 50], [len(c.split()) for c in chunks])
        self.assertEqual(chunks[0].split()[-20:], chunks[1].split()[:20])
        self.assertTrue(all(c in text for c in chunks))
        self.assertEqual(chunks[-1].split()[-1], 'apprentissage229')

    def test_empty_page_does_not_create_a_chunk(self):
        self.assertEqual([], list(page_chunks('  ', TokenizerFixture())))

    def test_overlong_query_is_rejected_before_silent_model_truncation(self):
        tokenizer = MagicMock()
        tokenizer.encode.return_value.ids = list(range(129))
        tokenizer_class = SimpleNamespace(from_str=lambda _: tokenizer)
        model = MagicMock()
        with patch.object(embeddings, 'get_model', return_value=model), patch.dict(sys.modules, {'tokenizers': SimpleNamespace(Tokenizer=tokenizer_class)}):
            result = embeddings.embed_query('A question with too many model tokens')
        self.assertEqual('query_too_long', result['error'])
        model.query_embed.assert_not_called()

    def test_unknown_bridge_operation_is_rejected(self):
        self.assertFalse(dispatch({'operation': 'execute_python', 'path': '/tmp/file.py'})['ok'])
        self.assertFalse(dispatch([])['ok'])


if __name__ == '__main__':
    unittest.main()
