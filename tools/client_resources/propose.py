"""Prepare a resource-only proposal; never applies or launches a client."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import stat
import uuid

from merge_resources import merge
from resource_table import read_table
from reviewed import complete


def ensure_no_reparse(path: Path) -> None:
    for item in (path, *path.parents):
        metadata = item.lstat()
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError("proposalReparseParentRefused")


def write_proposal(tool_root: Path, data: bytes, report: dict) -> Path:
    # Fixed owned tool location; caller cannot name a client destination.
    root = tool_root.absolute()
    ensure_no_reparse(root)
    parent = root / "proposals"
    parent.mkdir(exist_ok=True)
    ensure_no_reparse(parent)
    if not parent.is_dir():
        raise ValueError("proposalDirectoryRequired")
    destination = parent / uuid.uuid4().hex
    destination.mkdir()
    with (destination / "UStrRes.ini").open("xb") as stream:
        stream.write(data)
    with (destination / "merge-report.json").open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(report, stream, ensure_ascii=True, separators=(",", ":"))
        stream.write("\n")
    return destination


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_chinese", type=Path)
    parser.add_argument("english", type=Path)
    parser.add_argument("current_chinese", type=Path)
    parser.add_argument("--original-encoding", choices=("cp936", "utf-8", "utf-16-le", "utf-16-be"))
    parser.add_argument("--english-encoding", choices=("cp936", "utf-8", "utf-16-le", "utf-16-be"))
    parser.add_argument("--current-encoding", choices=("cp936", "utf-8", "utf-16-le", "utf-16-be"))
    parser.add_argument("--reviewed", type=Path, help="explicit reviewed exceptions JSON")
    parser.add_argument("--write-proposal", action="store_true")
    args = parser.parse_args()
    original = read_table(args.original_chinese, args.original_encoding)
    english = read_table(args.english, args.english_encoding)
    current = read_table(args.current_chinese, args.current_encoding)
    after, report = (complete(original, english, current, args.reviewed) if args.reviewed
                     else merge(original, english, current))
    if args.write_proposal:
        destination = write_proposal(Path(__file__).absolute().parent, after, report)
        report["proposalDirectory"] = str(destination)
    print(json.dumps(report, ensure_ascii=True, separators=(",", ":")))


if __name__ == "__main__":
    main()
