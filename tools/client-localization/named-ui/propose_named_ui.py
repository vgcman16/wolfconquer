"""Pinned three-way named UI proposal; no client apply operation."""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re
from types import MappingProxyType
import uuid

from named_resources import NamedResources, ordinary, read_bytes
from format_tokens import signature

CJK = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff]")
MACHINE = re.compile(r"(?i)(?:https?|ftp)://|\b(?:SOFTWARE|HKEY_[A-Z_]+)[\\/]|Font[\\/]|^[A-Z]:[\\/]")
PLAN_FAMILIES = MappingProxyType({
    "stat-ui-family.json": "character-stat-ui",
    "gear-ui-family.json": "equipment-ui",
    "combined-ui-family.json": "character-equipment-ui",
    "mail-trade-ui-family.json": "mail-trade-ui",
    "storage-vendor-ui-family.json": "storage-vendor-ui",
    "quest-social-ui-family.json": "quest-social-ui",
    "guild-team-ui-family.json": "guild-team-ui",
    "core-ui-family.json": "core-ui",
})
FAMILIES = frozenset(PLAN_FAMILIES.values())
DONOR_BOUNDARY = "Caller-supplied English; independent wording and rendering review required."


def digest(raw):
    return hashlib.sha256(raw).hexdigest().upper()


def unique_json(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("namedResourceDuplicatePlanProperty")
        result[key] = value
    return result


def validate_plan(plan, inputs):
    if (type(plan) is not dict or set(plan) != {"schemaVersion", "family", "codec", "inputSha256", "keys"}
            or type(plan["schemaVersion"]) is not int or plan["schemaVersion"] != 1
            or type(plan["family"]) is not str or plan["family"] not in FAMILIES or plan["codec"] != "utf-8"
            or type(plan["keys"]) is not list or not plan["keys"] or len(plan["keys"]) > 200):
        raise ValueError("namedResourcePlanShape")
    if (any(type(key) is not str or not re.fullmatch(r"STR_[A-Z0-9_]+", key) for key in plan["keys"])
            or len(set(plan["keys"])) != len(plan["keys"])
            or type(plan["inputSha256"]) is not dict
            or set(plan["inputSha256"]) != {"original", "english", "current"}):
        raise ValueError("namedResourcePlanKeys")
    for name, raw in zip(("original", "english", "current"), inputs):
        if digest(raw) != plan["inputSha256"][name]:
            raise ValueError("namedResourceInputPinMismatch")


def selected_case_aliases(documents, keys):
    selected = {key.casefold(): key for key in keys}
    ambiguous = set()
    for document in documents:
        for name in document.groups:
            key = selected.get(name.casefold())
            if key is not None and name != key:
                ambiguous.add(key)
    return frozenset(ambiguous)


def propose(original, english, current, plan):
    # Pinned captured bytes are authoritative; supplied parsed metadata is not.
    original, english, current = (NamedResources.parse(document.raw) for document in (original, english, current))
    validate_plan(plan, (original.raw, english.raw, current.raw))
    aliases = selected_case_aliases((original, english, current), plan["keys"])
    replacements, held = {}, defaultdict(list)
    for key in plan["keys"]:
        groups = [item.groups.get(key) for item in (original, english, current)]
        reason = None
        if key in aliases:
            reason = "caseAliasAmbiguous"
        elif any(group is None for group in groups):
            reason = "requiredKeyMissing"
        elif len({len(group) for group in groups}) != 1:
            reason = "occurrenceCountChanged"
        elif any(len({row.value for row in group}) != 1 for group in groups):
            reason = "nonUniformDuplicateMeaning"
        else:
            before, donor, target = (group[0].value for group in groups)
            if before != target:
                reason = "currentChineseMeaningChanged"
            elif before == donor:
                reason = "translationUnchanged"
            elif MACHINE.search(before) or MACHINE.search(donor) or not CJK.search(before):
                reason = "machineOrNonDisplayValue"
            elif not re.search(r"[A-Za-z]", donor) or any(not (32 <= ord(c) <= 126 or c == "\t") for c in donor):
                reason = "englishDonorRequired"
            else:
                try:
                    if signature(before) != signature(donor):
                        reason = "formatSignatureChanged"
                except ValueError:
                    reason = "unsupportedFormatSyntax"
            if reason is None:
                replacements[key] = donor
        if reason is not None:
            held[reason].append(key)
    after = current.replace_values(replacements)
    physical = sum(len(current.groups[key]) for key in replacements)
    report = {"schemaVersion": 1, "status": "NamedUiProposalOnly", "family": plan["family"],
              "selectedKeys": len(plan["keys"]), "mergedKeys": sorted(replacements),
              "mergedKeyCount": len(replacements), "changedPhysicalRows": physical,
              "held": dict(sorted(held.items())), "currentAssignmentRows": sum(map(len, current.groups.values())),
              "currentUniqueKeys": len(current.groups), "proposalBytes": len(after),
              "currentSha256": digest(current.raw), "proposalSha256": digest(after),
              "encoding": "UTF8; original BOM state preserved", "clientApplied": False,
              "liveRenderingVerified": False, "donorBoundary": DONOR_BOUNDARY}
    return after, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original", type=Path)
    parser.add_argument("english", type=Path)
    parser.add_argument("current", type=Path)
    parser.add_argument("--plan", choices=tuple(PLAN_FAMILIES), default="stat-ui-family.json")
    parser.add_argument("--write-proposal", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).absolute().parent
    plan_bytes = read_bytes(root / args.plan)
    if len(plan_bytes) > 65536:
        raise ValueError("namedResourcePlanBounds")
    plan = json.loads(plan_bytes.decode("utf-8-sig"), object_pairs_hook=unique_json)
    if type(plan) is not dict or plan.get("family") != PLAN_FAMILIES[args.plan]:
        raise ValueError("namedResourcePlanFamilyMismatch")
    documents = [NamedResources.parse(read_bytes(path)) for path in (args.original, args.english, args.current)]
    after, report = propose(*documents, plan)
    if args.write_proposal:
        ordinary(root, directory=True)
        parent = root / "proposals"
        parent.mkdir(exist_ok=True)
        ordinary(parent, directory=True)
        destination = parent / uuid.uuid4().hex
        destination.mkdir()
        with (destination / "Cn_Res.ini").open("xb") as stream:
            stream.write(after)
        with (destination / "proposal-report.json").open("x", encoding="utf-8") as stream:
            json.dump(report, stream, separators=(",", ":"))
            stream.write("\n")
        report["proposalDirectory"] = str(destination)
    print(json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
