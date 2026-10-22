"""Independent synthetic planning record, not the repository's live status data."""
import json

COMMIT = "a" * 40


def record() -> dict:
    return {
        "schemaVersion": 1,
        "estimate": {"percent": 55, "low": 40, "high": 70, "confidence": "low", "asOf": "2026-10-19"},
        "reviewedAt": "2026-10-22",
        "scope": "Strict legacy-server parity (project planning)",
        "sourceAudit": {"date": "2026-10-19", "files": 784, "lines": 260144, "targetComparison": "representative"},
        "domains": [{"id": "storage", "title": "Item storage", "status": "partial",
                     "implemented": "Owned component fixtures exist.", "remaining": "Complete integrated persistence remains."}],
        "milestones": [
            {"id": "live-client", "title": "Limited live-client proof", "status": "verified",
             "detail": "A bounded local client interaction is not full server parity."},
            {"id": "public-build", "title": "Public build not live-verified", "status": "in-progress",
             "detail": "Public checks do not prove the public build was played through."},
            {"id": "newer-client", "title": "Newer client not estimated", "status": "planned",
             "detail": "Newer-client work is outside this legacy estimate."},
            {"id": "english", "title": "Full English unfinished", "status": "in-progress",
             "detail": "Untranslated resources remain; complete English is not claimed."},
        ],
    }


def encoded(value: dict | None = None) -> bytes:
    return json.dumps(record() if value is None else value, separators=(",", ":")).encode("utf-8")
