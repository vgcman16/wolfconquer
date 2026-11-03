"""Read-only complete decoded-text column evidence; no rows or chosen mapping."""
import argparse
import json
import re


class _Parser(argparse.ArgumentParser):
    def error(self, message):
        # argparse normally echoes user-supplied arguments, including private paths.
        raise ValueError("argumentsRefused")


def _columns(value):
    if not re.fullmatch(r"[1-9][0-9]*(?:,[1-9][0-9]*)*", value):
        raise ValueError("columnsRequired")
    values = tuple(int(part) for part in value.split(","))
    if len(values) > 66 or len(set(values)) != len(values):
        raise ValueError("columnsInvalid")
    return values


def main():
    try:
        from catalog_rows import parse, read_pinned
        from decoded_text_alignment import analyze

        parser = _Parser(description=__doc__)
        parser.add_argument("--reference", required=True)
        parser.add_argument("--reference-sha256", required=True)
        parser.add_argument("--reference-fields", required=True, type=int, choices=(65, 67))
        parser.add_argument("--reference-codec", required=True, choices=("utf-8", "cp936"))
        parser.add_argument("--current", required=True)
        parser.add_argument("--current-sha256", required=True)
        parser.add_argument("--current-version", required=True, type=int, choices=(7937,))
        parser.add_argument("--current-codec", required=True, choices=("utf-8", "cp936"))
        parser.add_argument("--columns", required=True, type=_columns)
        parser.add_argument("--column-evidence", action="store_true")
        args = parser.parse_args()
        if any(index >= args.reference_fields for index in args.columns):
            raise ValueError("columnOutOfRange")
        reference = parse(read_pinned(args.reference, args.reference_sha256), args.reference_fields)
        current = parse(read_pinned(args.current, args.current_sha256), 67, current7937=True)
        result = analyze(reference, current, reference_codec=args.reference_codec,
                         current_codec=args.current_codec, reference_columns=args.columns,
                         current_version=args.current_version)
        report = result.aggregate()
        if args.column_evidence:
            report["columnEvidence"] = result.column_evidence()
        print(json.dumps(report, separators=(",", ":")))
        return 0
    except Exception as error:
        # The exception type alone is bounded; messages may contain private paths.
        print(json.dumps({"status": "Refused", "errorType": type(error).__name__,
                          "clientApplied": False}, separators=(",", ":")))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
