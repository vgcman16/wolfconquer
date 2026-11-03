"""Synthetic sequential core-UI proposals; no actual client keys or data."""
import json
from pathlib import Path
import unittest

import test_storage_vendor_ui as generic

FILENAME, FAMILY = "core-ui-family.json", "core-ui"
LOGIN, SETTINGS = "STR_SYNTHETIC_ENTRY", "STR_SYNTHETIC_VOLUME"
OLD_FAMILIES = {
    "stat-ui-family.json": "character-stat-ui",
    "gear-ui-family.json": "equipment-ui",
    "combined-ui-family.json": "character-equipment-ui",
    "mail-trade-ui-family.json": "mail-trade-ui",
    "storage-vendor-ui-family.json": "storage-vendor-ui",
    "quest-social-ui-family.json": "quest-social-ui",
    "guild-team-ui-family.json": "guild-team-ui",
}


class CoreUiTests(unittest.TestCase):
    @staticmethod
    def documents():
        original = (f";合成样例\r\nSTR_SYNTHETIC_PRIOR=先前:%s\n{LOGIN}=入口:%s\r"
                    f"STR_KEEP=原文\r\n{SETTINGS}=音量:%d%%\n{SETTINGS}=音量:%d%%").encode()
        donor = (f";unused donor\r\nSTR_SYNTHETIC_PRIOR=Wrong donor:%s\n{LOGIN}=Entry:%s\r"
                 f"STR_KEEP=Unused donor\r\n{SETTINGS}=Volume:%d%%\n{SETTINGS}=Volume:%d%%").encode()
        current = b"\xef\xbb\xbf" + original.replace("先前:%s".encode(), b"Earlier:%s").replace(
            "STR_KEEP=原文".encode(), "STR_KEEP=目标保留".encode())
        first = current.replace("入口:%s".encode(), b"Entry:%s")
        final = first.replace("音量:%d%%".encode(), b"Volume:%d%%")
        return (original, donor, current), first, final

    def test_actual_two_pass_cli_retains_prior_english_duplicates_and_every_unselected_byte(self):
        documents, first, final = self.documents()
        support = generic.StorageVendorUiTests
        with support.workspace(self, documents) as (root, tool, paths):
            current_path, current_bytes = paths[2], documents[2]
            outputs = []
            for key, physical_rows, expected in ((LOGIN, 1, first), (SETTINGS, 2, final)):
                inputs = documents[0], documents[1], current_bytes
                actual_paths = [paths[0], paths[1], current_path]
                plan = support.plan(inputs, keys=(key,), family=FAMILY)
                (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
                before = support.snapshot(root)
                checked = support.invoke(tool, actual_paths, FILENAME)
                self.assertEqual(0, checked.returncode, checked.stderr)
                report = json.loads(checked.stdout)
                self.assertEqual(FAMILY, report["family"])
                self.assertEqual((1, physical_rows), (report["mergedKeyCount"], report["changedPhysicalRows"]))
                self.assertEqual({}, report["held"])
                self.assertFalse(report["clientApplied"]); self.assertFalse(report["liveRenderingVerified"])
                self.assertEqual(before, support.snapshot(root))
                written = support.invoke(tool, actual_paths, FILENAME, write=True)
                self.assertEqual(0, written.returncode, written.stderr)
                output = Path(json.loads(written.stdout)["proposalDirectory"])
                self.assertEqual(tool / "proposals", output.parent)
                self.assertEqual(expected, (output / "Cn_Res.ini").read_bytes())
                self.assertEqual(current_bytes, current_path.read_bytes())
                outputs.append(output)
                current_path, current_bytes = output / "Cn_Res.ini", expected
            self.assertNotEqual(*outputs)
            self.assertEqual(set(outputs), set((tool / "proposals").iterdir()))
            self.assertEqual(first, (outputs[0] / "Cn_Res.ini").read_bytes())
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())

    def test_seven_previous_pairs_still_work_but_cross_family_or_unapproved_filenames_never_write(self):
        documents, _, _ = self.documents()
        support = generic.StorageVendorUiTests
        with support.workspace(self, documents) as (root, tool, paths):
            for old_filename, old_family in OLD_FAMILIES.items():
                with self.subTest(valid_old_filename=old_filename):
                    (tool / old_filename).write_text(json.dumps(support.plan(documents, keys=(LOGIN,), family=old_family)), encoding="utf-8")
                    before = support.snapshot(root)
                    checked = support.invoke(tool, paths, old_filename)
                    self.assertEqual(0, checked.returncode, checked.stderr)
                    report = json.loads(checked.stdout)
                    self.assertEqual(old_family, report["family"])
                    self.assertEqual((1, 1), (report["mergedKeyCount"], report["changedPhysicalRows"]))
                    self.assertEqual({}, report["held"])
                    self.assertEqual(before, support.snapshot(root))
                for filename, family in ((FILENAME, old_family), (old_filename, FAMILY)):
                    with self.subTest(filename=filename, declared_family=family):
                        (tool / filename).write_text(json.dumps(support.plan(documents, keys=(LOGIN,), family=family)), encoding="utf-8")
                        before = support.snapshot(root)
                        refused = support.invoke(tool, paths, filename, write=True)
                        self.assertNotEqual(0, refused.returncode); self.assertEqual("", refused.stdout)
                        self.assertIn("namedResourcePlanFamilyMismatch", refused.stderr)
                        self.assertEqual(before, support.snapshot(root))
            for filename in ("core-family.json", "../" + FILENAME):
                before = support.snapshot(root)
                refused = support.invoke(tool, paths, filename, write=True)
                self.assertNotEqual(0, refused.returncode); self.assertEqual("", refused.stdout)
                self.assertEqual(before, support.snapshot(root))

    def test_two_hundred_then_one_is_allowed_but_one_201_key_pass_or_stale_current_pin_refuses(self):
        support = generic.StorageVendorUiTests
        keys = tuple(f"STR_SYNTHETIC_BATCH_{index:03}" for index in range(201))
        original = "".join(f"{key}=合成{index}:%d\r\n" for index, key in enumerate(keys)).encode()
        donor = b"".join(f"{key}=Synthetic{index}:%d\r\n".encode() for index, key in enumerate(keys))
        documents = original, donor, original
        first = b"".join((f"{key}=Synthetic{index}:%d\r\n".encode() if index < 200 else
                           f"{key}=合成{index}:%d\r\n".encode()) for index, key in enumerate(keys))
        with support.workspace(self, documents) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(support.plan(documents, keys=keys[:200], family=FAMILY)), encoding="utf-8")
            written = support.invoke(tool, paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            report = json.loads(written.stdout)
            self.assertEqual((200, 200), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            first_path = Path(report["proposalDirectory"]) / "Cn_Res.ini"
            self.assertEqual(first, first_path.read_bytes())
            inputs, actual_paths = (original, donor, first), [paths[0], paths[1], first_path]

            # Same next key, deliberately old current pin: no directory may appear.
            (tool / FILENAME).write_text(json.dumps(support.plan(documents, keys=keys[200:], family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            refused = support.invoke(tool, actual_paths, FILENAME, write=True)
            self.assertNotEqual(0, refused.returncode); self.assertEqual("", refused.stdout)
            self.assertIn("namedResourceInputPinMismatch", refused.stderr)
            self.assertEqual(before, support.snapshot(root))

            (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=keys[200:], family=FAMILY)), encoding="utf-8")
            written = support.invoke(tool, actual_paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            report = json.loads(written.stdout)
            self.assertEqual((1, 1), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            final_path = Path(report["proposalDirectory"]) / "Cn_Res.ini"
            self.assertNotEqual(first_path.parent, final_path.parent)
            self.assertEqual(donor, final_path.read_bytes()); self.assertEqual(first, first_path.read_bytes())

            (tool / FILENAME).write_text(json.dumps(support.plan(inputs, keys=keys, family=FAMILY)), encoding="utf-8")
            before = support.snapshot(root)
            refused = support.invoke(tool, actual_paths, FILENAME, write=True)
            self.assertNotEqual(0, refused.returncode); self.assertEqual("", refused.stdout)
            self.assertIn("namedResourcePlanShape", refused.stderr)
            self.assertEqual(before, support.snapshot(root))
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())


if __name__ == "__main__":
    unittest.main()
