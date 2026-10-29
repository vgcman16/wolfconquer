"""Exact combined formatting tokens; no literal-percent or unknown-syntax fallback."""
import re

PRINTF = re.compile(r"%(?:[0-9]+\$)?[-+ #0']*(?:\*(?:[0-9]+\$)?|[0-9]+)?"
                    r"(?:\.(?:\*(?:[0-9]+\$)?|[0-9]+))?(?:I64|I32|hh|ll|[hljztLw])?[diuoxXfFeEgGaAcCsSpn%]")
ESCAPE = re.compile(r"\\(?:x[0-9A-Fa-f]{2,8}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|[0-7]{1,3}|[^\r\n])")
BRACE = re.compile(r"\{\{|\}\}|\{[^{}\r\n]*\}")
TAG = re.compile(r"<[^<>\r\n]*>")


def signature(value):
    tokens, cursor = [], 0
    while cursor < len(value):
        char = value[cursor]
        grammar = {"%": PRINTF, "\\": ESCAPE, "{": BRACE, "}": BRACE, "<": TAG, ">": TAG}.get(char)
        if grammar is not None:
            match = grammar.match(value, cursor)
            if match is None:
                raise ValueError("namedResourceUnsupportedFormat")
            tokens.append((char, match[0]))
            cursor = match.end()
        else:
            if ord(char) < 32:
                tokens.append(("control", char))
            cursor += 1
    return tuple(tokens)
