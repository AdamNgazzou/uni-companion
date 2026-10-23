import requests  # Send HTTP requests to your chatbot service.
import json  # Convert Python lists to JSON and JSON back to Python.
import urllib.parse  # Encode the request as form data.
import os  # Read the optional debug setting.
from typing import Optional, Tuple  # Describe the values returned by ask_raw.

URL = "https://gemini.google.com/_/BardChatUi/data/assistant.lamda.BardFrontendService/StreamGenerate"  # Keep your supplied endpoint.
REQUEST_TIMEOUT_SECONDS = 45  # Keep your supplied HTTP timeout.

HEADERS = {  # Keep the headers from your supplied service.
    "accept": "*/*",  # Accept any response content type.
    "content-type": "application/x-www-form-urlencoded;charset=UTF-8",  # Describe the encoded form body.
    "x-same-domain": "1",  # Preserve the service's supplied header value.
    "cookie": ""  # Keep the supplied cookie field empty; add no cookie values.
}  # Finish the header dictionary.


def build_payload(prompt):  # Turn a message into the service's request format.
    inner = [  # Preserve the nested protocol structure exactly as supplied.
        [prompt, 0, None, None, None, None, 0],  # Place the prompt in the first protocol field.
        ["en-US"],  # Preserve the supplied language setting.
        ["", "", "", None, None, None, None, None, None, ""],  # Preserve the supplied empty protocol fields.
        "", "", None, [0], 1, None, None, 1, 0,  # Preserve the supplied protocol flags.
        None, None, None, None, None, [[0]], 0  # Preserve the remaining protocol fields.
    ]  # Finish the inner request structure.

    outer = [None, json.dumps(inner)]  # Wrap the inner list as a JSON string.

    return urllib.parse.urlencode({  # Encode the dictionary as an HTTP form body.
        "f.req": json.dumps(outer)  # Use the field name expected by the service.
    }) + "&"  # Keep the supplied trailing ampersand.


def _merge_fragment(accumulated, fragment):  # Combine streamed text without repeating overlapping content.
    if not fragment:  # Check whether the new fragment is empty.
        return accumulated  # Keep the text already collected.

    if not accumulated:  # Check whether this is the first fragment.
        return fragment  # Start the collected answer with this fragment.

    if fragment.startswith(accumulated):  # Detect a snapshot containing all the earlier text.
        return fragment  # Replace the earlier text with the fuller snapshot.
    if accumulated.startswith(fragment):  # Detect a fragment already contained at the start.
        return accumulated  # Avoid adding the same text twice.

    max_overlap = min(len(accumulated), len(fragment))  # Find the largest possible overlap length.
    for size in range(max_overlap, 0, -1):  # Try overlap lengths from largest to smallest.
        if accumulated.endswith(fragment[:size]):  # Compare the old ending with the new beginning.
            return accumulated + fragment[size:]  # Append only the part that is new.

    return accumulated + fragment  # Join the fragments when there is no overlap.


def parse_response(text):  # Extract and merge answer fragments from the service response.
    text = text.replace(")]}'", "")  # Remove the response prefix used by the supplied service.
    merged = ""  # Start with an empty answer.
    fragment_count = 0  # Count fragments for optional debugging.

    for line in text.splitlines():  # Inspect each line in the response.
        if "wrb.fr" not in line:  # Ignore lines without the expected response marker.
            continue  # Move to the next line.

        try:  # Try reading the line as JSON.
            data = json.loads(line)  # Convert the JSON line into Python values.
        except:  # Preserve the supplied parser's behavior for unreadable lines.
            continue  # Skip the line if parsing fails.

        entries = []  # Collect matching response entries from this line.
        if isinstance(data, list):  # Only inspect list-shaped responses.
            if data[0] == "wrb.fr":  # Recognize a single response entry.
                entries = [data]  # Wrap that entry in a list for the loop below.
            else:  # Handle a list containing multiple entries.
                entries = [i for i in data if isinstance(i, list) and i[0] == "wrb.fr"]  # Keep matching entries.

        for entry in entries:  # Process each matching entry.
            try:  # Try extracting the nested answer data.
                inner = json.loads(entry[2])  # Decode the JSON string stored in the third field.

                if isinstance(inner, list) and isinstance(inner[4], list):  # Check the supplied answer location.
                    for c in inner[4]:  # Inspect each answer item at that location.
                        if isinstance(c, list) and isinstance(c[1], list):  # Check the supplied text location.
                            txt = "".join([t for t in c[1] if isinstance(t, str)])  # Join the string parts.
                            if txt:  # Skip empty fragments.
                                merged = _merge_fragment(merged, txt)  # Merge this fragment into the answer.
                                fragment_count += 1  # Record that one more fragment was processed.
            except:  # Preserve the supplied parser's handling of unexpected entry shapes.
                continue  # Skip entries that cannot be read.

    return merged.strip(), fragment_count  # Return the answer and the number of fragments.


def ask(prompt):  # Keep the supplied convenience function for terminal use.
    text, status, _retry_after = ask_raw(prompt)  # Request the answer and its HTTP status.
    if status is not None and status != 200:  # Check for a known unsuccessful HTTP status.
        return f"[ERROR {status}]"  # Show that status as a short error message.
    return text or "[No response]"  # Return the answer or the supplied empty-response message.


def ask_raw(prompt: str) -> Tuple[str, Optional[int], Optional[int]]:  # Return text, HTTP status, and retry delay.
    payload = build_payload(prompt)  # Encode the message for the service.
    try:  # Handle failures while sending the HTTP request.
        res = requests.post(  # Send the same POST request as your supplied file.
            URL,  # Use the supplied service address.
            headers=HEADERS,  # Use the supplied headers with an empty cookie field.
            data=payload,  # Send the encoded form data.
            timeout=REQUEST_TIMEOUT_SECONDS,  # Stop waiting according to the supplied timeout.
        )  # Finish the HTTP request.
    except requests.RequestException as exc:  # Catch connection, timeout, and other request errors.
        return f"[ERROR request_failed: {exc}]", None, None  # Preserve the supplied failure tuple.

    retry_after = None  # Assume no retry delay was provided.
    try:  # Try interpreting the server's retry header.
        ra = res.headers.get("Retry-After")  # Read the optional retry-delay value.
        if ra is not None:  # Check that the header exists.
            retry_after = int(ra)  # Convert a numeric delay into seconds.
    except Exception:  # Handle retry values that are not integers.
        retry_after = None  # Preserve the supplied behavior for unreadable delays.

    if res.status_code != 200:  # Check whether the HTTP request failed.
        return "", res.status_code, retry_after  # Return the status without treating it as an answer.

    parsed, fragment_count = parse_response(res.text)  # Extract the text from a successful response.
    debug_enabled = os.getenv("GEMINI_DEBUG", "").lower() in {"1", "true", "yes", "on"}  # Read the optional debug flag.
    if debug_enabled:  # Print diagnostics only when requested.
        print(f"[debug] stream_fragments={fragment_count}, chars={len(parsed)}")  # Print counts without the conversation text.

    return parsed.strip(), 200, retry_after  # Return the cleaned answer and HTTP metadata.


def main():  # Keep the supplied terminal chat loop for running this file directly.
    print("⚠️ Unofficial Gemini Chatbot (type 'exit' to quit)\n")  # Explain how to stop the terminal chat.

    while True:  # Keep accepting messages until the user quits.
        user_input = input("You: ")  # Read a message from the terminal.

        if user_input.lower() in ["exit", "quit"]:  # Recognize either exit command.
            break  # Stop the loop.

        reply = ask(user_input)  # Ask the supplied service for a response.
        print("Bot:", reply)  # Display the response in the terminal.


if __name__ == "__main__":  # Run the terminal loop only when this file is executed directly.
    main()  # Start the terminal chatbot.
