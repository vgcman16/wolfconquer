"""Synthetic byte-level controls for pinned named UI proposal integrity."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from named_resources import NamedResources
from propose_named_ui import propose
from test_named_ui import KEY, plan_for, table


class NamedUiIntegrityTests(unittest.TestCase):
    def test_pinned_raw_documents_override_forged_lines_and_groups_for_all_three_roles(self):
        original = table(f"{KEY}=生命:%d\r\nSTR_KEEP=原文\n")
        english = table(f"{KEY}=HP:%d\r\nSTR_KEEP=Not selected\n")
        current = table(f";保留\n{KEY}=生命:%d\r\nSTR_KEEP=保留", b"\xef\xbb\xbf")
        plan = plan_for(original, english, current)
        golden = b"\xef\xbb\xbf" + f";保留\n{KEY}=HP:%d\r\nSTR_KEEP=保留".encode()
        forged_index = table(f"{KEY}=伪造\nSTR_KEEP=改动\n")
        for index in range(3):
            with self.subTest(role=index):
                documents = [original, english, current]
                genuine = documents[index]
                documents[index] = NamedResources(genuine.raw, forged_index.bom,
                                                   forged_index.lines, forged_index.groups)
                after, report = propose(*documents, plan)
                self.assertEqual(golden, after)
                self.assertEqual([KEY], report["mergedKeys"])
                self.assertEqual(1, report["changedPhysicalRows"])

    def test_parse_owns_mutable_input_and_group_membership_cannot_be_replaced(self):
        source = bytearray(f"{KEY}=生命:%d\r\nSTR_KEEP=保留\n".encode())
        before = bytes(source)
        current = NamedResources.parse(source)
        source[:] = b"X" * len(source)
        self.assertIsInstance(current.raw, bytes)
        self.assertEqual(before, current.raw)
        with self.assertRaises(TypeError):
            current.groups[KEY] = ()
        with self.assertRaises(TypeError):
            del current.groups[KEY]
        english = table(f"{KEY}=HP:%d\r\nSTR_KEEP=unused\n")
        after, _ = propose(current, english, current, plan_for(current, english, current))
        self.assertEqual(f"{KEY}=HP:%d\r\nSTR_KEEP=保留\n".encode(), after)

    def test_selected_case_alias_in_each_input_holds_but_unselected_aliases_remain_exact(self):
        base = f"{KEY}=生命:%d\r\nSTR_KEEP=原\nstr_keep=另一个\r"
        for position in range(3):
            with self.subTest(position=position):
                texts = [base, f"{KEY}=HP:%d\r\nSTR_KEEP=old\nstr_keep=other\r", base]
                texts[position] += "str_player_life=别名\n"
                documents = [table(text) for text in texts]
                after, report = propose(*documents, plan_for(*documents))
                self.assertEqual(texts[2].encode(), after)
                self.assertEqual([], report["mergedKeys"])
                self.assertIn(KEY, [key for group in report["held"].values() for key in group])
        original = table(base)
        english = table(f"{KEY}=HP:%d\r\nSTR_KEEP=old\nstr_keep=other\r")
        current = table(base)
        after, report = propose(original, english, current, plan_for(original, english, current))
        self.assertEqual(f"{KEY}=HP:%d\r\nSTR_KEEP=原\nstr_keep=另一个\r".encode(), after)
        self.assertEqual([KEY], report["mergedKeys"])

    def test_existing_per_plan_and_input_size_limits_are_not_widened(self):
        original = current = table(f"{KEY}=生命:%d\n")
        english = table(f"{KEY}=HP:%d\n")
        plan = plan_for(original, english, current,
                        keys=tuple(f"STR_KEY_{index}" for index in range(201)))
        with self.assertRaises(ValueError):
            propose(original, english, current, plan)
        with self.assertRaises(ValueError):
            NamedResources.parse(b"X" * (16 * 1024 * 1024 + 1))
    def test_actual_cli_binds_each_curated_filename_to_family_before_creating_output(self):
        families = {"stat-ui-family.json": "character-stat-ui",
                    "gear-ui-family.json": "equipment-ui",
                    "combined-ui-family.json": "character-equipment-ui",
                    "mail-trade-ui-family.json": "mail-trade-ui"}
        with tempfile.TemporaryDirectory(prefix="wolf-ui-integrity-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            original = current = table(f"{KEY}=生命:%d\r\nSTR_KEEP=保留\n", b"\xef\xbb\xbf")
            english = table(f"{KEY}=HP:%d\r\nSTR_KEEP=unused\n", b"\xef\xbb\xbf")
            docs = original, english, current
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, document in zip(paths, docs):
                path.write_bytes(document.raw)
            inputs_before = [path.read_bytes() for path in paths]
            golden = b"\xef\xbb\xbf" + f"{KEY}=HP:%d\r\nSTR_KEEP=保留\n".encode()
            for filename, family in families.items():
                with self.subTest(filename=filename):
                    plan = plan_for(*docs)
                    plan["family"] = family
                    plan_path = tool / filename
                    plan_path.write_text(json.dumps(plan), encoding="utf-8")
                    command = [sys.executable, str(tool / "propose_named_ui.py"), *map(str, paths),
                               "--plan", filename, "--write-proposal"]
                    valid = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
                    self.assertEqual(0, valid.returncode, valid.stderr)
                    report = json.loads(valid.stdout)
                    self.assertEqual(family, report["family"])
                    self.assertEqual(golden, (Path(report["proposalDirectory"]) / "Cn_Res.ini").read_bytes())
                    prior = set((tool / "proposals").iterdir())
                    plan["family"] = next(other for other in families.values() if other != family)
                    plan_path.write_text(json.dumps(plan), encoding="utf-8")
                    plan_before = plan_path.read_bytes()
                    rejected = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
                    self.assertNotEqual(0, rejected.returncode)
                    self.assertEqual("", rejected.stdout)
                    self.assertEqual(prior, set((tool / "proposals").iterdir()))
                    self.assertEqual(plan_before, plan_path.read_bytes())
                    self.assertEqual(inputs_before, [path.read_bytes() for path in paths])


if __name__ == "__main__":
    unittest.main()

