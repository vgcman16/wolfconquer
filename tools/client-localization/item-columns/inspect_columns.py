"""Read-only aggregate diagnostics; does not print rows, names or selected mappings."""
import argparse
import json

from catalog_rows import parse, read_pinned
from column_alignment import analyze


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--reference-sha256", required=True)
    parser.add_argument("--reference-fields", required=True, type=int, choices=(65, 67))
    parser.add_argument("--current", required=True)
    parser.add_argument("--current-sha256", required=True)
    parser.add_argument("--current-version", required=True, type=int, choices=(7937,))
    args = parser.parse_args()
    try:
        reference = parse(read_pinned(args.reference, args.reference_sha256), args.reference_fields)
        current = parse(read_pinned(args.current, args.current_sha256), 67, current7937=True)
        result = analyze(reference, current)
        print(json.dumps(result.aggregate(), separators=(",", ":")))
        return 0
    except (ValueError, OSError) as error:
        print(json.dumps({"status": "Refused", "errorType": type(error).__name__, "clientApplied": False}, separators=(",", ":")))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
