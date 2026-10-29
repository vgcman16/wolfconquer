"""Owned synthetic column/CLI fixtures; no actual item rows or names."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from catalog_rows import MAX_BYTES, parse, read_pinned
from column_alignment import analyze


def fields(identity, width=65):
    return [str(identity).encode()] + [f"opaque-column-{column}-row-{int(identity)}".encode() for column in range(1, width)]


def render(rows, ending=b"\r\n"):
    return b"".join(b"@@".join(row) + b"@@" + ending for row in rows)


def pair(identities=(1, 2)):
    source, target = [], []
    for identity in identities:
        before = fields(identity)
        after = [str(identity).encode()] + [f"extra-{column}-row-{int(identity)}".encode() for column in range(1, 67)]
        for column in range(1, 65):
            after[65 - column] = before[column]
        source.append(before); target.append(after)
    return source, target


def inspect(source, current):
    return analyze(parse(render(source), 65), parse(render(current), 67, current7937=True))


class ItemColumnsTests(unittest.TestCase):
    def test_complete_reversed_mapping_is_unique_without_appended_or_same_offset_assumptions(self):
        source, current = pair()
        result = inspect(source, current)
        self.assertEqual("UniqueCompleteByteAlignment", result.status)
        self.assertEqual(tuple((column, 65 - column) for column in range(1, 65)), result.unique_byte_mapping)
        self.assertFalse(result.aggregate()["semanticTextRolesProved"])
        self.assertFalse(result.aggregate()["clientApplied"])

    def test_shared_identity_join_is_order_independent_and_extra_rows_grant_no_authority(self):
        source, current = pair((2, 1))
        _, extra = pair((3,))
        result = inspect(source, list(reversed(current)) + extra)
        self.assertEqual(inspect(list(reversed(source)), current).unique_byte_mapping, result.unique_byte_mapping)
        self.assertEqual((2, 3, 2), (result.reference_rows, result.current_rows, result.shared_ids))
        self.assertEqual(1, result.aggregate()["currentOnlyIdentities"])

    def test_duplicate_byte_vectors_report_ambiguity_without_a_chosen_mapping(self):
        source, current = pair()
        for before, after in zip(source, current):
            before[2] = before[1]
            after[63] = after[64]
        result = inspect(source, current)
        self.assertEqual("AmbiguousCompleteByteAlignments", result.status)
        self.assertEqual((), result.unique_byte_mapping)
        self.assertEqual((63, 64), result.columns[0].target_candidates)

    def test_too_few_distinct_target_columns_report_injectivity_failure(self):
        source, current = pair()
        for before, after in zip(source, current):
            before[2] = before[1]
        result = inspect(source, current)
        self.assertEqual("InsufficientDistinctTargetColumns", result.status)
        self.assertEqual((), result.unique_byte_mapping)

    def test_any_missing_complete_vector_prevents_mapping_even_with_other_unique_matches(self):
        source, current = pair()
        current[1][64] = b"changed-in-one-shared-row"
        result = inspect(source, current)
        self.assertEqual("MissingExactColumnMatches", result.status)
        self.assertEqual((), result.columns[0].target_candidates)
        self.assertEqual((), result.unique_byte_mapping)

    def test_length_framing_prevents_concatenation_collision(self):
        source, current = pair()
        source[0][1], source[1][1] = b"a", b"bc"
        current[0][64], current[1][64] = b"ab", b"c"
        self.assertEqual((), inspect(source, current).columns[0].target_candidates)

    def test_no_shared_ids_is_no_evidence_and_not_a_unique_empty_mapping(self):
        source, _ = pair((1,)); _, current = pair((2,))
        result = inspect(source, current)
        self.assertEqual("NoSharedIdentities", result.status)
        self.assertEqual((), result.unique_byte_mapping)

    def test_raw_bytes_endings_and_opaque_fields_are_captured_without_mutable_aliases(self):
        source, current = pair()
        source[0][4] = current[0][61] = b"\xff\x80"
        raw = bytearray(render(source, b"\n")); expected = bytes(raw)
        parsed = parse(raw, 65); raw[:] = b"!" * len(raw)
        self.assertEqual(expected, parsed.raw)
        self.assertEqual(b"\n", parsed.rows[0].ending)
        self.assertEqual(b"\xff\x80", parsed.rows[0].fields[4])
        self.assertEqual("UniqueCompleteByteAlignment", analyze(parsed, parse(render(current), 67, current7937=True)).status)
        with self.assertRaises(TypeError):
            parsed.by_id[1] = None

    def test_exact_width_id_duplicates_nul_empty_and_current_endings_refuse(self):
        source, current = pair()
        malformed = [b"", b"\r\n", render(source) + b"\n", render(source)[:-3],
                     render(source) + b"\0", render([fields("01"), fields("1")]),
                     render([fields("4294967296")]), render([fields("-1")])]
        for raw in malformed:
            with self.subTest(size=len(raw)), self.assertRaises(ValueError):
                parse(raw, 65)
        for ending in (b"\n", b"\r", b""):
            with self.subTest(ending=ending), self.assertRaises(ValueError):
                parse(render(current, ending), 67, current7937=True)
        with self.assertRaises(ValueError): parse(render(source), 66)
        with self.assertRaises(ValueError): parse(render(source), 65, current7937=True)

    def test_zero_maximum_and_leading_zero_id_spelling_are_retained(self):
        raw = render([fields("0000"), fields("04294967295")])
        parsed = parse(raw, 65)
        self.assertEqual((0, 4294967295), tuple(row.item_id for row in parsed.rows))
        self.assertEqual(b"0000", parsed.rows[0].fields[0]); self.assertEqual(raw, parsed.raw)

    def test_framed_vector_operational_bound_refuses_without_modifying_inputs(self):
        source, current = pair(); old, new = render(source), render(current)
        with patch("column_alignment.MAX_VECTOR_BYTES", 1), self.assertRaisesRegex(ValueError, "vectorBounds"):
            analyze(parse(old, 65), parse(new, 67, current7937=True))
        self.assertEqual(old, render(source)); self.assertEqual(new, render(current))

    def test_pinned_ordinary_reads_reject_mismatch_relative_and_oversized_files(self):
        with tempfile.TemporaryDirectory(prefix="item-column-pins-") as temporary:
            path = Path(temporary) / "source.decoded"; raw = render(pair()[0]); path.write_bytes(raw)
            pin = hashlib.sha256(raw).hexdigest().upper()
            self.assertEqual(raw, read_pinned(path, pin))
            for candidate, digest in ((path, "0" * 64), (path, "00"), (Path("relative.decoded"), pin)):
                with self.subTest(candidate=str(candidate)), self.assertRaises((ValueError, OSError)):
                    read_pinned(candidate, digest)
            large = Path(temporary) / "large.decoded"
            with large.open("wb") as stream: stream.truncate(MAX_BYTES + 1)
            with self.assertRaisesRegex(ValueError, "inputBounds"): read_pinned(large, "0" * 64)
            self.assertEqual(raw, path.read_bytes())

    def test_cli_outputs_only_aggregate_diagnostics_and_never_creates_a_map_or_output(self):
        with tempfile.TemporaryDirectory(prefix="item-column-cli-") as temporary:
            directory = Path(temporary); old, new = map(render, pair())
            source = directory / "source.decoded"; current = directory / "current.decoded"
            source.write_bytes(old); current.write_bytes(new)
            command = [sys.executable, str(Path(__file__).parent / "inspect_columns.py"),
                       "--reference", str(source), "--reference-sha256", hashlib.sha256(old).hexdigest(), "--reference-fields", "65",
                       "--current", str(current), "--current-sha256", hashlib.sha256(new).hexdigest(), "--current-version", "7937"]
            result = subprocess.run(command, capture_output=True, text=True, timeout=10, check=False)
            self.assertEqual(0, result.returncode, result.stderr)
            report = json.loads(result.stdout); self.assertEqual("UniqueCompleteByteAlignment", report["status"])
            self.assertNotIn("opaque", result.stdout); self.assertNotIn("mapping", result.stdout)
            self.assertEqual({source, current}, set(directory.iterdir()))
            refused = subprocess.run([*command[:-1], "7931"], capture_output=True, text=True, timeout=10, check=False)
            self.assertNotEqual(0, refused.returncode)
            self.assertEqual(old, source.read_bytes()); self.assertEqual(new, current.read_bytes())


if __name__ == "__main__":
    unittest.main()
