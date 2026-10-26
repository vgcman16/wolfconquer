"""Read only 1..8 selected normal IAT identities from an explicitly pinned PE."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import sys

sys.dont_write_bytecode = True
import pe_import_slots as pe

MAX_FILE_BYTES = 64 * 1024 * 1024
MAX_TOOL_BYTES = 64 * 1024
MAX_OUTPUT_BYTES = 16384


class Arguments(argparse.ArgumentParser):
    def error(self, message):
        raise pe.Refused("invalid-command-arguments")


def ordinary(path):
    if not path.is_absolute() or len(str(path)) > 4096 or str(path).startswith(("\\\\", "//")):
        raise pe.Refused("explicit-local-absolute-path-required")
    if any(part in (".", "..") for part in path.parts):
        raise pe.Refused("path-traversal-refused")
    first = None
    for number, current in enumerate((path, *path.parents)):
        info = current.lstat()
        if getattr(info, "st_file_attributes", 0) & 0x440 or stat.S_ISLNK(info.st_mode):
            raise pe.Refused("reparse-or-device-path-refused")
        if not (stat.S_ISREG(info.st_mode) if number == 0 else stat.S_ISDIR(info.st_mode)):
            raise pe.Refused("ordinary-file-and-ancestors-required")
        if number == 0:
            first = info
    return first


def identity(info):
    return info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns


def same_file(path_info, handle_info):
    # Windows lstat/fstat can expose different ctime semantics. Preserve each
    # API's full before/after identity; match their common file/write fields.
    return identity(path_info)[:4] == identity(handle_info)[:4]


def tool_snapshot(path):
    before = ordinary(path)
    if not 1 <= before.st_size <= MAX_TOOL_BYTES:
        raise pe.Refused("tool-source-size-refused")
    with path.open("rb") as source:
        opened = os.fstat(source.fileno())
        if not same_file(before, opened):
            raise pe.Refused("tool-source-identity-changed")
        data = source.read(MAX_TOOL_BYTES + 1)
        if identity(os.fstat(source.fileno())) != identity(opened):
            raise pe.Refused("tool-source-identity-changed")
    if len(data) != before.st_size or identity(ordinary(path)) != identity(before):
        raise pe.Refused("tool-source-identity-changed")
    return identity(before), data


def query(path, expected_size, expected_sha, slots):
    budget = pe.Budget()
    if not 64 <= expected_size <= MAX_FILE_BYTES or len(expected_sha) != 64 or any(
            c not in "0123456789abcdefABCDEF" for c in expected_sha):
        raise pe.Refused("expected-size-or-sha256-invalid")
    if not 1 <= len(slots) <= 8 or len(set(slots)) != len(slots):
        raise pe.Refused("selected-slot-count-or-duplicates")
    tools = [Path(__file__).absolute(), Path(pe.__file__).absolute()]
    snapshots = [(tool, tool_snapshot(tool)) for tool in tools]
    before = ordinary(path)
    if before.st_size != expected_size:
        raise pe.Refused("physical-source-length-differs")
    with path.open("rb") as source:
        opened = os.fstat(source.fileno())
        if not same_file(before, opened):
            raise pe.Refused("physical-source-identity-changed")
        data = source.read(expected_size + 1)
        budget.check()
        if len(data) != expected_size or hashlib.sha256(data).hexdigest() != expected_sha.lower():
            raise pe.Refused("physical-source-sha256-differs")
        image = pe.ImportSlots(data, budget)
        imports = image.selected(slots)
        source.seek(0)
        after = source.read(expected_size + 1)
        budget.check()
        if after != data or identity(os.fstat(source.fileno())) != identity(opened) or identity(ordinary(path)) != identity(before):
            raise pe.Refused("physical-source-identity-changed")
    provenance = []
    for tool, snapshot in snapshots:
        if tool_snapshot(tool) != snapshot:
            raise pe.Refused("tool-source-identity-changed")
        provenance.append({"name": tool.name, "sha256": hashlib.sha256(snapshot[1]).hexdigest()})
    report = {"schemaVersion": 1, "kind": "selected-physical-normal-imports", "source": str(path),
              "size": expected_size, "sha256": expected_sha.upper(), "peKind": "PE32" if image.width == 4 else "PE32+",
              "imageBase": f"{image.base:0{image.width * 2}x}", "imports": imports, "tools": provenance,
              "limits": {"fileBytes": MAX_FILE_BYTES, "selectedSlots": 8, "descriptors": pe.MAX_DESCRIPTORS,
                         "thunksPerCandidate": pe.MAX_THUNKS, "totalThunkProbes": pe.MAX_TOTAL_THUNKS,
                         "nameBytes": pe.MAX_NAME_BYTES, "seconds": 30, "outputBytes": MAX_OUTPUT_BYTES},
              "sourceUnchanged": True, "toolsUnchanged": True, "partialOutput": False,
              "limitation": "Normal physical import identities only; no delay/dynamic imports, native loading, runtime target or behavior proof."}
    output = json.dumps(report, indent=2) + "\n"
    budget.check()
    if len(output.encode("utf-8")) > MAX_OUTPUT_BYTES:
        raise pe.Refused("output-cap-exceeded")
    return output


def main():
    parser = Arguments(description=__doc__)
    parser.add_argument("--pe", required=True)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--size", required=True)
    parser.add_argument("--iat", nargs="+", required=True, help="1..8 distinct eight- or sixteen-digit hexadecimal IAT VAs")
    try:
        args = parser.parse_args()
        if not args.size.isascii() or not args.size.isdecimal() or any(
                len(value) not in (8, 16) or any(c not in "0123456789abcdefABCDEF" for c in value) for value in args.iat):
            raise pe.Refused("invalid-size-or-iat-va")
        output = query(Path(args.pe), int(args.size), args.sha256, [int(value, 16) for value in args.iat])
    except pe.Refused as error:
        print("PE import-slot query refused: " + str(error), file=sys.stderr)
        return 1
    except (OSError, ValueError, OverflowError):
        print("PE import-slot query refused: invalid-or-changing-input.", file=sys.stderr)
        return 1
    print(output, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
