"""CLI checks using only disposable synthetic fixtures."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from test_named_ui import KEY, plan_for, table


class NamedUiCliTests(unittest.TestCase):
    def test_default_no_write_and_fresh_proposal_never_change_input_files(self):
        with tempfile.TemporaryDirectory(prefix="wolf-named-ui-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            original = current = table(f"{KEY}=生命:%d\r\n")
            english = table(f"{KEY}=HP:%d\r\n")
            documents = (original, english, current)
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, document in zip(paths, documents):
                path.write_bytes(document.raw)
            (tool / "stat-ui-family.json").write_text(json.dumps(plan_for(*documents)), encoding="utf-8")
            command = [sys.executable, str(tool / "propose_named_ui.py"), *map(str, paths)]
            planned = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, planned.returncode, planned.stderr)
            self.assertEqual(1, json.loads(planned.stdout)["changedPhysicalRows"])
            self.assertFalse((tool / "proposals").exists())
            created = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, created.returncode, created.stderr)
            proposal = Path(json.loads(created.stdout)["proposalDirectory"])
            self.assertEqual(tool / "proposals", proposal.parent)
            self.assertEqual(f"{KEY}=HP:%d\r\n".encode(), (proposal / "Cn_Res.ini").read_bytes())
            for path, document in zip(paths, documents):
                self.assertEqual(document.raw, path.read_bytes())
            again = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, again.returncode, again.stderr)
            self.assertNotEqual(proposal, Path(json.loads(again.stdout)["proposalDirectory"]))


if __name__ == "__main__":
    unittest.main()
