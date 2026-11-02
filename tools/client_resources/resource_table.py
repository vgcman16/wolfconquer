"""Bounded flat numeric resource tables; retain target layout and encoding."""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re
import stat

MAX_BYTES = 1024 * 1024
ROW = re.compile(r"^([ \t]*[0-9]+[ \t]*=)([^\r\n]*)$")


@dataclass(frozen=True)
class ResourceRow:
    index: int
    prefix: str
    value: str
    ending: str


@dataclass(frozen=True)
class ResourceTable:
    raw: bytes
    codec: str
    bom: bytes
    lines: tuple[str, ...]
    rows: dict[int, ResourceRow]

    @staticmethod
    def parse(raw: bytes, encoding: str | None = None) -> "ResourceTable":
        if not raw or len(raw) > MAX_BYTES:
            raise ValueError("resourceSizeInvalid")
        bom = b""
        for prefix, codec in ((b"\xff\xfe", "utf-16-le"),
                              (b"\xfe\xff", "utf-16-be"),
                              (b"\xef\xbb\xbf", "utf-8")):
            if raw.startswith(prefix):
                bom = prefix
                if encoding not in (None, codec):
                    raise ValueError("resourceEncodingConflict")
                encoding = codec
                break
        if encoding not in ("utf-16-le", "utf-16-be", "utf-8", "cp936"):
            raise ValueError("resourceExplicitEncodingRequired")
        payload = raw[len(bom):]
        text = payload.decode(encoding, errors="strict")
        if text.encode(encoding, errors="strict") != payload or "\x00" in text:
            raise ValueError("resourceEncodingRoundTripInvalid")
        lines = re.findall(r"[^\r\n]*(?:\r\n|\r|\n|$)", text)
        if lines and lines[-1] == "":
            lines.pop()
        rows: dict[int, ResourceRow] = {}
        for index, line in enumerate(lines):
            body = line.rstrip("\r\n")
            ending = line[len(body):]
            if not body.strip() or body.lstrip().startswith((";", "#")):
                continue
            match = ROW.fullmatch(body)
            if match is None:
                raise ValueError("resourceFlatNumericRowRequired")
            key = int(match[1].split("=", 1)[0].strip())
            if key in rows:
                raise ValueError("resourceDuplicateKey")
            rows[key] = ResourceRow(index, match[1], match[2], ending)
        if not rows:
            raise ValueError("resourceRowsMissing")
        return ResourceTable(raw, encoding, bom, tuple(lines), rows)

    def with_values(self, replacements: dict[int, str]) -> bytes:
        lines = list(self.lines)
        for key, value in replacements.items():
            row = self.rows[key]
            if "\r" in value or "\n" in value or "\x00" in value:
                raise ValueError("resourceInjectedRow")
            lines[row.index] = row.prefix + value + row.ending
        result = self.bom + "".join(lines).encode(self.codec, errors="strict")
        if len(result) > MAX_BYTES:
            raise ValueError("resourceProposedSizeInvalid")
        return result


def ordinary_file(path: Path) -> Path:
    path = path.absolute()
    for item in (path, *path.parents):
        metadata = item.lstat()
        if stat.S_ISLNK(metadata.st_mode) or (
            getattr(metadata, "st_file_attributes", 0)
            & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
        ):
            raise ValueError("resourceReparsePathRefused")
    if not path.is_file() or path.stat().st_size > MAX_BYTES:
        raise ValueError("resourceOrdinaryBoundedFileRequired")
    return path


def read_table(path: Path, encoding: str | None = None) -> ResourceTable:
    path = ordinary_file(path)
    with path.open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    return ResourceTable.parse(raw, encoding)
