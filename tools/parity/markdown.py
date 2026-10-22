"""Complete generated prose; card shortening never drops detail from this document."""
from .model import Status, validate_commit


def _escape(value: str) -> str:
    for char in "\\`*_{}[]<>|":
        value = value.replace(char, "\\" + char)
    return value


def render_markdown(status: Status, commit: str) -> bytes:
    commit = validate_commit(commit)
    estimate = status.estimate
    audit = status.source_audit
    rows = ["# WolfConquer project progress", "",
            "Generated from `status.json` by `tools/parity/generate.py`. Do not edit this generated document.", "",
            f"Scope: {_escape(status.scope)}.", "",
            f"Planning estimate: **{estimate.percent}%**, range **{estimate.low}-{estimate.high}%**, "
            f"**{estimate.confidence} confidence**. This is not a measured completion score.", "",
            f"Estimate date: **{estimate.as_of}**. Status review date: **{status.reviewed_at}**. "
            "Reviewing the status does not refresh the estimate's age.", "",
            f"Source commit: `{commit}` (short: `{commit[:12]}`).", "",
            f"Source audit: {audit.date}; {audit.files:,} files; {audit.lines:,} lines; "
            f"target comparison: **{audit.target_comparison}**. Inventory counts are not implementation proof.", "",
            "A green test suite is not a live-client, complete-English, or full-parity certification. "
            "A full-completion claim requires a human audit even when every listed item is marked done.", "",
            "## Milestones and verification boundaries", "",
            "| Milestone | Status | Detail |", "| --- | --- | --- |"]
    rows.extend(f"| {_escape(item.title)} | {item.status} | {_escape(item.detail)} |" for item in status.milestones)
    rows += ["", "## Legacy-server domains", "", "| Domain | Status | Implemented evidence | Remaining work |",
             "| --- | --- | --- | --- |"]
    rows.extend(f"| {_escape(item.title)} | {item.status} | {_escape(item.implemented)} | {_escape(item.remaining)} |"
                for item in status.domains)
    rows += ["", "The GIF animates decorative shine only; its estimate fill never grows between frames.", ""]
    return "\n".join(rows).encode("utf-8")
