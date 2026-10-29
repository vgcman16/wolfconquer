"""Bounded opaque decoded 65/67-field inputs; never infers text roles."""
from dataclasses import dataclass
import hashlib
from pathlib import Path
import re
import stat
from types import MappingProxyType

MAX_BYTES = 64 * 1024 * 1024
MAX_ROWS = 100_000


@dataclass(frozen=True)
class Row:
    item_id: int
    fields: tuple[bytes, ...]
    ending: bytes


@dataclass(frozen=True)
class Catalog:
    raw: bytes
    field_count: int
    rows: tuple[Row, ...]
    by_id: object


def parse(raw, field_count, *, current7937=False):
    if type(field_count) is not int or field_count not in (65, 67) or current7937 and field_count != 67:
        raise ValueError("unsupportedFieldCount")
    if not isinstance(raw, (bytes, bytearray, memoryview)) or not 0 < len(raw) <= MAX_BYTES:
        raise ValueError("inputBounds")
    captured = bytes(raw)
    if b"\0" in captured:
        raise ValueError("nul")
    rows, by_id = [], {}
    for line in re.findall(rb"[^\r\n]*(?:\r\n|\r|\n|$)", captured):
        if not line:
            continue
        body = line.rstrip(b"\r\n")
        ending = line[len(body):]
        if not body or current7937 and ending != b"\r\n":
            raise ValueError("emptyRowOrCurrentEnding")
        fields = body.split(b"@@")
        if len(fields) != field_count + 1 or fields[-1] != b"":
            raise ValueError("exactWidthAndTrailingDelimiterRequired")
        identity = fields[0]
        if not identity:
            raise ValueError("identityRequired")
        item_id = 0
        for byte in identity:
            if not 48 <= byte <= 57 or item_id > (0xffffffff - (byte - 48)) // 10:
                raise ValueError("uint32IdentityRequired")
            item_id = item_id * 10 + byte - 48
        if item_id in by_id:
            raise ValueError("duplicateCanonicalIdentity")
        if len(rows) == MAX_ROWS:
            raise ValueError("rowBounds")
        row = Row(item_id, tuple(fields[:-1]), ending)
        rows.append(row)
        by_id[item_id] = row
    if not rows:
        raise ValueError("rowsRequired")
    rebuilt = b"".join(b"@@".join(row.fields) + b"@@" + row.ending for row in rows)
    if rebuilt != captured:
        raise ValueError("exactReconstructionRequired")
    return Catalog(captured, field_count, tuple(rows), MappingProxyType(by_id))


def ordinary(path):
    path = Path(path)
    if not path.is_absolute() or str(path).startswith(("\\\\", "//")):
        raise ValueError("absoluteLocalPathRequired")
    for node in (path, *path.parents):
        info = node.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise ValueError("linkedPathRefused")
    if not path.is_file():
        raise ValueError("ordinaryFileRequired")
    return path


def read_pinned(path, expected_sha256):
    if type(expected_sha256) is not str or not re.fullmatch(r"[A-Fa-f0-9]{64}", expected_sha256):
        raise ValueError("sha256PinRequired")
    path = ordinary(path)
    before = path.stat()
    if not 0 < before.st_size <= MAX_BYTES:
        raise ValueError("inputBounds")
    with path.open("rb") as stream:
        captured = stream.read(MAX_BYTES + 1)
    after = ordinary(path).stat()
    if (before.st_size, before.st_mtime_ns, before.st_ino, before.st_dev) != (
            after.st_size, after.st_mtime_ns, after.st_ino, after.st_dev) or len(captured) != before.st_size:
        raise ValueError("sourceChanged")
    if hashlib.sha256(captured).hexdigest().upper() != expected_sha256.upper():
        raise ValueError("inputPinMismatch")
    return captured
