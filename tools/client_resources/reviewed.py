"""Explicit, hash-bound reviewed exceptions for the current 7937 table."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from merge_resources import MACHINE_LITERAL_KEY, merge, signature
from resource_table import ResourceTable, ordinary_file

REVIEWED_KEYS = frozenset((10375, 11090))
MAX_REVIEW_BYTES = 16 * 1024


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("reviewedDuplicateJsonMember")
        result[key] = value
    return result


def reviewed_values(current: ResourceTable, proposal: ResourceTable, path: Path) -> tuple[dict[int, str], str]:
    path = ordinary_file(path)
    with path.open("rb") as stream:
        raw = stream.read(MAX_REVIEW_BYTES + 1)
    if not raw or len(raw) > MAX_REVIEW_BYTES:
        raise ValueError("reviewedTranslationBounds")
    document = json.loads(raw.decode("utf-8", errors="strict"), object_pairs_hook=unique_object)
    if (type(document) is not dict or set(document) != {"schemaVersion", "targetBeforeSha256", "entries"}
            or type(document["schemaVersion"]) is not int or document["schemaVersion"] != 1
            or document["targetBeforeSha256"] != digest(current.raw)
            or type(document["entries"]) is not list or len(document["entries"]) != 2):
        raise ValueError("reviewedTranslationTargetMismatch")
    replacements: dict[int, str] = {}
    for entry in document["entries"]:
        if type(entry) is not dict or set(entry) != {"key", "expectedValueSha256", "english", "reason"}:
            raise ValueError("reviewedTranslationShapeInvalid")
        key, value = entry["key"], entry["english"]
        if type(key) is not int or key not in REVIEWED_KEYS or key in replacements or key not in current.rows:
            raise ValueError("reviewedTranslationKeyInvalid")
        if (type(value) is not str or not value or any(c in value for c in "\r\n\x00")
                or type(entry["reason"]) is not str or not entry["reason"].strip()):
            raise ValueError("reviewedTranslationValueInvalid")
        before = current.rows[key].value
        if digest(before.encode("utf-8")) != entry["expectedValueSha256"] or proposal.rows[key].value != before:
            raise ValueError("reviewedTranslationBaselineChanged")
        if signature(before, key=key) != signature(value, key=key):
            raise ValueError("reviewedTranslationFormatChanged")
        value.encode(current.codec, errors="strict")
        replacements[key] = value
    if set(replacements) != REVIEWED_KEYS:
        raise ValueError("reviewedTranslationKeysMissing")
    return replacements, digest(raw)


def complete(original: ResourceTable, english: ResourceTable, current: ResourceTable, reviewed_path: Path):
    automatic, report = merge(original, english, current)
    parsed = ResourceTable.parse(automatic, current.codec)
    replacements, reviewed_hash = reviewed_values(current, parsed, reviewed_path)
    after = parsed.with_values(replacements)
    final = ResourceTable.parse(after, current.codec)
    if (MACHINE_LITERAL_KEY in current.rows and
            final.rows[MACHINE_LITERAL_KEY].value != current.rows[MACHINE_LITERAL_KEY].value):
        raise ValueError("machineLookupLiteralChanged")
    report["automaticHeldCounts"] = report.pop("heldCounts")
    report["automaticHeldKeys"] = report.pop("heldKeys")
    report.update(status="ReviewedTranslationProposalOnly", reviewedOverrideKeys=sorted(replacements),
                  reviewedSourceSha256=reviewed_hash, proposalSha256=digest(after),
                  totalChangedRows=sum(final.rows[k].value != row.value for k, row in current.rows.items()))
    return after, report
