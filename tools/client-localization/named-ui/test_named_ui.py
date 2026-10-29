"""Synthetic owned resource fixtures; no actual client or proprietary row payloads."""
import copy
import unittest

from named_resources import NamedResources
from propose_named_ui import digest, propose, unique_json

KEY = "STR_PLAYER_LIFE"


def table(text, bom=b""):
    return NamedResources.parse(bom + text.encode("utf-8"))


def plan_for(original, english, current, keys=(KEY,)):
    return {"schemaVersion": 1, "family": "character-stat-ui", "codec": "utf-8",
            "inputSha256": {name: digest(document.raw) for name, document in
                            zip(("original", "english", "current"), (original, english, current))},
            "keys": list(keys)}


class NamedUiTests(unittest.TestCase):
    def test_three_way_ports_only_reviewed_current_meaning_preserving_raw_other_rows(self):
        original = table(f"{KEY}=生命:%d\r\nSTR_OTHER=旧文本\r\n")
        english = table(f"{KEY}=HP:%d\r\nSTR_OTHER=Old Text\r\n")
        current = table(f";注释\nSTR_%d[0]-&1&=新值=额外列\r\n{KEY}=生命:%d\rSTR_OTHER=新文本", b"\xef\xbb\xbf")
        after, report = propose(original, english, current, plan_for(original, english, current))
        self.assertEqual(b"\xef\xbb\xbf" + f";注释\nSTR_%d[0]-&1&=新值=额外列\r\n{KEY}=HP:%d\rSTR_OTHER=新文本".encode(), after)
        self.assertEqual((1, 1), (report["mergedKeyCount"], report["changedPhysicalRows"]))
        self.assertFalse(report["clientApplied"])

    def test_uniform_three_occurrence_groups_port_all_without_first_last_precedence(self):
        original = table((f"{KEY}=生命:%d\r\n") * 3)
        english = table((f"{KEY}=HP:%d\r\n") * 3)
        current = table(f"{KEY}=生命:%d\nSTR_NEW=保持\n{KEY}=生命:%d\r\n{KEY}=生命:%d")
        after, report = propose(original, english, current, plan_for(original, english, current))
        parsed = NamedResources.parse(after)
        self.assertEqual(["HP:%d"] * 3, [row.value for row in parsed.groups[KEY]])
        self.assertEqual(3, report["changedPhysicalRows"])
        self.assertEqual("保持", parsed.groups["STR_NEW"][0].value)

    def test_missing_count_drift_nonuniform_and_changed_meaning_are_held_unchanged(self):
        original = table(f"{KEY}=生命:%d")
        english = table(f"{KEY}=HP:%d")
        for text, reason in (("STR_NEW=新值", "requiredKeyMissing"),
                             ((f"{KEY}=生命:%d\n") * 2, "occurrenceCountChanged"),
                             (f"{KEY}=血量:%d", "currentChineseMeaningChanged")):
            with self.subTest(reason=reason):
                current = table(text)
                after, report = propose(original, english, current, plan_for(original, english, current))
                self.assertEqual(current.raw, after)
                self.assertEqual([KEY], report["held"][reason])
        original = table(f"{KEY}=生命:%d\n{KEY}=另一种含义:%d")
        english = table(f"{KEY}=HP:%d\n{KEY}=HP:%d")
        current = table(f"{KEY}=生命:%d\n{KEY}=生命:%d")
        after, report = propose(original, english, current, plan_for(original, english, current))
        self.assertEqual(current.raw, after)
        self.assertEqual([KEY], report["held"]["nonUniformDuplicateMeaning"])

    def test_all_tag_printf_escape_tokens_and_their_combined_order_are_preserved(self):
        before = '<color=0xff>生命:%0.2f%%</color>\\n'
        donor = '<color=0xff>HP:%0.2f%%</color>\\n'
        original, english, current = (table(f"{KEY}={value}") for value in (before, donor, before))
        after, report = propose(original, english, current, plan_for(original, english, current))
        self.assertEqual(donor, NamedResources.parse(after).groups[KEY][0].value)
        self.assertEqual(1, report["mergedKeyCount"])
        for changed in (donor.replace("%0.2f", "%d"), donor.replace("0xff", "0xfe"),
                        donor.replace("</color>\\n", "\\n</color>"), donor.replace("%%", "%")):
            english = table(f"{KEY}={changed}")
            held, report = propose(original, english, current, plan_for(original, english, current))
            self.assertEqual(current.raw, held)
            self.assertEqual(0, report["mergedKeyCount"])

    def test_machine_urls_registry_fonts_and_non_display_values_are_never_translated(self):
        for value in ("https://example.invalid/中文", r"SOFTWARE\中文", r"C:\中文", "Font/中文.ttq", "%d"):
            with self.subTest(value=value):
                original = current = table(f"{KEY}={value}")
                english = table(f"{KEY}=English")
                after, report = propose(original, english, current, plan_for(original, english, current))
                self.assertEqual(current.raw, after)
                self.assertEqual([KEY], report["held"]["machineOrNonDisplayValue"])

    def test_non_english_donor_and_unsupported_percentage_remain_held(self):
        for value, english_value in (("生命", "生命English"), ("生命50%", "HP50%")):
            with self.subTest(value=value):
                original = current = table(f"{KEY}={value}")
                english = table(f"{KEY}={english_value}")
                after, report = propose(original, english, current, plan_for(original, english, current))
                self.assertEqual(current.raw, after)
                self.assertEqual(0, report["mergedKeyCount"])

    def test_invalid_tail_encoding_nul_and_injected_output_never_get_repaired(self):
        for raw in (b"STR_A=ok\nunknown-tail", b"STR_A=\xff", b"STR_A=ok\0", b"=empty-key"):
            with self.subTest(raw=raw):
                with self.assertRaises((ValueError, UnicodeError)):
                    NamedResources.parse(raw)
        with self.assertRaises(ValueError):
            table(f"{KEY}=生命").replace_values({KEY: "HP\nSTR_INJECT=Injected"})

    def test_input_pin_unknown_plan_fields_and_duplicate_plan_keys_refuse(self):
        original = current = table(f"{KEY}=生命")
        english = table(f"{KEY}=HP")
        good = plan_for(original, english, current)
        for changed in (dict(good, unknown=True), dict(good, keys=[KEY, KEY]), dict(good, schemaVersion=True)):
            with self.assertRaises(ValueError):
                propose(original, english, current, changed)
        changed = copy.deepcopy(good)
        changed["inputSha256"]["current"] = "0" * 64
        with self.assertRaises(ValueError):
            propose(original, english, current, changed)
        with self.assertRaises(ValueError):
            unique_json([("key", 1), ("key", 2)])


if __name__ == "__main__":
    unittest.main()
