"""Narrow evidenced formats, reviewed new meanings and private owned text inputs."""
import json
from pathlib import Path
import tempfile
import unittest
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "client_resources"))

from merge_resources import LITERAL_PERCENT_KEYS, merge, signature
from resource_table import ResourceTable
from test_resource_merge import table


class HeldResourceMergeTests(unittest.TestCase):
    def test_literal_percentage_grammar_is_scoped_to_the37_observed_keys(self):
        self.assertEqual(37, len(LITERAL_PERCENT_KEYS))
        for key in LITERAL_PERCENT_KEYS:
            with self.subTest(key=key):
                current = table(f"{key}=伤害减少50%\r\n")
                after, report = merge(current, table(f"{key}=Damage Reduction50%\r\n"), current)
                self.assertEqual([key], report["mergedKeys"])
                self.assertEqual("Damage Reduction50%", ResourceTable.parse(after).rows[key].value)
        current = table("42=伤害减少50%")
        after, report = merge(current, table("42=Damage Reduction50%"), current)
        self.assertEqual(current.raw, after)
        self.assertEqual([42], report["heldKeys"]["unsupportedFormatSyntax"])

    def test_literal_percent_values_order_and_ambiguous_forms_cannot_silently_change(self):
        for before, translated in (("5%", "10%"), ("50%/30%", "30%/50%"),
                                    ("50%", "50%d"), ("50%", "50%discount"),
                                    ("50%", "50.0%"), ("50%", "50%%"),
                                    ("+5%", "-5%"), ("50.0%", "60.0%"),
                                    ("1,000%", "2,000%")):
            with self.subTest(before=before, translated=translated):
                current = table("10450=中文" + before)
                after, report = merge(current, table("10450=English" + translated), current)
                self.assertEqual(current.raw, after)
                self.assertEqual(0, report["mergedCount"])

    def test_literal_percent_precedes_plain_prose_without_stealing_real_printf(self):
        key = 11328
        self.assertEqual(signature("中文50%减免", key=key), signature("English50% discount", key=key))
        self.assertEqual((("%", "%d"),), signature("10%d", key=key))
        self.assertEqual((("%", "% d"),), signature("% d", key=key))
        self.assertNotEqual(signature("中文50%减免", key=key), signature("English50% d", key=None))

    def test_observed_navigation_arrows_keep_exact_escape_and_arrow_sequence(self):
        current = table('10407=中文\\n"属性"->"设置"->"高级"->"常规"\\n中文')
        translated = table('10407=English\\n"Properties"->"Settings"->"Advanced"->"General"\\nEnglish')
        after, report = merge(current, translated, current)
        self.assertEqual([10407], report["mergedKeys"])
        self.assertEqual(translated.rows[10407].value, ResourceTable.parse(after).rows[10407].value)
        for changed in ('English\\n"Properties"->"Settings"\\nEnglish',
                        'English->\\n"Properties"->"Settings"->"General"\\nEnglish'):
            held, result = merge(current, table("10407=" + changed), current)
            self.assertEqual(current.raw, held)
            self.assertEqual(0, result["mergedCount"])
        with self.assertRaises(ValueError):
            signature("Properties->Settings", key=42)
