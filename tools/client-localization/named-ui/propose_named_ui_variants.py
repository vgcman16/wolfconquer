"""Explicit physical-variant proposal CLI; no client apply operation."""
import argparse
import json
from pathlib import Path
from types import MappingProxyType
import uuid

from named_resources import NamedResources, ordinary, read_bytes
from propose_named_ui import unique_json
from variant_proposals import propose_variants

PLAN_FAMILIES = MappingProxyType({"core-ui-variants.json": "core-ui"})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original", type=Path)
    parser.add_argument("english", type=Path)
    parser.add_argument("current", type=Path)
    parser.add_argument("--plan", choices=tuple(PLAN_FAMILIES), required=True)
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
    after, report = propose_variants(*documents, plan)
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
