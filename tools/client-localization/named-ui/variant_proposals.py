"""Explicit pinned physical variants; never infers native duplicate-key lookup."""
from collections import defaultdict
import re

from format_tokens import signature
from named_resources import MAX_BYTES, NamedResources
from propose_named_ui import (CJK, DONOR_BOUNDARY, MACHINE, digest, english_donor,
                              selected_case_aliases, validate_plan)

VARIANT_FIELDS = frozenset({"key", "originalValueSha256", "englishValueSha256",
                            "occurrences", "occurrenceCount"})
SHA256 = re.compile(r"[0-9A-F]{64}")


def validate_variant_plan(plan, inputs):
    if (type(plan) is not dict
            or set(plan) != {"schemaVersion", "family", "codec", "inputSha256", "variants"}
            or type(plan["variants"]) is not list or not 1 <= len(plan["variants"]) <= 200
            or plan["family"] != "core-ui"):
        raise ValueError("namedResourceVariantPlanShape")
    selected, occupied = set(), set()
    for entry in plan["variants"]:
        if (type(entry) is not dict or set(entry) != VARIANT_FIELDS
                or type(entry["key"]) is not str
                or any(type(entry[name]) is not str or SHA256.fullmatch(entry[name]) is None
                       for name in ("originalValueSha256", "englishValueSha256"))
                or type(entry["occurrences"]) is not list or not entry["occurrences"]
                or any(type(line) is not int or not 1 <= line <= MAX_BYTES for line in entry["occurrences"])
                or entry["occurrences"] != sorted(set(entry["occurrences"]))
                or type(entry["occurrenceCount"]) is not int
                or entry["occurrenceCount"] != len(entry["occurrences"])):
            raise ValueError("namedResourceVariantPlanEntry")
        identity = entry["key"].casefold(), entry["originalValueSha256"]
        if identity in selected or occupied.intersection(entry["occurrences"]):
            raise ValueError("namedResourceVariantPlanOverlap")
        selected.add(identity)
        occupied.update(entry["occurrences"])
    # Reuse the established exact key grammar, casefold uniqueness, family,
    # schema/codec and three-file pin validation; variants do not weaken them.
    uniform = {name: plan[name] for name in ("schemaVersion", "family", "codec", "inputSha256")}
    uniform["keys"] = list(dict.fromkeys(entry["key"] for entry in plan["variants"]))
    validate_plan(uniform, inputs)


def _identity(entry):
    return {name: list(entry[name]) if name == "occurrences" else entry[name]
            for name in ("key", "originalValueSha256", "englishValueSha256", "occurrences", "occurrenceCount")}


def _positions(rows):
    return tuple(row.index + 1 for row in rows)


def _matching_rows(rows, value_digest):
    return tuple(row for row in rows if digest(row.value.encode("utf-8")) == value_digest)


def _aligned_groups(documents, key, aliases):
    groups = tuple(document.groups.get(key) for document in documents)
    if key in aliases:
        return groups, "caseAliasAmbiguous"
    if any(group is None for group in groups):
        return groups, "requiredKeyMissing"
    if len({len(group) for group in groups}) != 1:
        return groups, "occurrenceCountChanged"
    if len({_positions(group) for group in groups}) != 1:
        return groups, "physicalKeyPositionsChanged"
    return groups, None


def _donor_problem(before, donor):
    if before == donor:
        return "translationUnchanged"
    if MACHINE.search(before) or MACHINE.search(donor) or not CJK.search(before):
        return "machineOrNonDisplayValue"
    if not english_donor(before, donor):
        return "englishDonorRequired"
    try:
        if signature(before) != signature(donor):
            return "formatSignatureChanged"
    except ValueError:
        return "unsupportedFormatSyntax"
    return None


def _variant_rows(groups, entry):
    original, english, current = groups
    expected = tuple(entry["occurrences"])
    source_rows = _matching_rows(original, entry["originalValueSha256"])
    if _positions(source_rows) != expected:
        return (), (), "originalVariantOccurrencesChanged"
    donor_rows = _matching_rows(english, entry["englishValueSha256"])
    if _positions(donor_rows) != expected:
        return (), (), "englishVariantOccurrencesChanged"
    targets = {row.index: row for row in current}
    if any(targets[row.index].value != row.value for row in source_rows):
        return (), (), "currentChineseMeaningChanged"
    reason = _donor_problem(source_rows[0].value, donor_rows[0].value)
    return source_rows, donor_rows, reason


def _replace_rows(current, replacements):
    lines = list(current.lines)
    selected_rows = {row.index: row for rows in current.groups.values() for row in rows
                     if row.index in replacements}
    for index, value in replacements.items():
        if any(char in value for char in "\r\n\0"):
            raise ValueError("namedResourceRowInjection")
        row = selected_rows[index]
        lines[index] = row.prefix + value.encode("utf-8", errors="strict") + row.ending
    after = current.bom + b"".join(lines)
    if len(after) > MAX_BYTES:
        raise ValueError("namedResourceProposedBounds")
    return after


def propose_variants(original, english, current, plan):
    documents = tuple(NamedResources.parse(document.raw) for document in (original, english, current))
    original, english, current = documents
    validate_variant_plan(plan, tuple(document.raw for document in documents))
    keys = tuple(dict.fromkeys(entry["key"] for entry in plan["variants"]))
    aliases = selected_case_aliases(documents, keys)
    aligned = {key: _aligned_groups(documents, key, aliases) for key in keys}
    replacements, merged, held = {}, [], defaultdict(list)
    for entry in plan["variants"]:
        groups, reason = aligned[entry["key"]]
        if reason is None:
            source_rows, donor_rows, reason = _variant_rows(groups, entry)
            if reason is None:
                for source, donor in zip(source_rows, donor_rows):
                    replacements[source.index] = donor.value
                merged.append(_identity(entry))
        if reason is not None:
            held[reason].append(_identity(entry))
    after = _replace_rows(current, replacements)
    merged_keys = sorted({entry["key"] for entry in merged})
    report = {"schemaVersion": 1, "status": "NamedUiVariantProposalOnly", "family": plan["family"],
              "selectedKeyCount": len(keys), "selectedVariantCount": len(plan["variants"]),
              "mergedKeys": merged_keys, "mergedKeyCount": len(merged_keys),
              "mergedVariants": merged, "mergedVariantCount": len(merged),
              "changedPhysicalRows": len(replacements), "held": dict(sorted(held.items())),
              "currentAssignmentRows": sum(map(len, current.groups.values())),
              "currentUniqueKeys": len(current.groups), "proposalBytes": len(after),
              "currentSha256": digest(current.raw), "proposalSha256": digest(after),
              "encoding": "UTF8; original BOM state preserved", "clientApplied": False,
              "liveRenderingVerified": False, "donorBoundary": DONOR_BOUNDARY,
              "variantBoundary": "Exact physical-row proposal only; native duplicate-key lookup is unverified."}
    return after, report
