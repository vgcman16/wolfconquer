"""Synthetic guild/team CLI controls; no game resource keys or client application."""
import json
from pathlib import Path
import unittest

import test_storage_vendor_ui as generic

FILENAME = "guild-team-ui-family.json"
FAMILY = "guild-team-ui"
GUILD, TEAM = "STR_SYNTHETIC_GUILD_BADGE", "STR_SYNTHETIC_TEAM_INVITE"
OLD_FAMILIES = {
    "stat-ui-family.json": "character-stat-ui",
    "gear-ui-family.json": "equipment-ui",
    "combined-ui-family.json": "character-equipment-ui",
    "mail-trade-ui-family.json": "mail-trade-ui",
    "storage-vendor-ui-family.json": "storage-vendor-ui",
    "quest-social-ui-family.json": "quest-social-ui",
}


class GuildTeamUiTests(unittest.TestCase):
    @staticmethod
    def documents():
        original = (f";合成样例\r\n{GUILD}=徽章:%d\nSTR_KEEP=原文\r"
                    f"{TEAM}=<color=green>邀约:%s</color>\r\n{GUILD}=徽章:%d").encode()
        donor = (f";unused donor comment\r\n{GUILD}=Badge:%d\nSTR_KEEP=Unused donor\r"
                 f"{TEAM}=<color=green>Invite:%s</color>\r\n{GUILD}=Badge:%d").encode()
        current = b"\xef\xbb\xbf" + original.replace("STR_KEEP=原文".encode(), "STR_KEEP=目标保留".encode())
        golden = current.replace("徽章:%d".encode(), b"Badge:%d").replace("邀约:%s".encode(), b"Invite:%s")
        return (original, donor, current), golden

    def test_actual_guild_team_cli_creates_exact_proposal_after_inert_default(self):
        documents, golden = self.documents()
        support = generic.StorageVendorUiTests
        with support.workspace(self, documents) as (root, tool, paths):
            # Selected order is deliberate and need not be globally sorted.
            plan = support.plan(documents, keys=(TEAM, GUILD), family=FAMILY)
            (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
            before = support.snapshot(root)
            checked = support.invoke(tool, paths, FILENAME)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual(FAMILY, report["family"])
            self.assertEqual((2, 3), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual({}, report["held"])
            self.assertFalse(report["clientApplied"])
            self.assertFalse(report["liveRenderingVerified"])
            self.assertEqual(before, support.snapshot(root))

            written = support.invoke(tool, paths, FILENAME, write=True)
            self.assertEqual(0, written.returncode, written.stderr)
            output = Path(json.loads(written.stdout)["proposalDirectory"])
            self.assertEqual(tool / "proposals", output.parent)
            self.assertEqual(golden, (output / "Cn_Res.ini").read_bytes())
            self.assertEqual({output}, set((tool / "proposals").iterdir()))
            for path, raw in zip(paths, documents):
                self.assertEqual(raw, path.read_bytes())

    def test_all_six_old_pairs_still_work_and_new_pair_mismatches_refuse_without_writes(self):
        documents, _ = self.documents()
        support = generic.StorageVendorUiTests
        with support.workspace(self, documents) as (root, tool, paths):
            for old_filename, old_family in OLD_FAMILIES.items():
                with self.subTest(valid_old_filename=old_filename):
                    plan = support.plan(documents, keys=(TEAM, GUILD), family=old_family)
                    (tool / old_filename).write_text(json.dumps(plan), encoding="utf-8")
                    before = support.snapshot(root)
                    checked = support.invoke(tool, paths, old_filename)
                    self.assertEqual(0, checked.returncode, checked.stderr)
                    report = json.loads(checked.stdout)
                    self.assertEqual(old_family, report["family"])
                    self.assertEqual((2, 3), (report["mergedKeyCount"], report["changedPhysicalRows"]))
                    self.assertEqual({}, report["held"])
                    self.assertEqual(before, support.snapshot(root))

                for filename, family in ((FILENAME, old_family), (old_filename, FAMILY)):
                    with self.subTest(filename=filename, declared_family=family):
                        plan = support.plan(documents, keys=(TEAM, GUILD), family=family)
                        (tool / filename).write_text(json.dumps(plan), encoding="utf-8")
                        before = support.snapshot(root)
                        refused = support.invoke(tool, paths, filename, write=True)
                        self.assertNotEqual(0, refused.returncode)
                        self.assertEqual("", refused.stdout)
                        self.assertIn("namedResourcePlanFamilyMismatch", refused.stderr)
                        self.assertEqual(before, support.snapshot(root))

            for filename in ("guild-team-family.json", "../" + FILENAME):
                with self.subTest(unapproved_filename=filename):
                    before = support.snapshot(root)
                    refused = support.invoke(tool, paths, filename, write=True)
                    self.assertNotEqual(0, refused.returncode)
                    self.assertEqual("", refused.stdout)
                    self.assertEqual(before, support.snapshot(root))


if __name__ == "__main__":
    unittest.main()
