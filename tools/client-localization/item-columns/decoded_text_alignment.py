"""Complete strict decoded-text vectors; opaque evidence, never a chosen mapping."""
from collections import defaultdict
from dataclasses import dataclass
import struct

from catalog_rows import Catalog, MAX_BYTES, parse

MAX_VECTOR_BYTES = 256 * 1024 * 1024
CODECS = frozenset(("utf-8", "cp936"))
UNSUPPORTED = frozenset(("Undecodable", "RoundtripMismatch"))


@dataclass(frozen=True)
class Column:
    source_index: int
    status: str
    target_candidates: tuple[int, ...]


@dataclass(frozen=True)
class CurrentColumn:
    target_index: int
    status: str


@dataclass(frozen=True)
class TextAlignment:
    status: str
    reference_rows: int
    current_rows: int
    shared_ids: int
    reference_codec: str
    current_codec: str
    canonical_vector_bytes: int
    columns: tuple[Column, ...]
    current_columns: tuple[CurrentColumn, ...]

    def aggregate(self):
        return {"status": self.status, "referenceRows": self.reference_rows, "currentRows": self.current_rows,
                "sharedIdentities": self.shared_ids, "referenceOnlyIdentities": self.reference_rows - self.shared_ids,
                "currentOnlyIdentities": self.current_rows - self.shared_ids, "sourceColumns": len(self.columns),
                "currentColumns": len(self.current_columns),
                "unsupportedReferenceColumns": sum(c.status in UNSUPPORTED for c in self.columns),
                "unsupportedCurrentColumns": sum(c.status in UNSUPPORTED for c in self.current_columns),
                "columnsWithoutExactCandidate": sum(not c.target_candidates for c in self.columns),
                "columnsWithOneExactCandidate": sum(len(c.target_candidates) == 1 for c in self.columns),
                "columnsWithMultipleExactCandidates": sum(len(c.target_candidates) > 1 for c in self.columns),
                "canonicalVectorBytes": self.canonical_vector_bytes,
                "referenceCodec": self.reference_codec, "currentCodec": self.current_codec,
                "semanticTextRolesProved": False, "clientApplied": False, "chosenMappingProduced": False}

    def column_evidence(self):
        return {"columns": [{"referenceColumn": c.source_index, "status": c.status,
                             "candidateColumns": list(c.target_candidates)} for c in self.columns],
                "unsupportedCurrentColumns": [{"currentColumn": c.target_index, "status": c.status}
                                              for c in self.current_columns if c.status in UNSUPPORTED]}


class _RoundtripMismatch(ValueError):
    pass


def _decode(raw, codec):
    return raw.decode(codec, errors="strict")


def _canonical_cell(raw, codec):
    text = _decode(raw, codec)
    if text.encode(codec, errors="strict") != raw:
        raise _RoundtripMismatch("codecRoundtripMismatch")
    return text.encode("utf-8", errors="strict")


def _reparse(catalog, *, current=False):
    if not isinstance(catalog, Catalog):
        raise ValueError("catalogRequired")
    # A forged multidimensional buffer must not evade the existing byte cap
    # via its element count. Ordinary parse results already own plain bytes.
    if isinstance(catalog.raw, memoryview) and catalog.raw.nbytes > MAX_BYTES:
        raise ValueError("inputBounds")
    return parse(catalog.raw, catalog.field_count, current7937=current)


def _columns(reference_columns, width):
    if (type(reference_columns) not in (tuple, list) or not reference_columns
            or len(reference_columns) >= width
            or any(type(index) is not int or not 1 <= index < width for index in reference_columns)
            or len(set(reference_columns)) != len(reference_columns)):
        raise ValueError("explicitDistinctReferenceColumnsRequired")
    return tuple(reference_columns)


def _measure(catalog, shared_ids, index, codec):
    size = 0
    try:
        for identity in shared_ids:
            size += 4 + len(_canonical_cell(catalog.by_id[identity].fields[index], codec))
    except _RoundtripMismatch:
        return "RoundtripMismatch", 0
    except UnicodeError:
        return "Undecodable", 0
    return "Supported", size


def _vector(catalog, shared_ids, index, codec, size):
    vector = bytearray(size)
    offset = 0
    for identity in shared_ids:
        cell = _canonical_cell(catalog.by_id[identity].fields[index], codec)
        end = offset + 4 + len(cell)
        if end > size:
            raise ValueError("canonicalColumnChanged")
        struct.pack_into("<I", vector, offset, len(cell))
        vector[offset + 4:end] = cell
        offset = end
    if offset != size:
        raise ValueError("canonicalColumnChanged")
    return bytes(vector)


def _status(columns):
    if any(c.status in UNSUPPORTED for c in columns):
        return "UnsupportedReferenceColumns"
    groups = defaultdict(list)
    for column in columns:
        groups[column.target_candidates].append(column.source_index)
    if () in groups:
        return "MissingExactDecodedTextMatches"
    if any(len(sources) > len(targets) for targets, sources in groups.items()):
        return "InsufficientDistinctTargetColumns"
    if any(len(targets) > 1 for targets in groups):
        return "AmbiguousCompleteDecodedTextAlignments"
    return "UniqueCompleteDecodedTextCandidates"


def analyze(reference: Catalog, current: Catalog, *, reference_codec, current_codec,
            reference_columns, current_version, max_vector_bytes=MAX_VECTOR_BYTES):
    if type(current_version) is not int or current_version != 7937:
        raise ValueError("explicitCurrent7937Required")
    if (type(reference_codec) is not str or reference_codec not in CODECS
            or type(current_codec) is not str or current_codec not in CODECS):
        raise ValueError("explicitSupportedCodecsRequired")
    if type(max_vector_bytes) is not int or not 0 < max_vector_bytes <= MAX_VECTOR_BYTES:
        raise ValueError("vectorBudgetRequired")
    reference, current = _reparse(reference), _reparse(current, current=True)
    requested = _columns(reference_columns, reference.field_count)
    shared = tuple(sorted(reference.by_id.keys() & current.by_id.keys()))
    if not shared:
        return TextAlignment("NoSharedIdentities", len(reference.rows), len(current.rows), 0,
                             reference_codec, current_codec, 0,
                             tuple(Column(index, "NoSharedIdentities", ()) for index in requested),
                             tuple(CurrentColumn(index, "NoSharedIdentities") for index in range(1, 67)))

    measured = []
    for catalog, codec, indices in ((reference, reference_codec, requested), (current, current_codec, range(1, 67))):
        measured.append(tuple((index, *_measure(catalog, shared, index, codec)) for index in indices))
    total = sum(size for group in measured for _, _, size in group)
    if total > max_vector_bytes:
        raise ValueError("vectorBounds")
    # No canonical vector is allocated until every supported complete size fits.
    vectors = []
    for catalog, codec, group in zip((reference, current), (reference_codec, current_codec), measured):
        vectors.append({index: _vector(catalog, shared, index, codec, size)
                        for index, status, size in group if status == "Supported"})
    columns = tuple(Column(index, status, tuple(target for target, vector in vectors[1].items()
                                              if status == "Supported" and vector == vectors[0][index]))
                    for index, status, _ in measured[0])
    return TextAlignment(_status(columns), len(reference.rows), len(current.rows), len(shared),
                         reference_codec, current_codec, total, columns,
                         tuple(CurrentColumn(index, status) for index, status, _ in measured[1]))
