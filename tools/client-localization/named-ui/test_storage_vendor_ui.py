"""Synthetic actual-CLI storage/vendor controls; no client data or application."""
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

FILENAME = "storage-vendor-ui-family.json"
FAMILY = "storage-vendor-ui"
STORAGE, VENDOR = "STR_SYNTHETIC_STORAGE", "STR_SYNTHETIC_VENDOR"


class StorageVendorUiTests(unittest.TestCase):
    @contextmanager
    def workspace(self, documents):
        with tempfile.TemporaryDirectory(prefix="storage-vendor-ui-") as temporary:
            root = Path(temporary)
            tool = root / "tool"
            tool.mkdir()
            for name in ("named_resources.py", "format_tokens.py", "propose_named_ui.py"):
                shutil.copyfile(Path(__file__).parent / name, tool / name)
            paths = [root / name for name in ("original.ini", "english.ini", "current.ini")]
            for path, data in zip(paths, documents):
                path.write_bytes(data)
            yield root, tool, paths

    @staticmethod
    def plan(documents, keys=(STORAGE, VENDOR), family=FAMILY):
        return {"schemaVersion": 1, "family": family, "codec": "utf-8",
                "inputSha256": {role: hashlib.sha256(raw).hexdigest().upper()
                                for role, raw in zip(("original", "english", "current"), documents)},
                "keys": list(keys)}

    @staticmethod
    def invoke(tool, paths, filename=FILENAME, write=False):
        command = [sys.executable, "-B", str(tool / "propose_named_ui.py"), *map(str, paths), "--plan", filename]
        return subprocess.run(command + (["--write-proposal"] if write else []), capture_output=True,
                              text=True, encoding="utf-8", timeout=5, check=False,
                              env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1"))

    @staticmethod
    def snapshot(root):
        return {path.relative_to(root): None if path.is_dir() else path.read_bytes() for path in root.rglob("*")}

    @staticmethod
    def documents():
        original = (f";原文\r\n{STORAGE}=余额:%I64d\r\nSTR_KEEP=原文\n"
                    f"{VENDOR}=<color=red>数量<=%d</color>\r{STORAGE}=余额:%I64d").encode()
        english = (f";donor\r\n{STORAGE}=Balance:%I64d\r\nSTR_KEEP=Unused donor\n"
                   f"{VENDOR}=<color=red>Count<=%d</color>\r{STORAGE}=Balance:%I64d").encode()
        current = b"\xef\xbb\xbf" + original.replace("STR_KEEP=原文".encode(), "STR_KEEP=保留目标".encode())
        golden = current.replace("余额:%I64d".encode(), b"Balance:%I64d").replace("数量<=%d".encode(), b"Count<=%d")
        return (original, english, current), golden

    def test_actual_cli_preserves_duplicate_rows_tokens_bom_and_unselected_bytes(self):
        documents, golden = self.documents()
        with self.workspace(documents) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(self.plan(documents)), encoding="utf-8")
            before = self.snapshot(root)
            result = self.invoke(tool, paths)
            self.assertEqual(0, result.returncode, result.stderr)
            report = json.loads(result.stdout)
            self.assertEqual(FAMILY, report["family"])
            self.assertEqual((2, 3), (report["mergedKeyCount"], report["changedPhysicalRows"]))
            self.assertEqual({}, report["held"])
            self.assertFalse(report["clientApplied"])
            self.assertFalse(report["liveRenderingVerified"])
            self.assertEqual(before, self.snapshot(root))
            outputs = []
            for _ in range(2):
                written = self.invoke(tool, paths, write=True)
                self.assertEqual(0, written.returncode, written.stderr)
                output = Path(json.loads(written.stdout)["proposalDirectory"])
                self.assertEqual(tool / "proposals", output.parent)
                self.assertEqual(golden, (output / "Cn_Res.ini").read_bytes())
                outputs.append(output)
            self.assertNotEqual(*outputs)
            self.assertEqual(set(outputs), set((tool / "proposals").iterdir()))
            for path, raw in zip(paths, documents):
                self.assertEqual(raw, path.read_bytes())

    def test_actual_cli_refuses_family_mismatch_in_both_directions_before_any_write(self):
        documents, _ = self.documents()
        old = {"stat-ui-family.json": "character-stat-ui", "gear-ui-family.json": "equipment-ui",
               "combined-ui-family.json": "character-equipment-ui", "mail-trade-ui-family.json": "mail-trade-ui"}
        with self.workspace(documents) as (root, tool, paths):
            for filename, family in old.items():
                for selected_file, declared_family in ((FILENAME, family), (filename, FAMILY)):
                    with self.subTest(filename=selected_file, family=declared_family):
                        (tool / selected_file).write_text(json.dumps(self.plan(documents, family=declared_family)), encoding="utf-8")
                        before = self.snapshot(root)
                        result = self.invoke(tool, paths, selected_file, write=True)
                        self.assertNotEqual(0, result.returncode)
                        self.assertEqual("", result.stdout)
                        self.assertIn("namedResourcePlanFamilyMismatch", result.stderr)
                        self.assertEqual(before, self.snapshot(root))

    def test_actual_cli_holds_changed_printf_markup_and_comparison_without_writes(self):
        documents, _ = self.documents()
        for replacement in (documents[1].replace(b"%I64d", b"%d").replace(b"color=red", b"color=blue"),
                            documents[1].replace(b"%I64d", b"%d").replace(b"<=", b">=")):
            with self.subTest(donor=replacement), self.workspace((documents[0], replacement, documents[2])) as (root, tool, paths):
                actual = documents[0], replacement, documents[2]
                (tool / FILENAME).write_text(json.dumps(self.plan(actual)), encoding="utf-8")
                before = self.snapshot(root)
                result = self.invoke(tool, paths)
                self.assertEqual(0, result.returncode, result.stderr)
                report = json.loads(result.stdout)
                self.assertEqual(0, report["mergedKeyCount"])
                self.assertEqual([STORAGE, VENDOR], report["held"]["formatSignatureChanged"])
                self.assertEqual(hashlib.sha256(documents[2]).hexdigest().upper(), report["proposalSha256"])
                self.assertEqual(before, self.snapshot(root))

    def test_actual_cli_holds_nonuniform_duplicate_donor_meaning(self):
        documents, _ = self.documents()
        donor = documents[1].replace(b"Balance:%I64d", b"Other:%I64d", 1)
        actual = documents[0], donor, documents[2]
        with self.workspace(actual) as (root, tool, paths):
            (tool / FILENAME).write_text(json.dumps(self.plan(actual, keys=(STORAGE,))), encoding="utf-8")
            before = self.snapshot(root)
            result = self.invoke(tool, paths)
            self.assertEqual(0, result.returncode, result.stderr)
            report = json.loads(result.stdout)
            self.assertEqual({"nonUniformDuplicateMeaning": [STORAGE]}, report["held"])
            self.assertEqual(0, report["mergedKeyCount"])
            self.assertEqual(before, self.snapshot(root))

    def test_actual_cli_keeps_200_key_bound_and_rejects_arbitrary_plan_paths(self):
        documents, _ = self.documents()
        with self.workspace(documents) as (root, tool, paths):
            plan = self.plan(documents, keys=[f"STR_SYNTHETIC_{index}" for index in range(201)])
            (tool / FILENAME).write_text(json.dumps(plan), encoding="utf-8")
            before = self.snapshot(root)
            for filename in (FILENAME, "../" + FILENAME):
                result = self.invoke(tool, paths, filename, write=True)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual("", result.stdout)
                self.assertEqual(before, self.snapshot(root))

    def test_actual_cli_still_holds_selected_case_aliases_and_machine_values(self):
        cases = [(f"{STORAGE}=条目:%d\nstr_synthetic_storage=别名\n".encode(),
                  f"{STORAGE}=Entry:%d\nstr_synthetic_storage=Alias\n".encode(), "caseAliasAmbiguous"),
                 (f"{STORAGE}=地址:https://example.invalid/\n".encode(),
                  f"{STORAGE}=Address:https://example.invalid/\n".encode(), "machineOrNonDisplayValue")]
        for original, english, reason in cases:
            with self.subTest(reason=reason), self.workspace((original, english, original)) as (root, tool, paths):
                (tool / FILENAME).write_text(json.dumps(self.plan((original, english, original), keys=(STORAGE,))), encoding="utf-8")
                before = self.snapshot(root)
                result = self.invoke(tool, paths)
                self.assertEqual(0, result.returncode, result.stderr)
                report = json.loads(result.stdout)
                self.assertEqual({reason: [STORAGE]}, report["held"])
                self.assertEqual(0, report["mergedKeyCount"])
                self.assertEqual(before, self.snapshot(root))


if __name__ == "__main__":
    unittest.main()
