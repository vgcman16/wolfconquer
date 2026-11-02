"""Owned text fixtures only; no client execution, routing or live file mutation."""
from pathlib import Path
import tempfile
import unittest
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "client_resources"))

from merge_resources import merge, signature
from resource_table import MAX_BYTES, ResourceTable, read_table


def table(text, codec="utf-16-le", bom=b"\xff\xfe"):
    return ResourceTable.parse(bom + text.encode(codec), codec)


class ResourceMergeTests(unittest.TestCase):
    def test_machine_lookup_literal_never_enters_automatic_translation(self):
        current = table("1=普通文本\r\n100077=机器查找键\r\n")
        translated = table("1=Visible Text\n100077=Changed lookup key\n")
        after, report = merge(current, translated, current)
        final = ResourceTable.parse(after)
        self.assertEqual("Visible Text", final.rows[1].value)
        self.assertEqual(current.rows[100077].value, final.rows[100077].value)
        self.assertEqual([1], report["mergedKeys"])
        self.assertEqual([100077], report["heldKeys"]["machineLookupLiteral"])

    def test_three_way_preserves_new_layout_and_changed_meanings(self):
        original = table("1=力量:%d\r\n2=旧标题\r\n3=删除行\r\n")
        english = table("1=Strength:%d\r\n2=Old Title\r\n3=Removed\r\n")
        current = table(";新注释\r\n  1 =力量:%d\n2=新标题\r\n4=新功能\r")
        after, report = merge(original, english, current)
        self.assertEqual(b"\xff\xfe" + ";新注释\r\n  1 =Strength:%d\n2=新标题\r\n4=新功能\r".encode("utf-16-le"), after)
        self.assertEqual([1], report["mergedKeys"])
        self.assertEqual([2], report["heldKeys"]["chineseMeaningChanged"])
        self.assertEqual([4], report["heldKeys"]["originalChineseMissing"])
        self.assertNotIn(3, ResourceTable.parse(after).rows)
        self.assertEqual("旧标题", original.rows[2].value)

    def test_printf_width_type_order_dynamic_and_literal_percent_are_required(self):
        for before, changed in (("%d", "%u"), ("%02d", "%d"),
                                ("%s:%d", "%d:%s"), ("%*.*f", "%f"),
                                ("%I64u", "%u"), ("%d%%", "%d%")):
            with self.subTest(before=before, changed=changed):
                original = table("1=中文" + before)
                after, report = merge(original, table("1=English" + changed), original)
                self.assertEqual(original.raw, after)
                self.assertEqual(0, report["mergedCount"])

    def test_matching_formats_escapes_markup_and_controls_port_exactly(self):
        suffix = r"%02d/%I64u%%\n\x1B{0}<color=red>" + "\t"
        current = table("1=中文" + suffix)
        after, report = merge(current, table("1=English" + suffix), current)
        self.assertEqual("English" + suffix, ResourceTable.parse(after).rows[1].value)
        self.assertEqual([1], report["mergedKeys"])

    def test_changed_escapes_markup_braces_and_controls_stay_chinese(self):
        for before, changed in ((r"\x01", r"\x02"), (r"\n", r"\t"),
                                ("{0}", "{1}"), ("<red>", "<blue>"),
                                ("\t", ""), ("\\", "")):
            with self.subTest(before=before):
                current = table("1=中文" + before)
                after, report = merge(current, table("1=English" + changed), current)
                self.assertEqual(current.raw, after)
                self.assertEqual(0, report["mergedCount"])

    def test_cross_category_token_reordering_is_not_compatible_formatting(self):
        for before, changed in ((r"%d\n%s", r"%d%s\n"),
                                ("<red>%d</red>", "<red></red>%d"),
                                ("%d\t%s", "%d%s\t"),
                                ("{0}%d", "%d{0}")):
            with self.subTest(before=before, changed=changed):
                current = table("1=中文" + before)
                after, report = merge(current, table("1=English" + changed), current)
                self.assertEqual(current.raw, after)
                self.assertEqual([1], report["heldKeys"]["formatSignatureChanged"])

    def test_cp936_roundtrip_and_unrepresentable_value_are_safe(self):
        current = table(";注释\r\n1=中文:%d\r\n2=消息\r\n", "cp936", b"")
        translated = table("1=English:%d\n2=Message😀\n")
        after, report = merge(current, translated, current)
        self.assertEqual(";注释\r\n1=English:%d\r\n2=消息\r\n".encode("cp936"), after)
        self.assertEqual("cp936", report["targetCodec"])
        self.assertEqual([2], report["heldKeys"]["targetEncodingCannotRepresentTranslation"])

    def test_duplicate_normalized_ids_and_sectioned_unknown_shapes_refuse(self):
        for text in ("1=a\n01=b", "[section]\n1=a", "name=value", "1=a\x00"):
            with self.subTest(text=text):
                with self.assertRaises(ValueError):
                    table(text)

    def test_missing_encoding_invalid_payload_and_injected_rows_refuse(self):
        with self.assertRaises(ValueError):
            ResourceTable.parse(b"1=abc")
        with self.assertRaises(UnicodeError):
            ResourceTable.parse(b"\xff\xfe1")
        with self.assertRaises(ValueError):
            ResourceTable.parse(b"x" * (MAX_BYTES + 1), "utf-8")
        with self.assertRaises(ValueError):
            table("1=one").with_values({1: "one\n2=injected"})

    def test_file_reader_uses_only_ordinary_owned_bounded_input(self):
        with tempfile.TemporaryDirectory(prefix="wolf-resource-merge-") as root:
            path = Path(root) / "UStrRes.ini"
            source = table("1=中文").raw
            path.write_bytes(source)
            self.assertEqual(source, read_table(path).raw)
            self.assertEqual(source, path.read_bytes())
            with self.assertRaises(ValueError):
                read_table(Path(root))
