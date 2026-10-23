import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

AI = Path(__file__).resolve().parents[2] / "ai"
sys.path.insert(0, str(AI))
import gemini
import bridge


class GeminiContractTests(unittest.TestCase):
    def test_supplied_file_is_unchanged(self):
        self.assertEqual(hashlib.sha256((AI / 'gemini.py').read_bytes()).hexdigest(),
                         'cc6120d2bec42986ec13be5b92655bedcd35bf23ccade0f9a80b29ead8942630')

    def test_actual_request_and_stream_parser(self):
        def frame(text):
            return json.dumps([['wrb.fr', 'rpc', json.dumps([None, None, None, None, [[None, [text]]]])]])
        reply = type('Reply', (), {'status_code': 200, 'headers': {},
                                  'text': ")]}'\n" + frame('Hello') + '\n' + frame('Hello world')})()
        with patch.object(gemini.requests, 'post', return_value=reply) as post:
            self.assertEqual(gemini.ask_raw('Hi'), ('Hello world', 200, None))
            post.assert_called_once_with(gemini.URL, headers={
                'accept': '*/*', 'content-type': 'application/x-www-form-urlencoded;charset=UTF-8',
                'x-same-domain': '1', 'cookie': ''}, data=gemini.build_payload('Hi'), timeout=45)

    def test_retry_and_transport_failure(self):
        reply = type('Reply', (), {'status_code': 429, 'headers': {'Retry-After': '12'}})()
        with patch.object(gemini.requests, 'post', return_value=reply):
            self.assertEqual(gemini.ask_raw('Hi'), ('', 429, 12))
        with patch.object(gemini.requests, 'post', side_effect=gemini.requests.Timeout('timeout')):
            self.assertIsNone(gemini.ask_raw('Hi')[1])

    def test_wrapper_handles_errors_without_mutating_helper(self):
        for result in [('', 200, None), ('[ERROR broken]', 200, None), ('error', None, None), ('', 503, None)]:
            with self.subTest(result=result), patch.object(gemini, 'ask_raw', return_value=result):
                self.assertFalse(bridge.generate('Hi')['ok'])
        with patch.object(gemini, 'ask_raw', side_effect=IndexError()):
            self.assertEqual(bridge.generate('Hi')['error'], 'invalid_provider_response')

    def test_validation_does_not_call_upstream(self):
        with patch.object(gemini, 'ask_raw') as ask:
            for prompt in [None, '', ' ', 'x' * 48_001]:
                self.assertFalse(bridge.generate(prompt)['ok'])
            ask.assert_not_called()


if __name__ == '__main__':
    unittest.main()
