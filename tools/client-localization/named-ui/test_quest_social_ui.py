"""Narrow synthetic quest/social CLI controls, reusing generic fixture support."""
import json
from pathlib import Path
import unittest

import test_storage_vendor_ui as generic

FILENAME = "quest-social-ui-family.json"
FAMILY = "quest-social-ui"
KEYS = ("STR_SYNTHETIC_QUEST", "STR_SYNTHETIC_SOCIAL")


class QuestSocialUiTests(unittest.TestCase):
    @staticmethod
    def documents():
        original = "STR_SYNTHETIC_QUEST=提示:%d\r\nSTR_SYNTHETIC_SOCIAL=对象:%s\nSTR_KEEP=原文\r\n".encode()
        english = b"STR_SYNTHETIC_QUEST=Hint:%d\r\nSTR_SYNTHETIC_SOCIAL=Peer:%s\nSTR_KEEP=Unused donor\r\n"
        current = b"\xef\xbb\xbf" + original.replace("STR_KEEP=原文".encode(), "STR_KEEP=目标保留".encode())
        golden = current.replace("提示:%d".encode(), b"Hint:%d").replace("对象:%s".encode(), b"Peer:%s")
        return (original, english, current), golden

    def test_actual_quest_social_cli_defaults_to_no_write_then_creates_exact_proposal(self):
        documents, golden = self.documents()
        with generic.StorageVendorUiTests.workspace(self, documents) as (root, tool, paths):
            plan = generic.StorageVendorUiTests.plan(documents, keys=KEYS, family=FAMILY)
            (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
            before = generic.StorageVendorUiTests.snapshot(root)
            checked = generic.StorageVendorUiTests.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual(FAMILY, report["family"])
            self.assertEqual((2, 2), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual({}, report["held"])
            self.assertFalse(report["clientApplied"]); self.assertFalse(report["liveRenderingVerified"])
            self.assertEqual(before, generic.StorageVendorUiTests.snapshot(root))
            written = generic.StorageVendorUiTests.invoke(tool, paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            output = Path(json.loads(written.stdout)["proposalDirectory"])
            self.assertEqual(tool / "proposals", output.parent)
            self.assertEqual(golden, (output / "Cn_Res.ini").read_bytes())
            self.assertEqual({output}, set((tool / "proposals").iterdir()))
            for path, raw in zip(paths, documents): self.assertEqual(raw, path.read_bytes())

    def test_actual_cli_refuses_new_and_existing_family_filename_mismatch_before_writes(self):
        documents, _ = self.documents()
        with generic.StorageVendorUiTests.workspace(self, documents) as (root, tool, paths):
            for filename, family in ((FILENAME, "storage-vendor-ui"), ("storage-vendor-ui-family.json", FAMILY)):
                with self.subTest(filename=filename, family=family):
                    plan = generic.StorageVendorUiTests.plan(documents, keys=KEYS, family=family)
                    (tool / filename).write_text(json.dumps(plan), encoding="utf-8")
                    before = generic.StorageVendorUiTests.snapshot(root)
                    refused = generic.StorageVendorUiTests.invoke(tool, paths, filename, write=True)
                    self.assertNotEqual(0, refused.returncode)
                    self.assertEqual("", refused.stdout)
                    self.assertIn("namedResourcePlanFamilyMismatch", refused.stderr)
                    self.assertEqual(before, generic.StorageVendorUiTests.snapshot(root))


if __name__ == "__main__":
    unittest.main()
