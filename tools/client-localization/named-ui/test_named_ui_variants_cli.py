"""Actual variant CLI controls using only synthetic disposable files."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from test_named_ui_variants import BLUE, EN_BLUE, EN_RED, RED, fixtures, variant_plan


class NamedUiVariantCliTests(unittest.TestCase):
    def fixture(self, root):
        tool = root / "tool"
        tool.mkdir()
        for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py",
                     "variant_proposals.py", "propose_named_ui_variants.py"):
            shutil.copyfile(Path(__file__).parent / name, tool / name)
        documents = fixtures()
        paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
        for path, document in zip(paths, documents):
            path.write_bytes(document.raw)
        plan_path = tool / "core-ui-variants.json"
        plan = variant_plan(*documents)
        plan_path.write_text(json.dumps(plan), encoding="utf-8")
        command = [sys.executable, "-B", str(tool / "propose_named_ui_variants.py"), *map(str, paths),
                   "--plan", "core-ui-variants.json"]
        return tool, documents, paths, plan, plan_path, command

    def run_cli(self, command):
        return subprocess.run(command, capture_output=True, text=True, timeout=10, check=False)

    def test_no_write_and_two_fresh_outputs_preserve_all_three_input_files(self):
        with tempfile.TemporaryDirectory(prefix="wolf-variant-cli-") as temporary:
            tool, documents, paths, plan, plan_path, command = self.fixture(Path(temporary))
            planned = self.run_cli(command)
            self.assertEqual(0, planned.returncode, planned.stderr)
            report = json.loads(planned.stdout)
            self.assertEqual((1, 2, 3), (report["mergedKeyCount"], report["mergedVariantCount"], report["changedPhysicalRows"]))
            self.assertFalse((tool / "proposals").exists())
            outputs = []
            golden = documents[2].raw.replace(RED.encode(), EN_RED.encode()).replace(BLUE.encode(), EN_BLUE.encode())
            for _ in range(2):
                actual = self.run_cli([*command, "--write-proposal"])
                self.assertEqual(0, actual.returncode, actual.stderr)
                report = json.loads(actual.stdout)
                proposal = Path(report["proposalDirectory"])
                self.assertEqual(tool / "proposals", proposal.parent)
                self.assertTrue((proposal / "proposal-report.json").is_file())
                self.assertEqual(golden, (proposal / "Cn_Res.ini").read_bytes())
                outputs.append(proposal)
            self.assertNotEqual(*outputs)
            self.assertEqual((outputs[0] / "Cn_Res.ini").read_bytes(), (outputs[1] / "Cn_Res.ini").read_bytes())
            self.assertEqual([document.raw for document in documents], [path.read_bytes() for path in paths])
            self.assertEqual(plan, json.loads(plan_path.read_text()))

    def test_plan_bound_duplicate_properties_family_and_pins_fail_without_any_output(self):
        with tempfile.TemporaryDirectory(prefix="wolf-variant-cli-") as temporary:
            tool, documents, paths, plan, plan_path, command = self.fixture(Path(temporary))
            good = json.dumps(plan)
            wrong_pin = json.loads(good)
            wrong_pin["inputSha256"]["current"] = "0" * 64
            wrong_family = dict(plan, family="equipment-ui")
            for raw in (good + " " * (65537 - len(good)),
                        good.replace('"family": "core-ui"', '"family":"core-ui","family":"core-ui"'),
                        good.replace('"key": "STR_SYNTHETIC_LABEL"', '"key":"STR_SYNTHETIC_LABEL","key":"STR_SYNTHETIC_LABEL"', 1),
                        json.dumps(wrong_pin), json.dumps(wrong_family), json.dumps(dict(plan, unknown=True))):
                with self.subTest(raw=raw[:80]):
                    plan_path.write_text(raw, encoding="utf-8")
                    actual = self.run_cli([*command, "--write-proposal"])
                    self.assertNotEqual(0, actual.returncode)
                    self.assertEqual("", actual.stdout)
                    self.assertFalse((tool / "proposals").exists())
                    self.assertEqual([document.raw for document in documents], [path.read_bytes() for path in paths])
                    self.assertEqual(raw, plan_path.read_text())

    def test_unlisted_absolute_relative_and_missing_plan_routes_are_not_accepted(self):
        with tempfile.TemporaryDirectory(prefix="wolf-variant-cli-") as temporary:
            tool, documents, paths, plan, plan_path, command = self.fixture(Path(temporary))
            prefix = command[:-2]
            for tail in ([], ["--plan", "../core-ui-variants.json"], ["--plan", str(plan_path)],
                         ["--plan", "core-ui-family.json"]):
                with self.subTest(tail=tail):
                    actual = self.run_cli([*prefix, *tail, "--write-proposal"])
                    self.assertNotEqual(0, actual.returncode)
                    self.assertEqual("", actual.stdout)
                    self.assertFalse((tool / "proposals").exists())

    def test_nonordinary_input_malformed_tail_and_nul_fail_without_output(self):
        with tempfile.TemporaryDirectory(prefix="wolf-variant-cli-") as temporary:
            tool, documents, paths, plan, plan_path, command = self.fixture(Path(temporary))
            original_bytes = paths[0].read_bytes()
            for malformed in (original_bytes + b"\nnot-an-assignment", original_bytes + b"\0", b"STR_X=\xff"):
                paths[0].write_bytes(malformed)
                actual = self.run_cli([*command, "--write-proposal"])
                self.assertNotEqual(0, actual.returncode)
                self.assertEqual("", actual.stdout)
                self.assertFalse((tool / "proposals").exists())
            paths[0].write_bytes(original_bytes)
            bad_command = list(command)
            bad_command[3] = str(tool)
            actual = self.run_cli([*bad_command, "--write-proposal"])
            self.assertNotEqual(0, actual.returncode)
            self.assertEqual("", actual.stdout)
            self.assertFalse((tool / "proposals").exists())

    def test_occupied_proposal_parent_is_not_replaced_or_written_through(self):
        with tempfile.TemporaryDirectory(prefix="wolf-variant-cli-") as temporary:
            tool, documents, paths, plan, plan_path, command = self.fixture(Path(temporary))
            parent = tool / "proposals"
            parent.write_bytes(b"owned-existing-file")
            actual = self.run_cli([*command, "--write-proposal"])
            self.assertNotEqual(0, actual.returncode)
            self.assertEqual("", actual.stdout)
            self.assertEqual(b"owned-existing-file", parent.read_bytes())
            self.assertEqual([document.raw for document in documents], [path.read_bytes() for path in paths])


if __name__ == "__main__":
    unittest.main()
