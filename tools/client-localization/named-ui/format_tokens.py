"""Exact formatting tokens, including bounded fixed numeric percentages."""
import re

PRINTF = re.compile(r"%(?:[0-9]+\$)?[-+ #0']*(?:\*(?:[0-9]+\$)?|[0-9]+)?"
                    r"(?:\.(?:\*(?:[0-9]+\$)?|[0-9]+))?(?:I64|I32|hh|ll|[hljztLw])?[diuoxXfFeEgGaAcCsSpn%]")
ESCAPE = re.compile(r"\\(?:x[0-9A-Fa-f]{2,8}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|[0-7]{1,3}|[^\r\n])")
BRACE = re.compile(r"\{\{|\}\}|\{[^{}\r\n]*\}")
TAG = re.compile(r"<[^<>\r\n]*>")
NUMERIC_PERCENTAGE = re.compile(r"[+-]?[0-9]+(?:\.[0-9]+)?%")
NUMERIC_CONTINUATION = re.compile(r"[A-Za-z0-9_.,%+\-#&]")


def _cjk(char):
    return "\u3400" <= char <= "\u4dbf" or "\u4e00" <= char <= "\u9fff"


def _percentage_end(value, end):
    if end == len(value):
        return True
    char = value[end]
    if char in " \t\r\n,;:!?/|()[]\"'、。，；：！？（）【】":
        return True
    if char == ".":
        return end + 1 == len(value) or value[end + 1] not in "0123456789"
    if _cjk(char):
        return True
    if value.startswith(("<=", ">=", "->"), end):
        return True
    grammar = {"\\": ESCAPE, "{": BRACE, "}": BRACE, "<": TAG}.get(char)
    return grammar is not None and grammar.match(value, end) is not None


def _numeric_percentage(value, cursor, token_end):
    # A completed format token is a boundary, including the n in literal \\n.
    if cursor != token_end and cursor:
        previous = value[cursor - 1]
        identifier_tail = ("x" + previous).isidentifier() and not _cjk(previous)
        if NUMERIC_CONTINUATION.fullmatch(previous) or identifier_tail:
            return None
    match = NUMERIC_PERCENTAGE.match(value, cursor)
    if match is None or PRINTF.match(value, match.end() - 1) is not None:
        return None
    return match if _percentage_end(value, match.end()) else None


def signature(value):
    tokens, cursor, token_end = [], 0, 0
    while cursor < len(value):
        if value.startswith(("<=", ">="), cursor):
            tokens.append(("comparison", value[cursor:cursor + 2]))
            cursor += 2
            token_end = cursor
            continue
        if value.startswith("->", cursor):
            tokens.append(("arrow", "->"))
            cursor += 2
            token_end = cursor
            continue
        char = value[cursor]
        if char in "+-0123456789":
            percentage = _numeric_percentage(value, cursor, token_end)
            if percentage is not None:
                tokens.append(("percent", percentage[0]))
                cursor = token_end = percentage.end()
                continue
        grammar = {"%": PRINTF, "\\": ESCAPE, "{": BRACE, "}": BRACE, "<": TAG, ">": TAG}.get(char)
        if grammar is not None:
            match = grammar.match(value, cursor)
            if match is None:
                raise ValueError("namedResourceUnsupportedFormat")
            tokens.append((char, match[0]))
            cursor = token_end = match.end()
        else:
            if ord(char) < 32:
                tokens.append(("control", char))
            cursor += 1
    return tuple(tokens)
