"""Synthetic decoded-vector/CLI controls; no actual catalogs or inferred roles."""
from dataclasses import FrozenInstanceError
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from catalog_rows import Catalog, parse
import decoded_text_alignment as core
from test_item_columns import fields, render


def pair(identities=(991731041, 991731042), width=65):
    source, current = [], []
    # Literal independent CP936/UTF8 vectors: U+804A and U+00B7.
    # CPython primary fixtures: Lib/test/test_codecencodings_cn.py and
    # Modules/cjkcodecs/_codecs_cn.c (GBK_DECODE), github.com/python/cpython.
    old_text = (b"\xc1\xc4", b"\xa1\xa4")
    new_text = (b"\xe8\x81\x8a", b"\xc2\xb7")
    for index, identity in enumerate(identities):
        old = fields(identity, width)
        new = [str(identity).encode()] + [f"current-only-{column}-{int(identity)}".encode() for column in range(1, 67)]
        old[1], new[12] = old_text[index % 2], new_text[index % 2]
        old[2] = new[9] = f"PRIVATE_ROW_LABEL_{int(identity)}".encode()
        old[3] = new[7] = new[66] = b"7"
        source.append(old); current.append(new)
    return source, current


def inspect(source, current, columns=(1, 2), reference_codec="cp936", current_codec="utf-8", **options):
    return core.analyze(parse(render(source), len(source[0])), parse(render(current), 67, current7937=True),
                        reference_codec=reference_codec, current_codec=current_codec,
                        reference_columns=columns, current_version=7937, **options)


def columns(result):
    return {column.source_index: column for column in result.columns}


class DecodedTextColumnsTests(unittest.TestCase):
    def test_literal_cross_encoding_and_ascii_vectors_match_moved_columns_without_roles(self):
        source, current = pair()
        self.assertNotEqual(source[0][1], current[0][12])
        result = inspect(source, current)
        self.assertEqual("UniqueCompleteDecodedTextCandidates", result.status)
        self.assertEqual((12,), columns(result)[1].target_candidates)
        self.assertEqual((9,), columns(result)[2].target_candidates)
        self.assertFalse(result.aggregate()["semanticTextRolesProved"])
        self.assertFalse(result.aggregate()["chosenMappingProduced"])
        self.assertFalse(result.aggregate()["clientApplied"])
        for old, new in zip(source, current):
            old[1], new[12] = new[12], old[1]
        reverse = inspect(source, current, reference_codec="utf-8", current_codec="cp936")
        self.assertEqual((12,), columns(reverse)[1].target_candidates)

    def test_constant_numeric_vectors_retain_all_candidates_and_injectivity_limits(self):
        source, current = pair()
        ambiguous = inspect(source, current, (3,))
        self.assertEqual("AmbiguousCompleteDecodedTextAlignments", ambiguous.status)
        self.assertEqual((7, 66), columns(ambiguous)[3].target_candidates)
        for old, new in zip(source, current):
            old[4] = b"7"; new[66] = b"different"
        insufficient = inspect(source, current, (3, 4))
        self.assertEqual("InsufficientDistinctTargetColumns", insufficient.status)
        self.assertEqual((7,), columns(insufficient)[3].target_candidates)
        self.assertEqual((7,), columns(insufficient)[4].target_candidates)

    def test_complete_vector_includes_late_shared_rows_and_length_framing(self):
        source, current = pair(tuple(range(1, 34)))
        current[-1][12] = b"different-only-in-last-shared-row"
        result = inspect(source, current, (1,))
        self.assertEqual("MissingExactDecodedTextMatches", result.status)
        self.assertEqual((), columns(result)[1].target_candidates)
        source, current = pair((1, 2))
        source[0][1], source[1][1] = b"a", b"bc"
        current[0][12], current[1][12] = b"ab", b"c"
        self.assertEqual((), columns(inspect(source, current, (1,)))[1].target_candidates)

    def test_exact_text_does_not_trim_fold_strip_bom_or_normalize_unicode(self):
        for before, after in ((b" A ", b"A"), (b"A", b"a"), (b"\xef\xbb\xbfA", b"A"),
                              (b"\xc3\xa9", b"e\xcc\x81")):
            with self.subTest(before=before, after=after):
                source, current = pair((1,))
                source[0][1], current[0][12] = before, after
                result = inspect(source, current, (1,), "utf-8", "utf-8")
                self.assertEqual((), columns(result)[1].target_candidates)

    def test_canonical_ids_join_in_sorted_identity_order_and_ignore_nonshared_cells(self):
        source, current = pair((4294967295, 0))
        source[0][0], current[1][0] = b"04294967295", b"0000"
        source_extra = fields(999, 65); source_extra[1] = b"\xff"
        current_extra = fields(888, 67); current_extra[12] = b"\xff"
        result = inspect(source + [source_extra], list(reversed(current)) + [current_extra])
        self.assertEqual((3, 3, 2), (result.reference_rows, result.current_rows, result.shared_ids))
        self.assertEqual((12,), columns(result)[1].target_candidates)
        self.assertEqual((9,), columns(result)[2].target_candidates)
        self.assertEqual((1, 1), (result.aggregate()["referenceOnlyIdentities"], result.aggregate()["currentOnlyIdentities"]))
        duplicate = [fields("01"), fields("1")]
        with self.assertRaisesRegex(ValueError, "duplicateCanonicalIdentity"):
            parse(render(duplicate), 65)

    def test_no_shared_identity_retains_explicit_status_and_per_column_absence(self):
        source, _ = pair((1,)); _, current = pair((2,))
        with patch.object(core, "_decode", side_effect=AssertionError("No shared cells may be decoded")):
            result = inspect(source, current, (1, 2), max_vector_bytes=1)
        self.assertEqual("NoSharedIdentities", result.status)
        self.assertEqual(0, result.canonical_vector_bytes)
        self.assertEqual(2, len(result.columns)); self.assertEqual(66, len(result.current_columns))
        self.assertTrue(all(column.status == "NoSharedIdentities" and not column.target_candidates for column in result.columns))
        self.assertTrue(all(column.status == "NoSharedIdentities" for column in result.current_columns))

    def test_empty_cells_are_supported_ambiguous_vectors_not_missing_evidence(self):
        source = [[b"1"] + [b""] * 64]
        current = [[b"1"] + [b""] * 66]
        result = inspect(source, current, (1,), "utf-8", "utf-8")
        self.assertEqual("AmbiguousCompleteDecodedTextAlignments", result.status)
        self.assertEqual(tuple(range(1, 67)), columns(result)[1].target_candidates)
        self.assertEqual(268, result.canonical_vector_bytes)  # 67 empty cells x 4-byte length.

    def test_strict_decode_marks_entire_columns_unsupported_without_replacement_or_fallback(self):
        source, current = pair()
        source[-1][1] = b"\x81"  # Truncated CP936 sequence.
        current[-1][12] = b"\xc0\x80"  # Invalid overlong UTF8.
        result = inspect(source, current)
        self.assertEqual("UnsupportedReferenceColumns", result.status)
        self.assertEqual("Undecodable", columns(result)[1].status)
        self.assertEqual((), columns(result)[1].target_candidates)
        self.assertEqual("Undecodable", result.current_columns[11].status)
        self.assertEqual((9,), columns(result)[2].target_candidates)
        source, current = pair((1,))
        source[0][1], current[0][12] = b"\xef\xbf\xbd", b"\xff"
        self.assertEqual((), columns(inspect(source, current, (1,), "utf-8", "utf-8"))[1].target_candidates)

    def test_injected_decoder_fault_exercises_roundtrip_refusal_not_a_real_codec_alias(self):
        source, current = pair()
        source[0][1], current[0][12] = b"ROUNDTRIP_REFERENCE", b"ROUNDTRIP_CURRENT"
        original_decode = core._decode
        def injected(raw, codec):
            return "wrong text" if raw in (b"ROUNDTRIP_REFERENCE", b"ROUNDTRIP_CURRENT") else original_decode(raw, codec)
        with patch.object(core, "_decode", side_effect=injected):
            result = inspect(source, current)
        self.assertEqual("RoundtripMismatch", columns(result)[1].status)
        self.assertEqual("RoundtripMismatch", result.current_columns[11].status)
        self.assertEqual((), columns(result)[1].target_candidates)
        self.assertEqual((9,), columns(result)[2].target_candidates)

    def test_reparses_pinned_raw_instead_of_forged_rows_and_indexes_in_each_role(self):
        source, current = pair()
        genuine = [parse(render(source), 65), parse(render(current), 67, current7937=True)]
        expected = inspect(source, current)
        for role in (0, 1):
            with self.subTest(role=role):
                width = genuine[role].field_count
                forged = parse(render([fields(123, width)]), width)
                docs = list(genuine)
                docs[role] = Catalog(genuine[role].raw, width, forged.rows, forged.by_id)
                result = core.analyze(*docs, reference_codec="cp936", current_codec="utf-8",
                                      reference_columns=(1, 2), current_version=7937)
                self.assertEqual(expected.aggregate(), result.aggregate())
                self.assertEqual(expected.columns, result.columns)

    def test_captures_buffers_and_result_indexes_are_immutable_and_reports_detached(self):
        source, current = pair()
        raw = bytearray(render(source)); original = parse(raw, 65)
        supplied = Catalog(raw, 65, original.rows, original.by_id)
        result = core.analyze(supplied, parse(render(current), 67, current7937=True), reference_codec="cp936",
                              current_codec="utf-8", reference_columns=[1, 2], current_version=7937)
        before = result.aggregate(); raw[:] = b"!" * len(raw)
        self.assertEqual(before, result.aggregate())
        with self.assertRaises((FrozenInstanceError, AttributeError)): result.status = "forged"
        with self.assertRaises((FrozenInstanceError, AttributeError)): result.columns[0].status = "forged"
        self.assertIsInstance(result.columns, tuple); self.assertIsInstance(result.current_columns, tuple)
        self.assertIsInstance(columns(result)[1].target_candidates, tuple)
        summary = result.aggregate(); summary["sharedIdentities"] = 0
        evidence = result.column_evidence(); evidence["columns"][0]["candidateColumns"].append(999)
        self.assertEqual(before, result.aggregate())
        self.assertNotIn(999, result.column_evidence()["columns"][0]["candidateColumns"])

    def test_explicit_codecs_columns_version_and_envelope_are_required(self):
        source, current = pair(); old = parse(render(source), 65); new = parse(render(current), 67, current7937=True)
        good = dict(reference_codec="cp936", current_codec="utf-8", reference_columns=(1,), current_version=7937)
        for key in good:
            missing = dict(good); del missing[key]
            with self.subTest(missing=key), self.assertRaises(TypeError): core.analyze(old, new, **missing)
        invalid = {"reference_codec": ("gbk", "UTF-8", None), "current_codec": ("latin-1", "CP936", None),
                   "reference_columns": ([], (), [0], [65], [True], [1, 1], {1}, None),
                   "current_version": (7931, "7937", True, None)}
        for key, values in invalid.items():
            for value in values:
                with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                    core.analyze(old, new, **dict(good, **{key: value}))
        source67, current = pair(width=67)
        for old_row, new_row in zip(source67, current): new_row[10] = old_row[66]
        self.assertEqual((10,), columns(inspect(source67, current, (66,)))[66].target_candidates)
        wrong = parse(render(current), 67)
        wrong = Catalog(wrong.raw.replace(b"\r\n", b"\n"), 67, wrong.rows, wrong.by_id)
        with self.assertRaises(ValueError): core.analyze(old, wrong, **good)

    def test_forged_multidimensional_buffer_cannot_evade_raw_byte_bounds(self):
        source, current = pair()
        raw = render(source); parsed = parse(raw, 65)
        view = memoryview(raw).cast("B", shape=[1, len(raw)])
        supplied = Catalog(view, 65, parsed.rows, parsed.by_id)
        # Cheap injected operational limit; no large fixture or actual-data probe.
        with patch.object(core, "MAX_BYTES", len(raw) - 1), self.assertRaisesRegex(ValueError, "inputBounds"):
            core.analyze(supplied, parse(render(current), 67, current7937=True), reference_codec="cp936",
                         current_codec="utf-8", reference_columns=(1,), current_version=7937)

    def test_exact_canonical_utf8_budget_precedes_any_vector_allocation(self):
        source = [[b"1"] + [b"r"] * 64]; source[0][1] = b"\xc1\xc4"
        current = [[b"1"] + [b"x"] * 66]; current[0][12] = b"\xe8\x81\x8a"
        # 7 bytes for the reference cell; 65*5 + 7 for all current cells = 339.
        result = inspect(source, current, (1,), max_vector_bytes=339)
        self.assertEqual(339, result.canonical_vector_bytes)
        self.assertEqual((12,), columns(result)[1].target_candidates)
        with patch.object(core, "_vector", side_effect=AssertionError("Allocated before budget check")) as vectors:
            with self.assertRaisesRegex(ValueError, "vectorBounds"):
                inspect(source, current, (1,), max_vector_bytes=338)
            vectors.assert_not_called()
        for value in (0, -1, True, "339", 256 * 1024 * 1024 + 1):
            with self.subTest(budget=value), self.assertRaisesRegex(ValueError, "vectorBudgetRequired"):
                inspect(source, current, (1,), max_vector_bytes=value)


class DecodedTextColumnsCliTests(unittest.TestCase):
    @staticmethod
    def command(source, current, old, new):
        return ["--reference", str(source), "--reference-sha256", hashlib.sha256(old).hexdigest(),
                "--reference-fields", "65", "--reference-codec", "cp936", "--current", str(current),
                "--current-sha256", hashlib.sha256(new).hexdigest(), "--current-version", "7937",
                "--current-codec", "utf-8", "--columns", "1,2"]

    @staticmethod
    def run_cli(arguments, script=None):
        script = script or Path(__file__).parent / "inspect_text_columns.py"
        return subprocess.run([sys.executable, "-B", str(script), *arguments], capture_output=True, text=True,
                              encoding="utf-8", timeout=10, check=False, cwd=script.parent,
                              env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONPATH=""))

    def test_actual_cli_aggregate_and_opt_in_indices_are_private_read_only_evidence(self):
        source_rows, current_rows = pair(); current_rows[0][66] = b"\xff"
        old, new = render(source_rows), render(current_rows)
        with tempfile.TemporaryDirectory(prefix="PRIVATE_COLUMN_PATH_991731041-") as temporary:
            root = Path(temporary); source = root / "reference.secret.decoded"; current = root / "current.secret.decoded"
            source.write_bytes(old); current.write_bytes(new)
            before = {path: path.read_bytes() for path in root.iterdir()}
            arguments = self.command(source, current, old, new)
            result = self.run_cli(arguments)
            self.assertEqual(0, result.returncode, result.stderr); self.assertEqual("", result.stderr)
            report = json.loads(result.stdout)
            expected = {"status", "referenceRows", "currentRows", "sharedIdentities", "referenceOnlyIdentities",
                        "currentOnlyIdentities", "sourceColumns", "currentColumns", "unsupportedReferenceColumns",
                        "unsupportedCurrentColumns", "columnsWithoutExactCandidate", "columnsWithOneExactCandidate",
                        "columnsWithMultipleExactCandidates", "canonicalVectorBytes", "referenceCodec", "currentCodec",
                        "semanticTextRolesProved", "clientApplied", "chosenMappingProduced"}
            self.assertEqual(expected, set(report)); self.assertEqual(1, report["unsupportedCurrentColumns"])
            self.assertFalse(report["chosenMappingProduced"]); self.assertFalse(report["semanticTextRolesProved"])
            detailed = self.run_cli([*arguments, "--column-evidence"])
            self.assertEqual(0, detailed.returncode, detailed.stderr)
            evidence = json.loads(detailed.stdout)
            self.assertEqual(expected | {"columnEvidence"}, set(evidence))
            by_index = {row["referenceColumn"]: row for row in evidence["columnEvidence"]["columns"]}
            self.assertEqual({"referenceColumn": 1, "status": "Supported", "candidateColumns": [12]}, by_index[1])
            self.assertEqual({"referenceColumn": 2, "status": "Supported", "candidateColumns": [9]}, by_index[2])
            self.assertEqual([{"currentColumn": 66, "status": "Undecodable"}], evidence["columnEvidence"]["unsupportedCurrentColumns"])
            for output in (result.stdout, detailed.stdout):
                for secret in (str(root), str(source), "PRIVATE_ROW_LABEL", "991731041", "991731042", "opaque-column"):
                    self.assertNotIn(secret, output)
            self.assertEqual(before, {path: path.read_bytes() for path in root.iterdir()})

    def test_actual_cli_requires_explicit_arguments_and_sanitizes_refusals_without_writes(self):
        old, new = map(render, pair())
        with tempfile.TemporaryDirectory(prefix="PRIVATE_COLUMN_REFUSAL-") as temporary:
            root = Path(temporary); source = root / "reference.decoded"; current = root / "current.decoded"
            source.write_bytes(old); current.write_bytes(new)
            good = self.command(source, current, old, new)
            cases = []
            for flag in ("--reference-fields", "--reference-codec", "--current-codec", "--current-version", "--columns"):
                index = good.index(flag); cases.append(good[:index] + good[index + 2:])
            for flag, value in (("--columns", "1,1"), ("--columns", "0"), ("--columns", "65"),
                                ("--columns", "PRIVATE_ARGUMENT_VALUE"), ("--current-version", "7931"),
                                ("--reference-codec", "gbk"), ("--current-codec", "CP936"),
                                ("--reference-sha256", "0" * 64), ("--reference", str(root / "MISSING_PRIVATE_PATH"))):
                changed = list(good); changed[changed.index(flag) + 1] = value; cases.append(changed)
            cases.append([*good, "--write-map", str(root / "PRIVATE_OUTPUT")])
            before = {path: path.read_bytes() for path in root.iterdir()}
            for arguments in cases:
                with self.subTest(arguments=arguments):
                    result = self.run_cli(arguments)
                    self.assertNotEqual(0, result.returncode); self.assertEqual("", result.stderr)
                    report = json.loads(result.stdout)
                    self.assertEqual({"status", "errorType", "clientApplied"}, set(report))
                    self.assertEqual("Refused", report["status"]); self.assertFalse(report["clientApplied"])
                    for private in (str(root), "PRIVATE_ARGUMENT_VALUE", "PRIVATE_OUTPUT", "PRIVATE_ROW_LABEL"):
                        self.assertNotIn(private, result.stdout)
                    self.assertEqual(before, {path: path.read_bytes() for path in root.iterdir()})

    def test_actual_cli_missing_support_returns_only_generic_refusal(self):
        with tempfile.TemporaryDirectory(prefix="PRIVATE_MISSING_COLUMN_SUPPORT-") as temporary:
            root = Path(temporary); script = root / "inspect_text_columns.py"
            shutil.copyfile(Path(__file__).parent / script.name, script)
            before = script.read_bytes()
            result = self.run_cli([], script)
            self.assertNotEqual(0, result.returncode); self.assertEqual("", result.stderr)
            self.assertEqual({"status": "Refused", "errorType": "ModuleNotFoundError", "clientApplied": False}, json.loads(result.stdout))
            self.assertNotIn(str(root), result.stdout)
            self.assertEqual({script}, set(root.iterdir())); self.assertEqual(before, script.read_bytes())


if __name__ == "__main__":
    unittest.main()
