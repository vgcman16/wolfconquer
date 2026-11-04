"""Synthetic literal button-tooltip keys; no client data or resource application."""
import json
from pathlib import Path
import unittest

from named_resources import NamedResources
from propose_named_ui import propose
import test_storage_vendor_ui as generic


FILENAME, FAMILY = "core-ui-family.json", "core-ui"
TOOLTIP, PADDED = "STR_BTN_TIP_12-34", "STR_BTN_TIP_0012-0034"


def proposal(documents, keys=(TOOLTIP,)):
    return propose(*(NamedResources.parse(raw) for raw in documents),
                   generic.StorageVendorUiTests.plan(documents, keys=keys, family=FAMILY))


class ButtonTooltipKeyTests(unittest.TestCase):
    def test_actual_cli_preserves_exact_mixed_case_ascii_resource_name(self):
        support = generic.StorageVendorUiTests
        key = "STR_Synthetic_Tip_7"
        original = (f"{key}=合成导航:%s\r\nSTR_Unselected_8=保留\n").encode()
        english = (f"{key}=Navigation:%s\r\nSTR_Unselected_8=Unused donor\n").encode()
        current = b"\xef\xbb\xbf" + original
        documents = original, english, current
        expected = current.replace("合成导航:%s".encode(), b"Navigation:%s")
        with support.workspace(self, documents) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(documents, keys=(key,), family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            checked = support.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            self.assertEqual([key], json.loads(checked.stdout)["mergedKeys"])
            self.assertEqual(before, support.snapshot(root))
            written = support.invoke(tool, paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            report = json.loads(written.stdout)
            output = Path(report["proposalDirectory"])
            self.assertEqual(tool / "proposals", output.parent)
            self.assertEqual(expected, (output / "Cn_Res.ini").read_bytes())
            self.assertEqual([key], report["mergedKeys"])
            self.assertFalse(report["clientApplied"]); self.assertFalse(report["liveRenderingVerified"])
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())

    def test_case_colliding_plan_names_refuse_before_row_selection_or_output(self):
        support = generic.StorageVendorUiTests
        keys = "STR_Synthetic_Tip", "STR_SYNTHETIC_TIP"
        original = current = f"{keys[0]}=原始提示\n{keys[1]}=另一提示\r\n".encode()
        english = f"{keys[0]}=First tip\n{keys[1]}=Second tip\r\n".encode()
        documents = original, english, current
        with self.assertRaisesRegex(ValueError, "namedResourcePlanKeys"):
            proposal(documents, keys)
        with support.workspace(self, documents) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(documents, keys=keys, family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            refused = support.invoke(tool, paths, FILENAME, write=True)
            self.assertNotEqual(0, refused.returncode)
            self.assertEqual("", refused.stdout)
            self.assertIn("namedResourcePlanKeys", refused.stderr)
            self.assertEqual(before, support.snapshot(root))

    def test_existing_api_keeps_leading_zero_tooltips_distinct_and_unselected_aliases_exact(self):
        original = (f"{TOOLTIP}=短提示:%d\r\n{PADDED}=长提示:%s\n"
                    "STR_BTN_TIP_9-9=未选原文\rstr_btn_tip_9-9=另一原文").encode()
        english = (f"{TOOLTIP}=Short tip:%d\r\n{PADDED}=Long tip:%s\n"
                   "STR_BTN_TIP_9-9=Unused donor\rstr_btn_tip_9-9=Another donor").encode()
        current = b"\xef\xbb\xbf" + original.replace("未选原文".encode(), "保留目标".encode())
        expected = current.replace("短提示:%d".encode(), b"Short tip:%d").replace(
            "长提示:%s".encode(), b"Long tip:%s")
        after, report = proposal((original, english, current), (TOOLTIP, PADDED))
        self.assertEqual(expected, after)
        self.assertEqual((2, 2), (report["mergedKeyCount"], report["changedPhysicalRows"]))
        self.assertEqual(sorted((TOOLTIP, PADDED)), report["mergedKeys"])
        self.assertEqual({}, report["held"])
        self.assertFalse(report["clientApplied"]); self.assertFalse(report["liveRenderingVerified"])

    def test_actual_cli_preserves_duplicate_tooltips_bom_endings_prior_english_and_inputs(self):
        support = generic.StorageVendorUiTests
        original = (f";合成样例\r\nSTR_PRIOR=先前:%s\n{TOOLTIP}=提示:%d\r"
                    f"STR_OPAQUE_4-5=保留\r\n{TOOLTIP}=提示:%d").encode()
        english = (f";unused donor\r\nSTR_PRIOR=Wrong donor:%s\n{TOOLTIP}=Tip:%d\r"
                   f"STR_OPAQUE_4-5=Unused donor\r\n{TOOLTIP}=Tip:%d").encode()
        current = b"\xef\xbb\xbf" + original.replace("先前:%s".encode(), b"Earlier:%s")
        documents = original, english, current
        expected = current.replace("提示:%d".encode(), b"Tip:%d")
        with support.workspace(self, documents) as (root, tool, paths):
            plan = support.plan(documents, keys=(TOOLTIP,), family=FAMILY)
            (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
            before = support.snapshot(root)
            checked = support.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual((1, 2), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual({}, report["held"])
            self.assertEqual(before, support.snapshot(root))
            written = support.invoke(tool, paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            report = json.loads(written.stdout)
            output = Path(report["proposalDirectory"])
            self.assertEqual(tool / "proposals", output.parent)
            self.assertEqual(expected, (output / "Cn_Res.ini").read_bytes())
            self.assertFalse(report["clientApplied"]); self.assertFalse(report["liveRenderingVerified"])
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())

    def test_other_hyphens_noncanonical_digits_and_punctuation_are_not_plan_keys(self):
        invalid = ("STR_OTHER_12-34", "STR_BTN_TIP_-34", "STR_BTN_TIP_12-",
                   "STR_BTN_TIP_12--34", "STR_BTN_TIP_12-34-56", "STR_BTN_TIP_+12-34",
                   "STR_BTN_TIP_12.0-34", "STR_BTN_TIP_A-34", "STR_BTN_TIP_12-3A",
                   "STR_BTN_TIP_１２-34", "STR_BTN_TIP_12–34", "str_btn_tip_12-34", "STR_BTN_tip_12-34", "STR_合成提示",
                   "STR_BTN_TIP_12-34/X", "STR_BTN_TIP_12-34 ")
        for key in invalid:
            with self.subTest(key=key):
                original = current = f"{key}=提示:%d\r\n".encode()
                english = f"{key}=Tip:%d\r\n".encode()
                with self.assertRaisesRegex(ValueError, "namedResourcePlanKeys"):
                    proposal((original, english, current), (key,))

    def test_selected_tooltip_case_alias_in_any_input_is_held(self):
        original = current = f"{TOOLTIP}=提示:%d\r\n".encode()
        english = f"{TOOLTIP}=Tip:%d\r\n".encode()
        for role in range(3):
            documents = [original, english, current]
            documents[role] += f"{TOOLTIP.lower()}=别名:%d\n".encode()
            with self.subTest(role=role):
                after, report = proposal(documents)
                self.assertEqual(documents[2], after)
                self.assertEqual(0, report["mergedKeyCount"])
                self.assertEqual([TOOLTIP], report["held"]["caseAliasAmbiguous"])

    def test_literal_tooltips_retain_format_machine_and_duplicate_guards(self):
        cases = (
            ("提示:%d", "Tip:%s", "提示:%d", "formatSignatureChanged"),
            ("https://example.invalid/中文", "English", "https://example.invalid/中文", "machineOrNonDisplayValue"),
            ("提示:%q", "Tip:%q", "提示:%q", "unsupportedFormatSyntax"),
        )
        for source, donor, current, reason in cases:
            documents = tuple(f"{TOOLTIP}={value}\r\n".encode() for value in (source, donor, current))
            with self.subTest(reason=reason):
                after, report = proposal(documents)
                self.assertEqual(documents[2], after)
                self.assertEqual([TOOLTIP], report["held"][reason])
        original = current = (f"{TOOLTIP}=提示:%d\n" * 2).encode()
        english = (f"{TOOLTIP}=Tip:%d\n" * 2).encode()
        changed = current.replace("提示:%d".encode(), "另一含义:%d".encode(), 1)
        after, report = proposal((original, english, changed))
        self.assertEqual(changed, after)
        self.assertEqual([TOOLTIP], report["held"]["nonUniformDuplicateMeaning"])

    def test_actual_cli_rejected_key_or_stale_pin_never_creates_output(self):
        support = generic.StorageVendorUiTests
        for key in ("STR_OTHER_12-34", "STR_BTN_TIP_12-34-56", "STR_BTN_TIP_12–34", TOOLTIP):
            original = current = f"{key}=提示:%d\r\n".encode()
            english = f"{key}=Tip:%d\r\n".encode()
            documents = original, english, current
            with self.subTest(key=key), support.workspace(self, documents) as (root, tool, paths):
                plan = support.plan(documents, keys=(key,), family=FAMILY)
                reason = "namedResourcePlanKeys"
                if key == TOOLTIP:
                    plan["inputSha256"]["current"] = "0" * 64
                    reason = "namedResourceInputPinMismatch"
                (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
                before = support.snapshot(root)
                refused = support.invoke(tool, paths, FILENAME, write=True)
                self.assertNotEqual(0, refused.returncode)
                self.assertEqual("", refused.stdout)
                self.assertIn(reason, refused.stderr)
                self.assertEqual(before, support.snapshot(root))


if __name__ == "__main__":
    unittest.main()
