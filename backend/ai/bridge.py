"""Private JSON-lines adapter. The supplied gemini.py stays byte-for-byte unchanged."""
import contextlib
import json
import sys

import gemini

MAX_PROMPT_CHARS = 48_000


def generate(prompt: str) -> dict:
    if not isinstance(prompt, str) or not prompt.strip() or len(prompt) > MAX_PROMPT_CHARS:
        return {"ok": False, "error": "invalid_prompt", "retryable": False}
    try:
        with contextlib.redirect_stdout(sys.stderr):
            answer, status, retry_after = gemini.ask_raw(prompt)
    except Exception:
        return {"ok": False, "error": "invalid_provider_response", "retryable": False}
    if status != 200:
        return {
            "ok": False,
            "error": "provider_unavailable",
            "retryable": status is None or status == 429 or status >= 500,
            "status": status,
            "retryAfter": retry_after,
        }
    if not answer.strip() or answer.lstrip().startswith("[ERROR"):
        return {"ok": False, "error": "empty_provider_response", "retryable": True}
    return {"ok": True, "text": answer}


def dispatch(request):
    if not isinstance(request, dict):
        return {"ok": False, "error": "invalid_request", "retryable": False}
    operation = request.get('operation', 'generate')
    if operation == 'generate':
        return generate(request.get('prompt'))
    try:
        with contextlib.redirect_stdout(sys.stderr):
            if operation == 'embed':
                from embeddings import embed_query
                return embed_query(request.get('text'))
            if operation == 'index':
                from extraction import index_pdf
                return index_pdf(request['path'])
    except Exception:
        return {"ok": False, "error": "document_or_embedding_failed", "retryable": False}
    return {"ok": False, "error": "invalid_operation", "retryable": False}


def main() -> None:
    # Read a bounded line, not an unlimited stream controlled by the caller.
    while line := sys.stdin.buffer.readline(256_001):
        if len(line) > 256_000:
            print(json.dumps({"ok": False, "error": "request_too_large", "retryable": False}), flush=True)
            return
        try:
            request = json.loads(line)
            response = dispatch(request)
        except (ValueError, TypeError):
            response = {"ok": False, "error": "invalid_request", "retryable": False}
        print(json.dumps(response, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
