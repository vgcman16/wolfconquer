"""Synthetic reviewed exceptions and proposal-only storage."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "client_resources"))
from propose import write_proposal
from reviewed import complete, reviewed_values
from test_resource_merge import table


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def review(current):
    return {"schemaVersion": 1, "targetBeforeSha256": sha(current.raw), "entries": [
        {"key": 10375, "expectedValueSha256": sha(current.rows[10375].value.encode()),
         "english": "Example Brand", "reason": "explicit title review"},
        {"key": 11090, "expectedValueSha256": sha(current.rows[11090].value.encode()),
         "english": "Confirm %I64u and %I64u?", "reason": "reviewed current placeholders"}]}


class ReviewedProposalTests(unittest.TestCase):
    def test_duplicate_json_members_do_not_create_review_authority(self):
        current = table("10375=新标题\n11090=新捐赠 %I64u %I64u\n")
        document = json.dumps(review(current))
        document = document.replace('"schemaVersion": 1', '"schemaVersion": 1,"schemaVersion": 1', 1)
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "review.json"
            path.write_text(document, encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "reviewedDuplicateJsonMember"):
                reviewed_values(current, current, path)

    def test_branding_and_changed_donation_require_explicit_current_review(self):
        original = table("10375=旧标题\n11090=旧捐赠 %I64u %d\n100077=机器路径\n")
        english = table("10375=Old Brand\n11090=Old %I64u %d\n100077=机器路径\n")
        current = table("10375=新标题\n11090=新捐赠 %I64u %I64u\n100077=机器路径\n")
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "review.json"
            path.write_text(json.dumps(review(current)), encoding="utf-8")
            after, report = complete(original, english, current, path)
            final = type(current).parse(after)
            self.assertEqual("Example Brand", final.rows[10375].value)
            self.assertEqual("Confirm %I64u and %I64u?", final.rows[11090].value)
            self.assertEqual(current.rows[100077].value, final.rows[100077].value)
            self.assertEqual([10375, 11090], report["reviewedOverrideKeys"])
            self.assertNotIn("Example Brand", json.dumps(report))
            for mutation in ("hash", "format", "duplicate", "injection"):
                value = review(current)
                if mutation == "hash": value["targetBeforeSha256"] = "0" * 64
                elif mutation == "format": value["entries"][1]["english"] = "%I64u %d"
                elif mutation == "duplicate": value["entries"][1] = value["entries"][0].copy()
                else: value["entries"][0]["english"] = "Brand\n4=injected"
                path.write_text(json.dumps(value), encoding="utf-8")
                with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                    reviewed_values(current, current, path)

    def test_proposal_is_fresh_child_and_report_contains_no_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            data, report = b"synthetic proposal", {"mergedKeys": [1]}
            first = write_proposal(root, data, report)
            second = write_proposal(root, data, report)
            self.assertNotEqual(first, second)
            self.assertEqual(root / "proposals", first.parent)
            self.assertEqual({"UStrRes.ini", "merge-report.json"}, {p.name for p in first.iterdir()})
            self.assertEqual(data, (first / "UStrRes.ini").read_bytes())
            self.assertEqual(report, json.loads((first / "merge-report.json").read_text()))
