"""Synthetic equipment-family qualification; no proprietary table content."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from named_resources import NamedResources
from propose_named_ui import propose
from test_named_ui import plan_for, table


class EquipmentUiTests(unittest.TestCase):
    def test_family_reports_only_caller_supplied_english_and_preserves_ordered_markup(self):
        key = "STR_EQUIPMENT_LABEL"
        before = "材料<color=0xff>%s</color>\\n数量%d"
        after = "Material<color=0xff>%s</color>\\nCount%d"
        original = current = table(f"{key}={before}\r\n")
        english = table(f"{key}={after}\r\n")
        for family in ("character-stat-ui", "equipment-ui", "character-equipment-ui"):
            with self.subTest(family=family):
                plan = dict(plan_for(original, english, current, [key]), family=family)
                proposed, report = propose(original, english, current, plan)
                self.assertEqual(f"{key}={after}\r\n".encode(), proposed)
                self.assertEqual(family, report["family"])
                self.assertEqual("Caller-supplied English; independent wording and rendering review required.", report["donorBoundary"])
                self.assertFalse(report["liveRenderingVerified"])

    def test_exact_case_replacement_preserves_unselected_case_sibling_and_raw_endings(self):
        key = "STR_EQUIPMENT_LABEL"
        original = table(f"{key}=装备\r\n")
        english = table(f"{key}=Equipment\r\n")
        current = table(f"{key}=装备\r\nstr_equipment_label=保持\rSTR_UNKNOWN-&1&=保留=列\n#注释", b"\xef\xbb\xbf")
        plan = dict(plan_for(original, english, current, [key]), family="equipment-ui")
        proposed, report = propose(original, english, current, plan)
        expected = b"\xef\xbb\xbf" + f"{key}=Equipment\r\nstr_equipment_label=保持\rSTR_UNKNOWN-&1&=保留=列\n#注释".encode()
        self.assertEqual(expected, proposed)
        self.assertEqual(3, len(NamedResources.parse(proposed).groups))
        self.assertEqual(1, report["changedPhysicalRows"])

    def test_equipment_family_refuses_unknown_or_malformed_family_without_writes(self):
        key = "STR_EQUIPMENT_LABEL"
        original = current = table(f"{key}=装备")
        english = table(f"{key}=Equipment")
        good = plan_for(original, english, current, [key])
        for family in ("anything-ui", "", [], {}, None):
            with self.subTest(family=family), self.assertRaises(ValueError):
                propose(original, english, current, dict(good, family=family))

    def test_explicit_equipment_plan_cli_is_create_only_and_rejects_path_traversal(self):
        key = "STR_EQUIPMENT_LABEL"
        with tempfile.TemporaryDirectory(prefix="wolf-equipment-ui-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            original = current = table(f"{key}=装备\r\n")
            english = table(f"{key}=Equipment\r\n")
            documents = (original, english, current)
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, document in zip(paths, documents):
                path.write_bytes(document.raw)
            plan = dict(plan_for(*documents, [key]), family="equipment-ui")
            (tool / "gear-ui-family.json").write_text(json.dumps(plan), encoding="utf-8")
            command = [sys.executable, str(tool / "propose_named_ui.py"), *map(str, paths), "--plan", "gear-ui-family.json"]
            checked = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, checked.returncode, checked.stderr)
            self.assertEqual("equipment-ui", json.loads(checked.stdout)["family"])
            self.assertFalse((tool / "proposals").exists())
            rejected = subprocess.run([*command[:-1], "../gear-ui-family.json", "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertNotEqual(0, rejected.returncode)
            self.assertFalse((tool / "proposals").exists())
            written = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, written.returncode, written.stderr)
            proposal = Path(json.loads(written.stdout)["proposalDirectory"])
            self.assertEqual(tool / "proposals", proposal.parent)
            self.assertEqual(f"{key}=Equipment\r\n".encode(), (proposal / "Cn_Res.ini").read_bytes())
            for path, document in zip(paths, documents):
                self.assertEqual(document.raw, path.read_bytes())

    def test_reviewed_combined_cli_preserves_duplicate_rows_and_refuses_over_200_keys(self):
        character, equipment = "STR_CHARACTER_UI", "STR_EQUIPMENT_UI"
        with tempfile.TemporaryDirectory(prefix="named-combined-ui-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            original = current = table(f"{character}=生命:%d\r\nSTR_UNSELECTED=保持\n{equipment}=装备\r{character}=生命:%d\r\n{character}=生命:%d", b"\xef\xbb\xbf")
            english = table(f"{character}=HP:%d\r\nSTR_UNSELECTED=保持\n{equipment}=Equipment\r{character}=HP:%d\r\n{character}=HP:%d", b"\xef\xbb\xbf")
            documents = (original, english, current)
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, document in zip(paths, documents):
                path.write_bytes(document.raw)
            plan = dict(plan_for(*documents, [character, equipment]), family="character-equipment-ui")
            plan_path = tool / "combined-ui-family.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            command = [sys.executable, str(tool / "propose_named_ui.py"), *map(str, paths), "--plan", "combined-ui-family.json"]
            checked = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual((2, 4), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual("character-equipment-ui", report["family"])
            self.assertFalse((tool / "proposals").exists())
            written = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, written.returncode, written.stderr)
            proposal = Path(json.loads(written.stdout)["proposalDirectory"])
            self.assertEqual(tool / "proposals", proposal.parent)
            self.assertEqual(english.raw, (proposal / "Cn_Res.ini").read_bytes())
            plan["keys"] = [f"STR_BOUND_{index}" for index in range(201)]
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            rejected = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertNotEqual(0, rejected.returncode)
            self.assertEqual([proposal], list((tool / "proposals").iterdir()))
            for path, document in zip(paths, documents):
                self.assertEqual(document.raw, path.read_bytes())


if __name__ == "__main__":
    unittest.main()
