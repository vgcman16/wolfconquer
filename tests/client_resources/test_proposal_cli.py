"""Exercise the public CLI against synthetic ordinary files in an owned folder."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


class ProposalCliTests(unittest.TestCase):
    def test_dry_run_and_fresh_proposal_preserve_all_three_inputs(self):
        tool_source = Path(__file__).resolve().parents[2] / "tools" / "client_resources"
        with tempfile.TemporaryDirectory(prefix="wolf-resource-cli-") as tmp:
            root = Path(tmp)
            tool = root / "tools"
            tool.mkdir()
            for name in ("propose.py", "merge_resources.py", "resource_table.py", "reviewed.py"):
                shutil.copyfile(tool_source / name, tool / name)
            inputs = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            texts = ("1=原文:%d\r\n100077=机器键\r\n",
                     "1=Translated fixture:%d\n100077=Changed machine key\n",
                     ";当前布局\r\n  1 =原文:%d\r\n100077=机器键\r\n")
            for path, text in zip(inputs, texts):
                path.write_bytes(b"\xff\xfe" + text.encode("utf-16-le"))
            before = {path: path.read_bytes() for path in inputs}
            command = [sys.executable, "-B", str(tool / "propose.py"), *map(str, inputs)]
            environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1")
            dry = subprocess.run(command, cwd=root, env=environment, capture_output=True, text=True, timeout=10)
            self.assertEqual(0, dry.returncode, dry.stderr)
            report = json.loads(dry.stdout)
            self.assertEqual([1], report["mergedKeys"])
            self.assertEqual([100077], report["heldKeys"]["machineLookupLiteral"])
            self.assertFalse((tool / "proposals").exists())

            written = subprocess.run(command + ["--write-proposal"], cwd=root, env=environment,
                                     capture_output=True, text=True, timeout=10)
            self.assertEqual(0, written.returncode, written.stderr)
            result = json.loads(written.stdout)
            destination = Path(result["proposalDirectory"])
            self.assertEqual(tool / "proposals", destination.parent)
            self.assertEqual({"UStrRes.ini", "merge-report.json"}, {p.name for p in destination.iterdir()})
            expected = ";当前布局\r\n  1 =Translated fixture:%d\r\n100077=机器键\r\n"
            self.assertEqual(b"\xff\xfe" + expected.encode("utf-16-le"), (destination / "UStrRes.ini").read_bytes())
            self.assertFalse(result["clientFilesChanged"])
            self.assertFalse(result["runtimeStarted"])
            self.assertNotIn("Translated fixture:%d", written.stdout)
            self.assertNotIn("机器键", written.stdout)
            for path in inputs:
                self.assertEqual(before[path], path.read_bytes())
