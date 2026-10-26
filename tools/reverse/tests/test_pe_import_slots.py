"""Owned synthetic PE fixtures only; stdlib unittest, no native loading."""
import hashlib
import importlib.util
import json
from pathlib import Path
import stat
import struct
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

sys.dont_write_bytecode = True
ROOT = Path(__file__).absolute().parents[3]
TOOLS = ROOT / "tools" / "reverse"
sys.path.insert(0, str(TOOLS))
import pe_import_slots as pe

spec = importlib.util.spec_from_file_location("slot_command", TOOLS / "Inspect-PeImportSlots.py")
command = importlib.util.module_from_spec(spec)
spec.loader.exec_module(command)


def put32(data, offset, value):
    struct.pack_into("<I", data, offset, value)


def raw(rva):
    return rva - 0x1000 + 0x400


def fixture(wide=False, descriptors=1, fallback=False):
    data = bytearray(0x2000)
    data[:2] = b"MZ"
    put32(data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\0\0"
    width, base = (8, 0x140000000) if wide else (4, 0x400000)
    optional, size = 0x98, 240 if wide else 224
    struct.pack_into("<HH", data, 0x84, 0x8664 if wide else 0x14C, 1)
    struct.pack_into("<H", data, 0x94, size)
    struct.pack_into("<H", data, optional, 0x20B if wide else 0x10B)
    struct.pack_into("<Q" if wide else "<I", data, optional + (24 if wide else 28), base)
    put32(data, optional + 56, 0x3000)
    put32(data, optional + 60, 0x400)
    prefix = 112 if wide else 96
    put32(data, optional + prefix - 4, 16)
    struct.pack_into("<II", data, optional + prefix + 8, 0x1100, (descriptors + 1) * 20)
    table = optional + size
    data[table:table + 8] = b".rdata\0\0"
    struct.pack_into("<IIII", data, table + 8, 0x1000, 0x1000, 0x1000, 0x400)
    put32(data, table + 36, 0x40000040)
    for number in range(descriptors):
        lookup, iat = 0x1200 + number * 0x40, 0x1300 + number * 0x40
        module, symbol = 0x1400 + number * 0x40, 0x1500 + number * 0x100
        struct.pack_into("<IIIII", data, raw(0x1100) + number * 20,
                         0 if fallback else lookup, 0, 0, module, iat)
        for position, value in enumerate((symbol, (1 << (width * 8 - 1)) | 23, 0)):
            for target in (lookup, iat):
                struct.pack_into("<Q" if wide else "<I", data, raw(target) + position * width, value)
        name = b"synthetic.dll\0"
        data[raw(module):raw(module) + len(name)] = name
        struct.pack_into("<H", data, raw(symbol), 7)
        text = b"synthetic_read\0"
        data[raw(symbol) + 2:raw(symbol) + 2 + len(text)] = text
    return data, base, width


class ImportSlotTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        work = ROOT / ".artifacts" / "pe-import-slot-tests"
        work.mkdir(parents=True, exist_ok=True)
        cls.temporary = tempfile.TemporaryDirectory(prefix="synthetic-", dir=work)
        cls.work = Path(cls.temporary.name)

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def parse(self, data, slots):
        return pe.ImportSlots(bytes(data), pe.Budget()).selected(slots)

    def reject(self, data, slots=None):
        with self.assertRaises(pe.Refused):
            self.parse(data, slots or [0x401300])

    def test_pe32_name_and_ordinal_are_literal_metadata(self):
        data, base, width = fixture()
        rows = self.parse(data, [base + 0x1300, base + 0x1300 + width])
        self.assertEqual(["synthetic.dll", "synthetic.dll"], [row["module"] for row in rows])
        self.assertEqual("synthetic_read", rows[0]["symbol"])
        self.assertEqual(7, rows[0]["hint"])
        self.assertEqual(23, rows[1]["ordinal"])
        self.assertNotIn("symbol", rows[1])

    def test_pe64_preserves_full_va_and_ordinal_width(self):
        data, base, width = fixture(wide=True)
        rows = self.parse(data, [base + 0x1300, base + 0x1300 + width])
        self.assertEqual("0000000140001300", rows[0]["iatVa"])
        self.assertEqual(23, rows[1]["ordinal"])

    def test_unbound_fallback_uses_iat_only_when_original_missing(self):
        data, base, _ = fixture(fallback=True)
        self.assertEqual("unbound-iat", self.parse(data, [base + 0x1300])[0]["lookupKind"])

    def test_no_unselected_module_or_symbol_name_dereference(self):
        data, base, _ = fixture(descriptors=2)
        put32(data, raw(0x1100) + 20 + 12, 0xFFFFFF00)
        put32(data, raw(0x1240), 0xFFFFFF00)
        self.assertEqual("synthetic_read", self.parse(data, [base + 0x1300])[0]["symbol"])

    def test_reject_dos_signature(self):
        data, _, _ = fixture(); data[0] = 0
        self.reject(data)

    def test_reject_truncated_header(self):
        self.reject(b"MZ" + bytes(62))

    def test_reject_optional_header_directory_overrun(self):
        data, _, _ = fixture(); put32(data, 0x98 + 92, 17)
        self.reject(data)

    def test_reject_section_outside_file(self):
        data, _, _ = fixture(); put32(data, 0x98 + 224 + 20, 0x2000)
        self.reject(data)

    def test_reject_ambiguous_section_mapping(self):
        data, _, _ = fixture(); table = 0x98 + 224
        struct.pack_into("<H", data, 0x86, 2)
        data[table + 40:table + 80] = data[table:table + 40]
        self.reject(data)

    def test_reject_virtual_tail_without_raw_backing(self):
        data, _, _ = fixture(); put32(data, 0x98 + 224 + 8, 0x1800)
        self.reject(data, [0x402100])

    def test_reject_slot_count_duplicates_alignment_and_terminator(self):
        data, base, _ = fixture()
        for slots in ([], [base + 0x1300] * 2, list(range(9)), [base + 0x1301], [base + 0x1308]):
            with self.subTest(slots=slots), self.assertRaises(pe.Refused):
                self.parse(data, slots)

    def test_reject_descriptor_without_in_directory_terminator(self):
        data, _, _ = fixture(); put32(data, 0x98 + 96 + 12, 20)
        self.reject(data)

    def test_reject_descriptor_cap_without_truncation(self):
        data, _, _ = fixture(descriptors=2)
        with mock.patch.object(pe, "MAX_DESCRIPTORS", 1):
            self.reject(data)

    def test_reject_per_table_thunk_cap(self):
        data, _, _ = fixture()
        with mock.patch.object(pe, "MAX_THUNKS", 1):
            self.reject(data)

    def test_reject_total_thunk_budget(self):
        data, _, _ = fixture()
        with mock.patch.object(pe, "MAX_TOTAL_THUNKS", 1):
            self.reject(data)

    def test_reject_name_cap_or_unsafe_module(self):
        data, _, _ = fixture(); data[raw(0x1500) + 2:raw(0x1500) + 258] = b"A" * 256
        self.reject(data)
        data, _, _ = fixture(); data[raw(0x1400):raw(0x1400) + 13] = b"bad/name.dll\0"
        self.reject(data)

    def test_accept_exact_name_bound(self):
        data, base, _ = fixture(); data[raw(0x1500) + 2:raw(0x1500) + 258] = b"A" * 255 + b"\0"
        self.assertEqual(255, len(self.parse(data, [base + 0x1300])[0]["symbol"]))

    def test_reject_selected_name_outside_image(self):
        data, _, _ = fixture(); put32(data, raw(0x1200), 0xFFFF0000)
        self.reject(data)

    def test_reject_ambiguous_overlapping_iat_ownership(self):
        data, base, _ = fixture(descriptors=2)
        put32(data, raw(0x1100) + 20, 0x1204)
        put32(data, raw(0x1100) + 20 + 16, 0x1304)
        self.reject(data, [base + 0x1304])

    def test_reject_iat_lookup_terminator_mismatch(self):
        data, _, _ = fixture(); put32(data, raw(0x1304), 0)
        self.reject(data)

    def test_reject_bound_iat_fallback_without_original_lookup(self):
        data, _, _ = fixture(fallback=True); put32(data, raw(0x1100) + 4, 1)
        self.reject(data)

    def test_reject_reserved_ordinal_and_pe64_name_high_bits(self):
        data, base, _ = fixture(); put32(data, raw(0x1204), 0x80010017)
        self.reject(data, [base + 0x1304])
        data, base, _ = fixture(wide=True); struct.pack_into("<Q", data, raw(0x1200), 0x100001500)
        self.reject(data, [base + 0x1300])

    def test_deadline_is_refusal(self):
        budget = pe.Budget(); budget.deadline = 0
        data, _, _ = fixture()
        with self.assertRaisesRegex(pe.Refused, "deadline"):
            pe.ImportSlots(data, budget)

    def test_ordinary_guard_refuses_reparse_metadata(self):
        fake = SimpleNamespace(st_mode=stat.S_IFREG, st_file_attributes=0x400)
        with mock.patch.object(Path, "lstat", return_value=fake), self.assertRaises(pe.Refused):
            command.ordinary(self.work / "fake.exe")

    def test_tool_snapshot_detects_actual_synthetic_change(self):
        path = self.work / "synthetic_tool.py"
        path.write_text("# synthetic one\n", encoding="utf-8")
        before = command.tool_snapshot(path)
        path.write_text("# synthetic two changed\n", encoding="utf-8")
        self.assertNotEqual(before, command.tool_snapshot(path))

    def test_path_handle_match_does_not_conflate_windows_ctime_apis(self):
        values = dict(st_dev=1, st_ino=2, st_size=3, st_mtime_ns=4)
        path = SimpleNamespace(**values, st_ctime_ns=5)
        handle = SimpleNamespace(**values, st_ctime_ns=6)
        self.assertTrue(command.same_file(path, handle))
        self.assertNotEqual(command.identity(path), command.identity(handle))
        handle.st_ino = 9
        self.assertFalse(command.same_file(path, handle))

    def test_cli_success_and_refusals_have_no_partial_json(self):
        data, base, _ = fixture(); path = self.work / "synthetic.exe"
        path.write_bytes(data)
        sha = hashlib.sha256(data).hexdigest()
        common = [sys.executable, "-B", str(TOOLS / "Inspect-PeImportSlots.py"), "--pe", str(path),
                  "--size", str(len(data)), "--sha256", sha, "--iat"]
        good = subprocess.run(common + [f"{base + 0x1300:08x}"], capture_output=True, text=True, timeout=15)
        self.assertEqual(0, good.returncode, good.stderr)
        result = json.loads(good.stdout)
        self.assertEqual("synthetic_read", result["imports"][0]["symbol"])
        self.assertTrue(result["sourceUnchanged"] and result["toolsUnchanged"])
        self.assertFalse(result["partialOutput"])
        bad = subprocess.run(common + [f"{base + 0x1300:08x}", f"{base + 0x1308:08x}"],
                             capture_output=True, text=True, timeout=15)
        self.assertEqual(1, bad.returncode); self.assertEqual("", bad.stdout)
        self.assertIn("refused", bad.stderr)
        self.assertEqual(bytes(data), path.read_bytes())

    def test_wrong_pins_relative_path_and_file_cap_refuse(self):
        data, base, _ = fixture(); path = self.work / "pins.exe"; path.write_bytes(data)
        sha = hashlib.sha256(data).hexdigest()
        for target, size, digest in ((path, len(data), "0" * 64), (path, len(data) - 1, sha),
                                     (Path("relative.exe"), len(data), sha),
                                     (path, 64 * 1024 * 1024 + 1, sha)):
            with self.subTest(target=target, size=size), self.assertRaises(pe.Refused):
                command.query(target, size, digest, [base + 0x1300])


if __name__ == "__main__":
    unittest.main(verbosity=2)
