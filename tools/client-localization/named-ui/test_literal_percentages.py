"""Synthetic fixed-percentage signatures and actual named-UI CLI controls."""
import json
from pathlib import Path
import unittest

from format_tokens import signature
import test_storage_vendor_ui as generic


FILENAME, FAMILY = "core-ui-family.json", "core-ui"
FIRST, SECOND = "STR_SYNTHETIC_PERCENT_ONE", "STR_SYNTHETIC_PERCENT_TWO"


class LiteralPercentageTests(unittest.TestCase):
    def test_exact_numeric_spelling_and_mixed_format_order(self):
        self.assertEqual(
            (("percent", "+10%"), ("arrow", "->"), ("percent", "-0.50%"),
             ("comparison", "<="), ("<", "<color=red>"), ("%", "%d"),
             ("<", "</color>"), ("\\", "\\n"), ("percent", "0%")),
            signature("增幅:+10% -> -0.50% <= <color=red>%d</color>\\n0%"))
        self.assertEqual((("percent", "0010.00%"),), signature("数值:0010.00%"))
        self.assertEqual((("percent", "+10%"),), signature("概率+10%"))

    def test_existing_printf_and_escaped_percent_take_precedence(self):
        for value, expected in (("10%d", "%d"), ("10%%", "%%"), ("10% c", "% c")):
            with self.subTest(value=value):
                self.assertEqual((("%", expected),), signature(value))

    def test_ambiguous_numeric_fragments_and_malformed_syntax_still_refuse(self):
        for value in ("%q", "id10%", "α10%", "Ａ10%", "a\u030110%", "&#10%",
                      "1.2.3%", "1e10%", "1,000%", "0x10%",
                      "10%qabc", "10%2", ">", "->>", "<tag"):
            with self.subTest(value=value):
                with self.assertRaisesRegex(ValueError, "namedResourceUnsupportedFormat"):
                    signature(value)

    def test_actual_two_pass_cli_preserves_prior_and_unselected_bytes(self):
        original = (";源\r\nSTR_SYNTHETIC_PRIOR=之前:%s\n"
                    f"{FIRST}=数值:%d -> +10%\rSTR_KEEP=原文\r\n"
                    f"{SECOND}=比例:-0.50%\n").encode()
        donor = (";donor\r\nSTR_SYNTHETIC_PRIOR=Unused:%s\n"
                 f"{FIRST}=Value:%d -> +10%\rSTR_KEEP=Unused\r\n"
                 f"{SECOND}=Rate:-0.50%\n").encode()
        current = (b"\xef\xbb\xbf;" + "源".encode() + b"\r\nSTR_SYNTHETIC_PRIOR=Earlier:%s\n" +
                   f"{FIRST}=数值:%d -> +10%\rSTR_KEEP=保留\r\n{SECOND}=比例:-0.50%\n".encode())
        first_expected = (b"\xef\xbb\xbf;" + "源".encode() + b"\r\nSTR_SYNTHETIC_PRIOR=Earlier:%s\n" +
                          f"{FIRST}=Value:%d -> +10%\rSTR_KEEP=保留\r\n{SECOND}=比例:-0.50%\n".encode())
        final_expected = (b"\xef\xbb\xbf;" + "源".encode() + b"\r\nSTR_SYNTHETIC_PRIOR=Earlier:%s\n" +
                          f"{FIRST}=Value:%d -> +10%\rSTR_KEEP=保留\r\n{SECOND}=Rate:-0.50%\n".encode())
        documents = (original, donor, current)
        support = generic.StorageVendorUiTests
        with support.workspace(self, documents) as (root, tool, paths):
            current_path, current_bytes = paths[2], current
            outputs = []
            for key, expected in ((FIRST, first_expected), (SECOND, final_expected)):
                inputs = original, donor, current_bytes
                actual_paths = [paths[0], paths[1], current_path]
                (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=(key,), family=FAMILY)), encoding="utf-8")
                before = support.snapshot(root)
                checked = support.invoke(tool, actual_paths, FILENAME)
                self.assertEqual(0, checked.returncode, checked.stderr)
                report = json.loads(checked.stdout)
                self.assertEqual((1, 1), (report["mergedKeyCount"], report["changedPhysicalRows"]))
                self.assertEqual({}, report["held"]); self.assertEqual(before, support.snapshot(root))
                written = support.invoke(tool, actual_paths, FILENAME, write=True)
                self.assertEqual(0, written.returncode, written.stderr)
                output = Path(json.loads(written.stdout)["proposalDirectory"])
                self.assertEqual(expected, (output / "Cn_Res.ini").read_bytes())
                self.assertEqual(current_bytes, current_path.read_bytes())
                outputs.append(output); current_path, current_bytes = output / "Cn_Res.ini", expected
            self.assertNotEqual(*outputs)
            self.assertEqual(first_expected, (outputs[0] / "Cn_Res.ini").read_bytes())
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())

    def test_changed_missing_reordered_or_malformed_percentages_are_held_without_output_files(self):
        original = f"{FIRST}=变化:+10% -> -0.50%\n".encode()
        for donor_value, reason in (("Change: -> -0.50%", "formatSignatureChanged"),
                                    ("Change:+11% -> -0.50%", "formatSignatureChanged"),
                                    ("Change:-0.50% -> +10%", "formatSignatureChanged"),
                                    ("Change:+10%q -> -0.50%", "unsupportedFormatSyntax")):
            donor = f"{FIRST}={donor_value}\n".encode()
            documents = (original, donor, original)
            support = generic.StorageVendorUiTests
            with self.subTest(donor=donor_value), support.workspace(self, documents) as (root, tool, paths):
                (tool / FILENAME).write_text(json.dumps(support.plan(documents, keys=(FIRST,), family=FAMILY)), encoding="utf-8")
                before = support.snapshot(root)
                result = support.invoke(tool, paths, FILENAME)
                self.assertEqual(0, result.returncode, result.stderr)
                report = json.loads(result.stdout)
                self.assertEqual(0, report["mergedKeyCount"])
                self.assertEqual({reason: [FIRST]}, report["held"])
                self.assertEqual(before, support.snapshot(root))
                self.assertFalse((tool / "proposals").exists())
                for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())


if __name__ == "__main__":
    unittest.main()
