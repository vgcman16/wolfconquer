"""Strict version-one input contract. No clock, filesystem, Git, or network access."""
from __future__ import annotations

from dataclasses import dataclass
from datetime import date
import json
import re
import unicodedata

MAX_INPUT_BYTES = 65536
BOUNDARY_IDS = ("live-client", "public-build", "newer-client", "english")
DOMAIN_STATUSES = frozenset(("planned", "partial", "implemented"))
MILESTONE_STATUSES = frozenset(("planned", "in-progress", "verified"))


class StatusError(ValueError):
    """The supplied snapshot is not an accepted planning record."""


@dataclass(frozen=True)
class Estimate:
    percent: int
    low: int
    high: int
    confidence: str
    as_of: str


@dataclass(frozen=True)
class SourceAudit:
    date: str
    files: int
    lines: int
    target_comparison: str


@dataclass(frozen=True)
class Domain:
    id: str
    title: str
    status: str
    implemented: str
    remaining: str


@dataclass(frozen=True)
class Milestone:
    id: str
    title: str
    status: str
    detail: str


@dataclass(frozen=True)
class Status:
    estimate: Estimate
    reviewed_at: str
    scope: str
    source_audit: SourceAudit
    domains: tuple[Domain, ...]
    milestones: tuple[Milestone, ...]


def _object(value: object, keys: set[str], where: str) -> dict:
    if type(value) is not dict or set(value) != keys:
        raise StatusError(f"{where}: exact schema keys are required")
    return value


def _text(value: object, maximum: int, where: str) -> str:
    if (type(value) is not str or not value.strip() or len(value) > maximum
            or any(unicodedata.category(char) in {"Cc", "Cf", "Cs"} for char in value)):
        raise StatusError(f"{where}: bounded nonempty text without control characters is required")
    return value


def _integer(value: object, low: int, high: int, where: str) -> int:
    if type(value) is not int or not low <= value <= high:
        raise StatusError(f"{where}: integer outside the accepted bounds")
    return value


def _choice(value: object, choices: set | frozenset, where: str) -> str:
    if type(value) is not str or value not in choices:
        raise StatusError(f"{where}: unsupported status/value")
    return value


def _date(value: object, where: str) -> str:
    if type(value) is not str or re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value) is None:
        raise StatusError(f"{where}: ISO calendar date required")
    try:
        date.fromisoformat(value)
    except ValueError as error:
        raise StatusError(f"{where}: invalid calendar date") from error
    return value


def _identifier(value: object, where: str) -> str:
    if type(value) is not str or re.fullmatch(r"[a-z][a-z0-9-]{0,39}", value) is None:
        raise StatusError(f"{where}: lowercase identifier required")
    return value


def _rows(value: object, where: str) -> list:
    if type(value) is not list or not 1 <= len(value) <= 12:
        raise StatusError(f"{where}: one to twelve entries required")
    return value


def validate(value: object) -> Status:
    root = _object(value, {"schemaVersion", "estimate", "reviewedAt", "scope", "sourceAudit", "domains", "milestones"}, "root")
    _integer(root["schemaVersion"], 1, 1, "schemaVersion")
    raw = _object(root["estimate"], {"percent", "low", "high", "confidence", "asOf"}, "estimate")
    estimate = Estimate(_integer(raw["percent"], 0, 100, "estimate.percent"),
                        _integer(raw["low"], 0, 100, "estimate.low"),
                        _integer(raw["high"], 0, 100, "estimate.high"),
                        _choice(raw["confidence"], {"low", "medium", "high"}, "estimate.confidence"),
                        _date(raw["asOf"], "estimate.asOf"))
    if not estimate.low <= estimate.percent <= estimate.high:
        raise StatusError("estimate: low <= percent <= high is required")
    reviewed = _date(root["reviewedAt"], "reviewedAt")
    raw = _object(root["sourceAudit"], {"date", "files", "lines", "targetComparison"}, "sourceAudit")
    audit = SourceAudit(_date(raw["date"], "sourceAudit.date"),
                        _integer(raw["files"], 1, 1000000, "sourceAudit.files"),
                        _integer(raw["lines"], 1, 1000000000, "sourceAudit.lines"),
                        _choice(raw["targetComparison"], {"representative", "exhaustive"}, "sourceAudit.targetComparison"))
    if estimate.as_of > reviewed or audit.date > reviewed:
        raise StatusError("reviewedAt cannot precede the estimate or source audit")
    domains = []
    for item in _rows(root["domains"], "domains"):
        item = _object(item, {"id", "title", "status", "implemented", "remaining"}, "domain")
        domains.append(Domain(_identifier(item["id"], "domain.id"), _text(item["title"], 60, "domain.title"),
                              _choice(item["status"], DOMAIN_STATUSES, "domain.status"),
                              _text(item["implemented"], 500, "domain.implemented"),
                              _text(item["remaining"], 500, "domain.remaining")))
    milestones = []
    for item in _rows(root["milestones"], "milestones"):
        item = _object(item, {"id", "title", "status", "detail"}, "milestone")
        milestones.append(Milestone(_identifier(item["id"], "milestone.id"), _text(item["title"], 60, "milestone.title"),
                                    _choice(item["status"], MILESTONE_STATUSES, "milestone.status"),
                                    _text(item["detail"], 500, "milestone.detail")))
    if len({item.id for item in domains}) != len(domains) or len({item.id for item in milestones}) != len(milestones):
        raise StatusError("domain/milestone identifiers must be unique within their lists")
    if not set(BOUNDARY_IDS).issubset(item.id for item in milestones):
        raise StatusError("milestones must include live-client, public-build, newer-client, and english")
    if estimate.percent == 100 and (any(item.status != "implemented" for item in domains)
                                    or any(item.status != "verified" for item in milestones)):
        raise StatusError("100 requires every domain implemented and every milestone verified; human audit is still required")
    return Status(estimate, reviewed, _text(root["scope"], 120, "scope"), audit, tuple(domains), tuple(milestones))


def _unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise StatusError("duplicate JSON property")
        result[key] = value
    return result


def _invalid_constant(_: str) -> None:
    raise StatusError("non-finite JSON numbers are forbidden")


def parse(data: bytes) -> Status:
    if type(data) is not bytes or len(data) > MAX_INPUT_BYTES:
        raise StatusError("status input exceeds its byte bound or is not bytes")
    try:
        value = json.loads(data.decode("utf-8", errors="strict"), object_pairs_hook=_unique_object,
                           parse_constant=_invalid_constant)
    except StatusError:
        raise
    except (UnicodeError, ValueError, RecursionError) as error:
        raise StatusError("status must be bounded valid UTF-8 JSON") from error
    return validate(value)


def validate_commit(commit: str) -> str:
    if type(commit) is not str or re.fullmatch(r"[0-9a-fA-F]{40}", commit) is None:
        raise StatusError("commit must contain exactly forty hexadecimal characters")
    return commit.lower()
