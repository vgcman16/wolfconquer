"""Three-way translation proposal only. Never edits a client or source input."""
from __future__ import annotations

from collections import Counter, defaultdict
import hashlib
import re

from resource_table import ResourceTable

PRINTF = re.compile(
    r"%(?:[0-9]+\$)?[-+ #0']*(?:\*(?:[0-9]+\$)?|[0-9]+)?"
    r"(?:\.(?:\*(?:[0-9]+\$)?|[0-9]+))?"
    r"(?:I64|I32|hh|ll|[hljztLw])?[diuoxXfFeEgGaAcCsSpn%]"
)
ESCAPE = re.compile(r"\\(?:x[0-9A-Fa-f]{2,8}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|[0-7]{1,3}|[^\r\n])")
BRACE = re.compile(r"\{\{|\}\}|\{[^{}\r\n]*\}")
TAG = re.compile(r"<[^<>\r\n]*>")
# Actual unchanged-Chinese numeric-percentage labels/help, not a global '%' fallback.
LITERAL_PERCENT_KEYS = frozenset((*range(10200, 10221), 10450, 10457, 10458, 10459,
                                 *range(11328, 11340)))
NUMBER_PERCENT = re.compile(r"[+-]?[0-9]+%")
NAVIGATION_ARROW_KEY = 10407
MACHINE_LITERAL_KEY = 100077


def signature(value: str, *, key: int | None = None) -> tuple:
    """Conservative exact ordered tokens; unsupported syntax is held for review."""
    tokens = []
    cursor = 0
    while cursor < len(value):
        char = value[cursor]
        if (key in LITERAL_PERCENT_KEYS and (char in "+-" or char.isascii() and char.isdigit())
                and (cursor == 0 or value[cursor - 1] not in "0123456789.,+-")):
            percent = NUMBER_PERCENT.match(value, cursor)
            if percent is not None:
                end = percent.end()
                if (end == len(value) or value[end].isspace() or not value[end].isascii()
                        or value[end] in ".,;:!?)]"):
                    tokens.append(("literalPercentage", percent[0]))
                    cursor = end
                    continue
        if key == NAVIGATION_ARROW_KEY and value.startswith("->", cursor):
            tokens.append(("navigationArrow", "->"))
            cursor += 2
            continue
        grammar = {"%": PRINTF, "\\": ESCAPE, "{": BRACE, "}": BRACE,
                   "<": TAG, ">": TAG}.get(char)
        if grammar is not None:
            match = grammar.match(value, cursor)
            if match is None:
                raise ValueError("unsupportedFormatSyntax")
            tokens.append((char, match[0]))
            cursor = match.end()
        elif ord(char) < 32:
            tokens.append(("control", char))
            cursor += 1
        else:
            cursor += 1
    return tuple(tokens)


def merge(original: ResourceTable, english: ResourceTable,
          current: ResourceTable) -> tuple[bytes, dict]:
    replacements: dict[int, str] = {}
    reasons: Counter = Counter()
    held: dict[str, list[int]] = defaultdict(list)
    for key, target in current.rows.items():
        before, translated = original.rows.get(key), english.rows.get(key)
        reason = ""
        if key == MACHINE_LITERAL_KEY:
            reason = "machineLookupLiteral"
        elif before is None:
            reason = "originalChineseMissing"
        elif translated is None:
            reason = "englishMissing"
        elif before.value != target.value:
            reason = "chineseMeaningChanged"
        elif translated.value == before.value:
            reason = "unchangedTranslation"
        else:
            try:
                if signature(before.value, key=key) != signature(translated.value, key=key):
                    reason = "formatSignatureChanged"
                else:
                    translated.value.encode(current.codec, errors="strict")
            except UnicodeError:
                reason = "targetEncodingCannotRepresentTranslation"
            except ValueError:
                reason = "unsupportedFormatSyntax"
        if reason:
            reasons[reason] += 1
            if reason != "unchangedTranslation":
                held[reason].append(key)
        else:
            replacements[key] = translated.value
    after = current.with_values(replacements)
    report = {
        "schemaVersion": 1, "status": "TranslationProposalOnly",
        "originalChineseRows": len(original.rows), "englishRows": len(english.rows),
        "targetRows": len(current.rows), "mergedCount": len(replacements),
        "mergedKeys": sorted(replacements), "heldCounts": dict(sorted(reasons.items())),
        "heldKeys": {key: sorted(value) for key, value in sorted(held.items())},
        "originalChineseSha256": hashlib.sha256(original.raw).hexdigest().upper(),
        "englishSha256": hashlib.sha256(english.raw).hexdigest().upper(),
        "targetBeforeSha256": hashlib.sha256(current.raw).hexdigest().upper(),
        "proposalSha256": hashlib.sha256(after).hexdigest().upper(),
        "targetCodec": current.codec, "targetBomHex": current.bom.hex().upper(),
        "clientFilesChanged": False, "runtimeStarted": False,
        "semanticBoundary": "Exact original/current Chinese text; inherited English translation accuracy remains unverified."
    }
    return after, report
