"""Synthetic mail/trade proposal controls; no actual client resources."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from named_resources import NamedResources
from propose_named_ui import propose


class MailTradeUiTests(unittest.TestCase):
    @staticmethod
    def plan(documents):
        return {"schemaVersion": 1, "family": "mail-trade-ui", "codec": "utf-8",
                "inputSha256": {name: hashlib.sha256(raw).hexdigest().upper()
                                for name, raw in zip(("original", "english", "current"), documents)},
                "keys": ["STR_MAIL_SYNTHETIC", "STR_TRADE_SYNTHETIC"]}

    def test_curated_mail_trade_cli_preserves_tokens_duplicates_and_unselected_bytes(self):
        original = ("STR_MAIL_SYNTHETIC=银:%I64d\r\nSTR_KEEP=保留\n"
                    "STR_TRADE_SYNTHETIC=<color=0xff00ff00>拒绝:%s</color>\r"
                    "STR_MAIL_SYNTHETIC=银:%I64d").encode()
        english = ("STR_MAIL_SYNTHETIC=Silver:%I64d\r\nSTR_KEEP=保留\n"
                   "STR_TRADE_SYNTHETIC=<color=0xff00ff00>Refused:%s</color>\r"
                   "STR_MAIL_SYNTHETIC=Silver:%I64d").encode()
        documents = (original, english, original)
        with tempfile.TemporaryDirectory(prefix="mail-trade-ui-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, data in zip(paths, documents):
                path.write_bytes(data)
            (tool / "mail-trade-ui-family.json").write_text(json.dumps(self.plan(documents)), encoding="utf-8")
            command = [sys.executable, str(tool / "propose_named_ui.py"), *map(str, paths),
                       "--plan", "mail-trade-ui-family.json"]
            checked = subprocess.run(command, capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, checked.returncode, checked.stderr)
            report = json.loads(checked.stdout)
            self.assertEqual("mail-trade-ui", report["family"])
            self.assertEqual((2, 3), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertFalse(report["clientApplied"])
            self.assertFalse(report["liveRenderingVerified"])
            self.assertFalse((tool / "proposals").exists())
            written = subprocess.run([*command, "--write-proposal"], capture_output=True, text=True, timeout=5, check=False)
            self.assertEqual(0, written.returncode, written.stderr)
            proposal = Path(json.loads(written.stdout)["proposalDirectory"])
            self.assertEqual(english, (proposal / "Cn_Res.ini").read_bytes())
            bad = [*command[:-1], "../mail-trade-ui-family.json", "--write-proposal"]
            refused = subprocess.run(bad, capture_output=True, text=True, timeout=5, check=False)
            self.assertNotEqual(0, refused.returncode)
            self.assertEqual([proposal], list((tool / "proposals").iterdir()))
            for path, data in zip(paths, documents):
                self.assertEqual(data, path.read_bytes())

    def test_mail_trade_selection_holds_changed_printf_and_markup_without_repair(self):
        original = "STR_MAIL_SYNTHETIC=银:%I64d\nSTR_TRADE_SYNTHETIC=<color=red>拒绝:%s</color>\n".encode()
        english = b"STR_MAIL_SYNTHETIC=Silver:%d\nSTR_TRADE_SYNTHETIC=<color=blue>Refused:%s</color>\n"
        documents = (original, english, original)
        after, report = propose(*(NamedResources.parse(raw) for raw in documents), self.plan(documents))
        self.assertEqual(original, after)
        self.assertEqual(0, report["mergedKeyCount"])
        self.assertEqual(["STR_MAIL_SYNTHETIC", "STR_TRADE_SYNTHETIC"], report["held"]["formatSignatureChanged"])

    def test_literal_comparisons_keep_exact_order_and_bare_angle_syntax_stays_held(self):
        original = "STR_MAIL_SYNTHETIC=等级<=%d\nSTR_TRADE_SYNTHETIC=%d>=门槛\n".encode()
        english = b"STR_MAIL_SYNTHETIC=Level<=%d\nSTR_TRADE_SYNTHETIC=%d>=Threshold\n"
        documents = (original, english, original)
        after, report = propose(*(NamedResources.parse(raw) for raw in documents), self.plan(documents))
        self.assertEqual(english, after)
        self.assertEqual(2, report["mergedKeyCount"])
        for changed in (english.replace(b"<=", b">="), english.replace(b"Level<=%d", b"%d<=Level")):
            with self.subTest(changed=changed):
                held, report = propose(*(NamedResources.parse(raw) for raw in (original, changed, original)),
                                       self.plan((original, changed, original)))
                self.assertEqual(1, report["mergedKeyCount"])
                self.assertEqual(["STR_MAIL_SYNTHETIC"], report["held"]["formatSignatureChanged"])
                self.assertEqual("等级<=%d", NamedResources.parse(held).groups["STR_MAIL_SYNTHETIC"][0].value)
        bare = original.replace(b"<=", b"<")
        donor = english.replace(b"<=", b"<")
        held, report = propose(*(NamedResources.parse(raw) for raw in (bare, donor, bare)), self.plan((bare, donor, bare)))
        self.assertEqual(["STR_MAIL_SYNTHETIC"], report["held"]["unsupportedFormatSyntax"])


if __name__ == "__main__":
    unittest.main()
