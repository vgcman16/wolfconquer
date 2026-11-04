"""Synthetic pinned physical variants; no private client rows or lookup claims."""
import copy
import unittest
from unittest.mock import patch

from named_resources import NamedResources
from propose_named_ui import digest, propose
from test_named_ui import plan_for, table
from variant_proposals import _replace_rows, propose_variants

KEY = "STR_SYNTHETIC_LABEL"
RED = "<color=0x11>甲数:%d</color>"
BLUE = "<color=0x22>乙文:%s</color>"
EN_RED = "<color=0x11>Amount:%d</color>"
EN_BLUE = "<color=0x22>Label:%s</color>"


def fixtures():
    original = table(f"; synthetic\n{KEY}={RED}\r\nSTR_KEEP=未选\n{KEY}={BLUE}\r{KEY}={RED}", b"\xef\xbb\xbf")
    english = table(f"; donor\n{KEY}={EN_RED}\nSTR_KEEP=Unused\n{KEY}={EN_BLUE}\n{KEY}={EN_RED}\n")
    current = table(f"; synthetic\n{KEY}={RED}\r\nSTR_KEEP=Already translated\n{KEY}={BLUE}\r{KEY}={RED}", b"\xef\xbb\xbf")
    return original, english, current


def variant_for(original, english, key, line):
    source = next(row for row in original.groups[key] if row.index + 1 == line)
    donor = next(row for row in english.groups[key] if row.index + 1 == line)
    occurrences = [row.index + 1 for row in original.groups[key] if row.value == source.value]
    return {"key": key, "originalValueSha256": digest(source.value.encode()),
            "englishValueSha256": digest(donor.value.encode()), "occurrences": occurrences,
            "occurrenceCount": len(occurrences)}


def variant_plan(original, english, current, entries=None):
    return {"schemaVersion": 1, "family": "core-ui", "codec": "utf-8",
            "inputSha256": {name: digest(document.raw) for name, document in
                            zip(("original", "english", "current"), (original, english, current))},
            "variants": entries if entries is not None else
            [variant_for(original, english, KEY, 2), variant_for(original, english, KEY, 4)]}


class NamedUiVariantTests(unittest.TestCase):
    def test_exact_two_variant_golden_preserves_every_other_byte_and_separate_counts(self):
        documents = fixtures()
        after, report = propose_variants(*documents, variant_plan(*documents))
        golden = b"\xef\xbb\xbf" + f"; synthetic\n{KEY}={EN_RED}\r\nSTR_KEEP=Already translated\n{KEY}={EN_BLUE}\r{KEY}={EN_RED}".encode()
        self.assertEqual(golden, after)
        self.assertEqual((1, 2, 1, 2, 3), (report["selectedKeyCount"], report["selectedVariantCount"],
                         report["mergedKeyCount"], report["mergedVariantCount"], report["changedPhysicalRows"]))
        self.assertEqual([KEY], report["mergedKeys"])
        self.assertEqual([[2, 5], [4]], [entry["occurrences"] for entry in report["mergedVariants"]])
        self.assertFalse(report["clientApplied"])
        self.assertFalse(report["liveRenderingVerified"])
        self.assertEqual({}, report["held"])

    def test_uniform_route_still_holds_the_same_nonuniform_key(self):
        documents = fixtures()
        after, report = propose(*documents, plan_for(*documents, keys=(KEY,)))
        self.assertEqual(documents[2].raw, after)
        self.assertEqual(0, report["mergedKeyCount"])
        self.assertEqual([KEY], report["held"]["nonUniformDuplicateMeaning"])

    def test_one_variant_keeps_prior_other_variant_translation_and_every_unselected_byte(self):
        original, english, current = fixtures()
        current = NamedResources.parse(current.raw.replace(RED.encode(), EN_RED.encode()))
        plan = variant_plan(original, english, current, [variant_for(original, english, KEY, 4)])
        after, report = propose_variants(original, english, current, plan)
        self.assertEqual(current.raw.replace(BLUE.encode(), EN_BLUE.encode()), after)
        self.assertEqual((1, 1, 1), (report["mergedKeyCount"], report["mergedVariantCount"], report["changedPhysicalRows"]))

    def test_raw_pins_override_forged_parsed_metadata_in_every_role(self):
        documents = fixtures()
        plan = variant_plan(*documents)
        golden, _ = propose_variants(*documents, plan)
        forged = table(f"{KEY}=Forged\nSTR_KEEP=Different\n")
        for index in range(3):
            with self.subTest(role=index):
                supplied = list(documents)
                supplied[index] = NamedResources(supplied[index].raw, forged.bom, forged.lines, forged.groups)
                after, _ = propose_variants(*supplied, plan)
                self.assertEqual(golden, after)

    def test_wrong_source_or_donor_pin_line_and_partial_variant_stay_unchanged(self):
        documents = fixtures()
        for field, value, reason in (
                ("originalValueSha256", "0" * 64, "originalVariantOccurrencesChanged"),
                ("englishValueSha256", "0" * 64, "englishVariantOccurrencesChanged"),
                ("occurrences", [1, 5], "originalVariantOccurrencesChanged"),
                ("occurrences", [2], "originalVariantOccurrencesChanged")):
            with self.subTest(field=field, value=value):
                plan = variant_plan(*documents)
                plan["variants"] = [plan["variants"][0]]
                plan["variants"][0][field] = value
                plan["variants"][0]["occurrenceCount"] = len(plan["variants"][0]["occurrences"])
                after, report = propose_variants(*documents, plan)
                self.assertEqual(documents[2].raw, after)
                self.assertEqual(0, report["mergedVariantCount"])
                self.assertEqual(1, len(report["held"][reason]))

    def test_changed_selected_current_does_not_rewrite_it_or_block_unrelated_good_variant(self):
        original, english, current = fixtures()
        current = NamedResources.parse(current.raw.replace(RED.encode(), "已改:%d".encode(), 1))
        after, report = propose_variants(original, english, current, variant_plan(original, english, current))
        self.assertEqual(current.raw.replace(BLUE.encode(), EN_BLUE.encode()), after)
        self.assertEqual(1, report["mergedVariantCount"])
        self.assertEqual([2, 5], report["held"]["currentChineseMeaningChanged"][0]["occurrences"])

    def test_shifted_missing_count_changed_or_aliased_keys_are_held(self):
        base = fixtures()
        for role in range(3):
            for change, reason in (
                    (lambda raw: (b"\xef\xbb\xbf; shifted\n" + raw[3:]) if raw.startswith(b"\xef\xbb\xbf")
                     else b"; shifted\n" + raw, "physicalKeyPositionsChanged"),
                    (lambda raw: raw.replace(KEY.encode(), b"STR_OTHER"), "requiredKeyMissing"),
                    (lambda raw: raw + f"\n{KEY}=额外".encode(), "occurrenceCountChanged"),
                    (lambda raw: raw + f"\n{KEY.lower()}=别名".encode(), "caseAliasAmbiguous")):
                with self.subTest(role=role, reason=reason):
                    documents = list(base)
                    documents[role] = NamedResources.parse(change(documents[role].raw))
                    # Pin changed inputs but keep the independently selected source variant.
                    plan = variant_plan(*base)
                    plan["inputSha256"] = {name: digest(document.raw) for name, document in
                        zip(("original", "english", "current"), documents)}
                    after, report = propose_variants(*documents, plan)
                    self.assertEqual(documents[2].raw, after)
                    self.assertEqual(0, report["mergedVariantCount"])
                    self.assertEqual(2, len(report["held"][reason]))

    def test_unknown_key_and_donor_value_at_wrong_physical_positions_are_not_associated(self):
        original, english, current = fixtures()
        plan = variant_plan(original, english, current)
        plan["variants"] = [dict(plan["variants"][0], key="STR_UNKNOWN")]
        after, report = propose_variants(original, english, current, plan)
        self.assertEqual(current.raw, after)
        self.assertIn("requiredKeyMissing", report["held"])
        english = NamedResources.parse(english.raw.replace(EN_RED.encode(), b"Other text", 1))
        plan = variant_plan(original, english, current, [variant_for(original, english, KEY, 5)])
        after, report = propose_variants(original, english, current, plan)
        self.assertEqual(current.raw, after)
        self.assertIn("englishVariantOccurrencesChanged", report["held"])

    def test_shared_donor_value_between_distinct_variants_remains_explicitly_ambiguous(self):
        original = current = table(f"{KEY}=甲\n{KEY}=乙\n")
        english = table(f"{KEY}=Value\n{KEY}=Value\n")
        plan = variant_plan(original, english, current,
                            [variant_for(original, english, KEY, line) for line in (1, 2)])
        after, report = propose_variants(original, english, current, plan)
        self.assertEqual(current.raw, after)
        self.assertEqual(2, len(report["held"]["englishVariantOccurrencesChanged"]))

    def test_invalid_shape_count_overlap_case_alias_and_unknown_fields_raise(self):
        documents = fixtures()
        good = variant_plan(*documents)
        mutations = [lambda p: p.update(extra=True), lambda p: p.update(schemaVersion=True),
                     lambda p: p.update(family="equipment-ui"), lambda p: p.update(variants=[]),
                     lambda p: p.update(variants=p["variants"] * 101),
                     lambda p: p["variants"][0].update(extra=True),
                     lambda p: p["variants"][0].update(occurrenceCount=True),
                     lambda p: p["variants"][0].update(occurrenceCount=3),
                     lambda p: p["variants"][0].update(occurrences=[2, 2]),
                     lambda p: p["variants"][0].update(occurrences=[5, 2]),
                     lambda p: p["variants"][0].update(occurrences=[0, 5]),
                     lambda p: p["variants"][0].update(occurrences=[True, 5]),
                     lambda p: p["variants"][0].update(originalValueSha256="a" * 64),
                     lambda p: p["variants"][0].update(englishValueSha256=None),
                     lambda p: p["variants"].append(copy.deepcopy(p["variants"][0])),
                     lambda p: p["variants"][1].update(occurrences=[2], occurrenceCount=1),
                     lambda p: p["variants"][1].update(key="STR_SYNTHETIC_label"),
                     lambda p: p["variants"][0].update(key="STR_BAD-HYPHEN"),
                     lambda p: p["inputSha256"].update(current="0" * 64)]
        for index, mutate in enumerate(mutations):
            with self.subTest(mutation=index):
                plan = copy.deepcopy(good)
                mutate(plan)
                with self.assertRaises(ValueError):
                    propose_variants(*documents, plan)

    def test_exact_key_literals_color_printf_escape_mark_and_donor_guards_remain(self):
        for key in ("STR_Mixed_name", "STR_BTN_TIP_003-07"):
            before = "★【甲】：<color=0x11>%d</color>\\n50% -> %s"
            donor = "★【Text】：<color=0x11>%d</color>\\n50% -> %s"
            original = current = table(f"{key}={before}\n")
            for changed, valid in ((donor, True), (donor.replace("0x11", "0x12"), False),
                    (donor.replace("%d", "%s"), False), (donor.replace("\\n", "\\t"), False),
                    (donor.replace("50%", "60%"), False), (donor.replace("★", ""), False),
                    (donor.replace("Text", "Texteé"), False), (donor.replace("Text", "Text\u202e"), False),
                    (donor.replace("Text", "全角Text"), False), (donor + "<broken", False),
                    (donor.replace("%s", "%?"), False)):
                with self.subTest(key=key, donor=changed):
                    english = table(f"{key}={changed}\n")
                    plan = variant_plan(original, english, current, [variant_for(original, english, key, 1)])
                    after, report = propose_variants(original, english, current, plan)
                    self.assertEqual(int(valid), report["mergedVariantCount"])
                    self.assertEqual(english.raw if valid else current.raw, after)

    def test_machine_non_display_and_unchanged_values_are_held(self):
        for before, donor, reason in (("https://example.invalid/甲", "English", "machineOrNonDisplayValue"),
                ("甲", "Font/test", "machineOrNonDisplayValue"), ("%d", "Value%d", "machineOrNonDisplayValue"),
                ("甲", "甲", "translationUnchanged"), ("甲", "123", "englishDonorRequired")):
            with self.subTest(before=before, donor=donor):
                original = current = table(f"{KEY}={before}")
                english = table(f"{KEY}={donor}")
                plan = variant_plan(original, english, current, [variant_for(original, english, KEY, 1)])
                after, report = propose_variants(original, english, current, plan)
                self.assertEqual(current.raw, after)
                self.assertIn(reason, report["held"])

    def test_output_bounds_and_row_injection_refuse_before_output(self):
        documents = fixtures()
        with patch("variant_proposals.MAX_BYTES", 100):
            with self.assertRaisesRegex(ValueError, "namedResourceProposedBounds"):
                propose_variants(*documents, variant_plan(*documents))
        with self.assertRaisesRegex(ValueError, "namedResourceRowInjection"):
            _replace_rows(documents[2], {1: "Injected\nSTR_INJECT=Row"})


if __name__ == "__main__":
    unittest.main()
