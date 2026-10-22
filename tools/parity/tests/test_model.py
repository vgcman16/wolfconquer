from copy import deepcopy
from dataclasses import FrozenInstanceError
import unittest

from tools.parity.model import MAX_INPUT_BYTES, StatusError, parse, validate, validate_commit
from tools.parity.tests.fixture import COMMIT, encoded, record


class ModelTests(unittest.TestCase):
    def test_valid_snapshot_keeps_distinct_dates_and_owned_immutable_rows(self):
        raw = record()
        status = validate(raw)
        self.assertEqual((55, 40, 70, "low"), (status.estimate.percent, status.estimate.low, status.estimate.high, status.estimate.confidence))
        self.assertEqual("2026-10-19", status.estimate.as_of)
        self.assertEqual("2026-10-22", status.reviewed_at)
        raw["domains"][0]["title"] = "changed caller buffer"
        self.assertEqual("Item storage", status.domains[0].title)
        with self.assertRaises(FrozenInstanceError):
            status.estimate.percent = 99
        self.assertEqual(status, parse(encoded()))

    def test_types_ranges_dates_unknown_keys_and_statuses_fail_closed(self):
        mutations = [
            (("schemaVersion",), True), (("schemaVersion",), 2),
            (("estimate", "percent"), True), (("estimate", "percent"), 55.0),
            (("estimate", "percent"), "55"), (("estimate", "percent"), -1),
            (("estimate", "percent"), 101), (("estimate", "low"), 56),
            (("estimate", "high"), 54), (("estimate", "confidence"), "LOW"),
            (("estimate", "asOf"), "2026-02-30"), (("estimate", "asOf"), "20261019"),
            (("estimate", "asOf"), "2026-10-23"), (("reviewedAt",), "2026-10-18"),
            (("sourceAudit", "date"), "2026-10-23"), (("sourceAudit", "files"), 0),
            (("sourceAudit", "lines"), False), (("sourceAudit", "targetComparison"), "full"),
            (("scope",), ""), (("scope",), "x\nspoof"), (("scope",), "\ud800"),
            (("domains", 0, "remaining"), []), (("domains", 0, "implemented"), "x" * 501),
            (("domains", 0, "status"), "verified"), (("milestones", 0, "status"), "done"),
            (("milestones", 0, "extra"), "unknown"), (("estimate", "extra"), 7),
            (("unknown",), 1), (("domains",), []), (("milestones",), []),
        ]
        for path, value in mutations:
            with self.subTest(path=path, value=repr(value)):
                raw = record()
                owner = raw
                for key in path[:-1]:
                    owner = owner[key]
                owner[path[-1]] = value
                with self.assertRaises(StatusError):
                    validate(raw)

    def test_missing_keys_duplicate_ids_required_boundaries_and_list_bounds(self):
        for mutate in (
            lambda raw: raw.pop("sourceAudit"),
            lambda raw: raw["milestones"].pop(),
            lambda raw: raw["domains"].append(deepcopy(raw["domains"][0])),
            lambda raw: raw["milestones"].append(deepcopy(raw["milestones"][0])),
            lambda raw: raw.update(domains=raw["domains"] * 13),
            lambda raw: raw.update(milestones=raw["milestones"] * 4),
        ):
            raw = record()
            mutate(raw)
            with self.assertRaises(StatusError):
                validate(raw)

    def test_json_duplicates_nonfinite_overlong_and_encoding_are_rejected(self):
        data = encoded()
        bad = [data.replace(b'"schemaVersion":1', b'"schemaVersion":1,"schemaVersion":1', 1),
               data.replace(b'"percent":55', b'"percent":55,"percent":55', 1),
               data.replace(b'"percent":55', b'"percent":NaN', 1),
               data.replace(b'"percent":55', b'"percent":Infinity', 1),
               data.replace(b'"percent":55', b'"percent":' + b"9" * 5000, 1),
               b" " * (MAX_INPUT_BYTES + 1), b"\xff", b"{broken", b"null"]
        for value in bad:
            with self.subTest(prefix=value[:40]):
                with self.assertRaises(StatusError):
                    parse(value)

    def test_hundred_requires_all_declared_work_done_but_does_not_assert_human_audit(self):
        raw = record()
        raw["estimate"].update(percent=100, high=100)
        with self.assertRaises(StatusError):
            validate(raw)
        for domain in raw["domains"]:
            domain["status"] = "implemented"
        for milestone in raw["milestones"]:
            milestone["status"] = "verified"
        self.assertEqual(100, validate(raw).estimate.percent)

    def test_commit_is_exact_and_canonical(self):
        self.assertEqual(COMMIT, validate_commit(COMMIT.upper()))
        for value in ("a" * 39, "a" * 41, "g" * 40, "HEAD", "../path", True):
            with self.assertRaises(StatusError):
                validate_commit(value)


if __name__ == "__main__":
    unittest.main()
