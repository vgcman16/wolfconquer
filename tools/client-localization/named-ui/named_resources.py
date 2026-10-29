"""Byte-preserving UTF8 assignment documents, with duplicate occurrences retained."""
from dataclasses import dataclass
from pathlib import Path
import re
import stat

MAX_BYTES = 16 * 1024 * 1024


@dataclass(frozen=True)
class Row:
    index: int
    value: str
    prefix: bytes
    ending: bytes


@dataclass(frozen=True)
class NamedResources:
    raw: bytes
    bom: bytes
    lines: tuple[bytes, ...]
    groups: dict[str, tuple[Row, ...]]

    @staticmethod
    def parse(raw: bytes):
        if not raw or len(raw) > MAX_BYTES or b"\0" in raw:
            raise ValueError("namedResourceBoundsOrNul")
        bom = b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b""
        payload = raw[len(bom):]
        if payload.decode("utf-8", errors="strict").encode("utf-8") != payload:
            raise ValueError("namedResourceEncoding")
        lines = re.findall(rb"[^\r\n]*(?:\r\n|\r|\n|$)", payload)
        if lines and not lines[-1]:
            lines.pop()
        groups = {}
        for index, line in enumerate(lines):
            body = line.rstrip(b"\r\n")
            if not body.strip() or body.lstrip().startswith((b";", b"#", b"//")):
                continue
            key, delimiter, value = body.partition(b"=")
            if not delimiter or not key or any(byte < 32 for byte in key):
                raise ValueError("namedResourceAssignmentRequired")
            name = key.decode("utf-8", errors="strict")
            groups.setdefault(name, []).append(Row(index, value.decode("utf-8"), key + b"=", line[len(body):]))
        if not groups:
            raise ValueError("namedResourceRowsMissing")
        return NamedResources(raw, bom, tuple(lines), {key: tuple(rows) for key, rows in groups.items()})

    def replace_values(self, values: dict[str, str]):
        lines = list(self.lines)
        for key, value in values.items():
            if any(char in value for char in "\r\n\0"):
                raise ValueError("namedResourceRowInjection")
            encoded = value.encode("utf-8", errors="strict")
            for row in self.groups[key]:
                lines[row.index] = row.prefix + encoded + row.ending
        result = self.bom + b"".join(lines)
        if len(result) > MAX_BYTES:
            raise ValueError("namedResourceProposedBounds")
        return result


def ordinary(path: Path, directory=False):
    path = path.absolute()
    for node in (path, *path.parents):
        metadata = node.lstat()
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError("namedResourceReparsePath")
    if (path.is_dir() if directory else path.is_file()) is not True:
        raise ValueError("namedResourceOrdinaryPath")
    return path


def read_bytes(path: Path):
    path = ordinary(path)
    with path.open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError("namedResourceFileBounds")
    return raw
