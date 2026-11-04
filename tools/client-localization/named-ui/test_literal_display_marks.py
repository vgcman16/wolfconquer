"""Synthetic source-preserved display marks; no client inputs or application."""
import json
from pathlib import Path
import unittest
from unittest import mock

import named_resources as resources
import propose_named_ui as named_ui
import test_storage_vendor_ui as generic


FILENAME, FAMILY = "core-ui-family.json", "core-ui"
HEADING, TIME = "STR_SYNTHETIC_HEADING", "STR_SYNTHETIC_TIME"


def proposal(documents, keys=(HEADING,)):
    return named_ui.propose(*(resources.NamedResources.parse(raw) for raw in documents),
                            generic.StorageVendorUiTests.plan(documents, keys=keys, family=FAMILY))


def documents(source="★【原文】：%d", donor="★【English】：%d"):
    original, english = (f"{HEADING}={value}\r\n".encode() for value in (source, donor))
    return original, english, original


class LiteralDisplayMarkTests(unittest.TestCase):
    def held(self, inputs, reason, keys=(HEADING,)):
        after, report = proposal(inputs, keys)
        self.assertEqual(inputs[2], after)
        self.assertEqual(0, report["mergedKeyCount"])
        self.assertEqual({reason: list(keys)}, report["held"])
        self.assertFalse(report["clientApplied"])
        self.assertFalse(report["liveRenderingVerified"])

    def test_existing_api_preserves_four_marks_and_all_label_time_colons(self):
        source_time = "时段：09：05-10：15\\n入场：08：55\\n"
        donor_time = "Time：09：05-10：15\\nEntry：08：55\\n"
        original = f"{HEADING}=★【标题】：%s\r\n{TIME}={source_time}\n".encode()
        english = f"{HEADING}=★【Heading】：%s\r\n{TIME}={donor_time}\n".encode()
        current = b"\xef\xbb\xbf" + original
        expected = current.replace("★【标题】：%s".encode(), "★【Heading】：%s".encode()).replace(
            source_time.encode(), donor_time.encode())
        after, report = proposal((original, english, current), (HEADING, TIME))
        self.assertEqual(expected, after)
        self.assertEqual((2, 2), (report["mergedKeyCount"], report["changedPhysicalRows"]))
        self.assertEqual({}, report["held"])
        self.assertFalse(report["clientApplied"])
        self.assertFalse(report["liveRenderingVerified"])

    def test_actual_cli_preserves_marks_duplicates_bom_endings_prior_and_unselected_bytes(self):
        support = generic.StorageVendorUiTests
        source_time = "时段：09：05-10：15\\n入场：08：55\\n"
        donor_time = "Time：09：05-10：15\\nEntry：08：55\\n"
        original = (f";合成样例\r\nSTR_PRIOR=原先:%s\n{HEADING}=★【标题】：%s\r"
                    f"STR_KEEP=保留\r\n{TIME}={source_time}\n{HEADING}=★【标题】：%s").encode()
        english = (f";unused donor\r\nSTR_PRIOR=Wrong donor:%s\n{HEADING}=★【Heading】：%s\r"
                   f"STR_KEEP=Unused donor\r\n{TIME}={donor_time}\n{HEADING}=★【Heading】：%s").encode()
        current = b"\xef\xbb\xbf" + original.replace("原先:%s".encode(), b"Earlier:%s").replace(
            "STR_KEEP=保留".encode(), "STR_KEEP=目标保留".encode())
        inputs = original, english, current
        expected = current.replace("★【标题】：%s".encode(), "★【Heading】：%s".encode()).replace(
            source_time.encode(), donor_time.encode())
        with support.workspace(self, inputs) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=(HEADING, TIME), family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            checked = support.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual((2, 3), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual({}, report["held"])
            self.assertEqual(before, support.snapshot(root))
            outputs = []
            for _ in range(2):
                written = support.invoke(tool, paths, FILENAME, write=True)
                self.assertEqual(0, written.returncode, written.stderr)
                report = json.loads(written.stdout)
                output = Path(report["proposalDirectory"])
                self.assertEqual(tool / "proposals", output.parent)
                self.assertEqual(expected, (output / "Cn_Res.ini").read_bytes())
                self.assertFalse(report["clientApplied"])
                self.assertFalse(report["liveRenderingVerified"])
                outputs.append(output)
            self.assertNotEqual(*outputs)
            self.assertEqual(set(outputs), set((tool / "proposals").iterdir()))
            self.assertEqual(expected, (outputs[0] / "Cn_Res.ini").read_bytes())
            for path, raw in zip(paths, inputs):
                self.assertEqual(raw, path.read_bytes())

    def test_each_finite_mark_keeps_order_without_imposing_word_positions(self):
        for mark in "★【】：":
            with self.subTest(mark=mark):
                inputs = documents(f"{mark}原文%d", f"Value%d{mark}")
                after, report = proposal(inputs)
                self.assertEqual(inputs[1], after)
                self.assertEqual((1, 1), (report["mergedKeyCount"], report["changedPhysicalRows"]))
                self.assertEqual({}, report["held"])

    def test_all_ascii_donor_compatibility_including_source_marks_and_tab_remains(self):
        for source, donor in (("★【原文】：%d", "English:%d"), ("原文\t%d", "English\t%d")):
            with self.subTest(donor=donor):
                inputs = documents(source, donor)
                after, report = proposal(inputs)
                self.assertEqual(inputs[1], after)
                self.assertEqual({}, report["held"])
                self.assertEqual(1, report["mergedKeyCount"])

    def test_partial_added_missing_and_reordered_mark_projections_are_held(self):
        cases = (
            ("★【原文】：%d", "★★【English】：%d"),
            ("★【原文】：%d", "★English：%d"),
            ("★【原文】：%d", "【★English】：%d"),
            ("★【原文】：%d", "★【English】:%d"),
            ("原文:%d", "English：%d"),
            ("时段：09：05-10：15\\n入场：08：55\\n", "Time:09：05-10：15\\nEntry:08：55\\n"),
        )
        for source, donor in cases:
            with self.subTest(donor=donor):
                self.held(documents(source, donor), "englishDonorRequired")

    def test_unicode_letters_digits_lookalikes_punctuation_cjk_and_bidi_are_held(self):
        invalid = ("é", "α", "А", "１", "١", "，", "；", "·", "“", "☆", "😀", "中",
                   "\u00a0", "\u200b", "\u202e", "\u2066", "\x0b", "\x7f", "\x85")
        for character in invalid:
            with self.subTest(character=repr(character)):
                self.held(documents(donor=f"★【English{character}】：%d"), "englishDonorRequired")

    def test_predicate_refuses_row_controls_and_requires_an_ascii_letter(self):
        for donor in ("★【】：123", "123\t", "★【English】：\r", "★【English】：\n",
                      "★【English】：\0", "★【English】：\x1b"):
            with self.subTest(donor=repr(donor)):
                self.assertFalse(named_ui.english_donor("★【原文】：123", donor))
        self.held(documents("★【原文】：123", "★【】：123"), "englishDonorRequired")

    def test_existing_format_and_machine_holds_still_apply_to_admitted_marks(self):
        cases = (
            ("★【原文】：%d", "★【English】：%s", "formatSignatureChanged"),
            ("★【原文】：%q", "★【English】：%q", "unsupportedFormatSyntax"),
            ("★【<color=red>原文</color>】：%d", "★【<color=blue>English</color>】：%d", "formatSignatureChanged"),
            ("★【原文】：100%", "★【English】：101%", "formatSignatureChanged"),
            ("★【路径】：https://example.invalid/中文", "★【Path】：English", "machineOrNonDisplayValue"),
            ("★【路径】：原文", "★【Path】：https://example.invalid/", "machineOrNonDisplayValue"),
        )
        for source, donor, reason in cases:
            with self.subTest(reason=reason, donor=donor):
                self.held(documents(source, donor), reason)

    def test_existing_duplicate_case_alias_missing_and_changed_current_holds_remain(self):
        base = documents()
        self.held((base[0] * 2, base[1], base[2] * 2), "occurrenceCountChanged")
        self.held((base[0] * 2, base[1] + base[1].replace(b"English", b"Other"), base[2] * 2),
                  "nonUniformDuplicateMeaning")
        self.held((base[0], base[1].replace(HEADING.encode(), b"STR_SYNTHETIC_OTHER"), base[2]), "requiredKeyMissing")
        self.held((base[0], base[1], base[2].replace("原文".encode(), "另一原文".encode())), "currentChineseMeaningChanged")
        for role in range(3):
            inputs = list(base)
            inputs[role] += f"{HEADING.lower()}=别名\n".encode()
            with self.subTest(role=role):
                self.held(inputs, "caseAliasAmbiguous")

    def test_actual_cli_unicode_holds_are_read_only_and_leave_every_input_unchanged(self):
        support = generic.StorageVendorUiTests
        inputs = documents(donor="★【English，】：%d")
        with support.workspace(self, inputs) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=(HEADING,), family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            checked = support.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual(0, report["mergedKeyCount"])
            self.assertEqual({"englishDonorRequired": [HEADING]}, report["held"])
            self.assertEqual(named_ui.digest(inputs[2]), report["proposalSha256"])
            self.assertEqual(before, support.snapshot(root))

    def test_actual_cli_pin_plan_family_key_count_and_path_refusals_create_no_output(self):
        support = generic.StorageVendorUiTests
        inputs = documents()
        for case in ("pin", "family", "shape", "key", "casefold", "count", "filename", "plan-size"):
            with self.subTest(case=case), support.workspace(self, inputs) as (root, tool, paths):
                plan = support.plan(inputs, keys=(HEADING,), family=FAMILY)
                filename, reason = FILENAME, "namedResourcePlanShape"
                if case == "pin":
                    plan["inputSha256"]["current"] = "0" * 64
                    reason = "namedResourceInputPinMismatch"
                elif case == "family":
                    plan["family"] = "equipment-ui"
                    reason = "namedResourcePlanFamilyMismatch"
                elif case == "shape":
                    plan["codec"] = "gbk"
                elif case == "key":
                    plan["keys"] = ["STR_OTHER_1-2"]
                    reason = "namedResourcePlanKeys"
                elif case == "casefold":
                    plan["keys"] = [HEADING, "STR_Synthetic_Heading"]
                    reason = "namedResourcePlanKeys"
                elif case == "count":
                    plan["keys"] = [f"STR_SYNTHETIC_{index}" for index in range(201)]
                elif case == "filename":
                    filename, reason = "../" + FILENAME, "invalid choice"
                elif case == "plan-size":
                    reason = "namedResourcePlanBounds"
                raw = json.dumps(plan).encode()
                if case == "plan-size":
                    raw += b" " * (65537 - len(raw))
                (tool / FILENAME).write_bytes(raw)
                before = support.snapshot(root)
                refused = support.invoke(tool, paths, filename, write=True)
                self.assertNotEqual(0, refused.returncode)
                self.assertEqual("", refused.stdout)
                self.assertIn(reason, refused.stderr)
                self.assertEqual(before, support.snapshot(root))

    def test_actual_cli_existing_output_file_is_never_overwritten(self):
        support = generic.StorageVendorUiTests
        inputs = documents()
        with support.workspace(self, inputs) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=(HEADING,), family=FAMILY)), encoding="utf-8")
            (tool / "proposals").write_bytes(b"Existing output sentinel\n")
            before = support.snapshot(root)
            refused = support.invoke(tool, paths, FILENAME, write=True)
            self.assertNotEqual(0, refused.returncode)
            self.assertEqual("", refused.stdout)
            self.assertIn("FileExistsError", refused.stderr)
            self.assertEqual(before, support.snapshot(root))

    def test_input_and_proposed_byte_bounds_remain_with_mark_preservation(self):
        base = documents()
        with mock.patch.object(resources, "MAX_BYTES", len(base[0]) - 1):
            with self.assertRaisesRegex(ValueError, "namedResourceBoundsOrNul"):
                proposal(base)
        original = base[0] * 2
        english = base[1].replace(b"English", b"English" * 30) * 2
        current = b";" + b"x" * len(english) + b"\n" + original
        inputs = original, english, current
        with mock.patch.object(resources, "MAX_BYTES", max(map(len, inputs))):
            with self.assertRaisesRegex(ValueError, "namedResourceProposedBounds"):
                proposal(inputs)


if __name__ == "__main__":
    unittest.main()
