"""Complete exact shared-ID column vectors; no approximate/semantic mapping."""
from collections import defaultdict
from dataclasses import dataclass
import struct

from catalog_rows import Catalog

MAX_VECTOR_BYTES = 256 * 1024 * 1024


@dataclass(frozen=True)
class Column:
    source_index: int
    target_candidates: tuple[int, ...]


@dataclass(frozen=True)
class Alignment:
    status: str
    reference_rows: int
    current_rows: int
    shared_ids: int
    columns: tuple[Column, ...]
    unique_byte_mapping: tuple[tuple[int, int], ...]

    def aggregate(self):
        return {"status": self.status, "referenceRows": self.reference_rows, "currentRows": self.current_rows,
                "sharedIdentities": self.shared_ids, "referenceOnlyIdentities": self.reference_rows - self.shared_ids,
                "currentOnlyIdentities": self.current_rows - self.shared_ids,
                "sourceColumns": len(self.columns),
                "columnsWithoutExactCandidate": sum(not c.target_candidates for c in self.columns),
                "columnsWithOneExactCandidate": sum(len(c.target_candidates) == 1 for c in self.columns),
                "columnsWithMultipleExactCandidates": sum(len(c.target_candidates) > 1 for c in self.columns),
                "semanticTextRolesProved": False, "clientApplied": False}


def analyze(reference: Catalog, current: Catalog):
    if reference.field_count not in (65, 67) or current.field_count != 67 or any(row.ending != b"\r\n" for row in current.rows):
        raise ValueError("explicitCurrent67EnvelopeRequired")
    shared = sorted(reference.by_id.keys() & current.by_id.keys())
    if not shared:
        return Alignment("NoSharedIdentities", len(reference.rows), len(current.rows), 0, (), ())
    total = sum(4 + len(field) for catalog in (reference, current) for identity in shared
                for field in catalog.by_id[identity].fields[1:])
    if total > MAX_VECTOR_BYTES:
        raise ValueError("vectorBounds")
    vectors = []
    for catalog in (reference, current):
        vectors.append(tuple(b"".join(struct.pack("<I", len(catalog.by_id[identity].fields[column])) +
                                     catalog.by_id[identity].fields[column] for identity in shared)
                             for column in range(1, catalog.field_count)))
    columns = tuple(Column(source + 1, tuple(target + 1 for target, vector in enumerate(vectors[1]) if vector == original))
                    for source, original in enumerate(vectors[0]))
    status, mapping = "UniqueCompleteByteAlignment", ()
    groups = defaultdict(list)
    for column in columns:
        groups[column.target_candidates].append(column.source_index)
    if () in groups:
        status = "MissingExactColumnMatches"
    elif any(len(sources) > len(targets) for targets, sources in groups.items()):
        status = "InsufficientDistinctTargetColumns"
    elif any(len(sources) != 1 or len(targets) != 1 for targets, sources in groups.items()):
        status = "AmbiguousCompleteByteAlignments"
    else:
        mapping = tuple((column.source_index, column.target_candidates[0]) for column in columns)
    return Alignment(status, len(reference.rows), len(current.rows), len(shared), columns, mapping)
