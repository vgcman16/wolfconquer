"""Strict, selected normal-import metadata; no native loading or disassembly.

Header/range/terminated-descriptor rules follow the repository's
Program.ClientPeDependencyAudit.cs and Program.ClientPeSymbolAudit.cs readers.
"""
import struct
import time

MAX_DESCRIPTORS = 256
MAX_THUNKS = 4096
MAX_TOTAL_THUNKS = 65536
MAX_NAME_BYTES = 256


class Refused(ValueError):
    """A bounded static query could not establish an unambiguous result."""


class Budget:
    def __init__(self):
        self.deadline = time.monotonic() + 30

    def check(self):
        if time.monotonic() >= self.deadline:
            raise Refused("deadline-exceeded")


class ImportSlots:
    def __init__(self, data, budget):
        self.data, self.budget = data, budget
        self.sections, self.cache, self.probes = [], {}, 0
        self._headers()
        self.descriptors = self._descriptors()

    def _number(self, offset, width):
        if offset < 0 or offset + width > len(self.data):
            raise Refused("file-range-outside-input")
        return int.from_bytes(self.data[offset:offset + width], "little")

    def _headers(self):
        if len(self.data) < 64 or self.data[:2] != b"MZ":
            raise Refused("missing-dos-header")
        pe = self._number(0x3C, 4)
        if pe < 64 or self.data[pe:pe + 4] != b"PE\0\0":
            raise Refused("missing-pe-header")
        machine, count = self._number(pe + 4, 2), self._number(pe + 6, 2)
        optional, optional_size = pe + 24, self._number(pe + 20, 2)
        magic = self._number(optional, 2)
        if (machine, magic) not in ((0x14C, 0x10B), (0x8664, 0x20B)):
            raise Refused("unsupported-pe-machine-or-kind")
        self.width = 4 if magic == 0x10B else 8
        prefix = 96 if self.width == 4 else 112
        if optional_size < prefix + 16 or optional + optional_size > len(self.data):
            raise Refused("optional-header-too-small")
        self.base = self._number(optional + (28 if self.width == 4 else 24), self.width)
        self.image_size = self._number(optional + 56, 4)
        self.header_size = self._number(optional + 60, 4)
        directories = self._number(optional + prefix - 4, 4)
        if directories < 2 or optional_size < prefix + directories * 8:
            raise Refused("data-directory-outside-optional-header")
        self.import_rva = self._number(optional + prefix + 8, 4)
        self.import_size = self._number(optional + prefix + 12, 4)
        table = optional + optional_size
        if not 1 <= count <= 96 or table + count * 40 > self.header_size or not self.header_size <= len(self.data):
            raise Refused("invalid-section-or-header-bounds")
        if not self.header_size <= self.image_size or self.base + self.image_size > 1 << (8 * self.width):
            raise Refused("image-address-overflow")
        for index in range(count):
            self.budget.check()
            row = table + 40 * index
            virtual, rva, size, raw = (self._number(row + offset, 4) for offset in (8, 12, 16, 20))
            span = max(virtual, size)
            if raw + size > len(self.data) or rva + span > self.image_size:
                raise Refused("section-outside-file-or-image")
            if size and raw < self.header_size or span and rva < self.header_size:
                raise Refused("section-overlaps-headers")
            for old_rva, old_size, old_raw, old_span in self.sections:
                if size and old_size and max(raw, old_raw) < min(raw + size, old_raw + old_size):
                    raise Refused("overlapping-raw-sections")
                if span and old_span and max(rva, old_rva) < min(rva + span, old_rva + old_span):
                    raise Refused("overlapping-virtual-sections")
            self.sections.append((rva, size, raw, span))

    def mapped(self, rva, length):
        self.budget.check()
        if rva < 0 or length < 1 or rva + length > self.image_size:
            raise Refused("rva-outside-image")
        matches = [rva] if rva + length <= self.header_size else []
        matches += [raw + rva - start for start, size, raw, _ in self.sections
                    if start <= rva and rva + length <= start + size]
        if len(matches) != 1:
            raise Refused("rva-not-uniquely-raw-backed")
        return matches[0]

    def _descriptors(self):
        if not self.import_rva or self.import_size < 20:
            raise Refused("normal-import-directory-absent-or-incomplete")
        first = self.mapped(self.import_rva, self.import_size)
        rows, starts = [], set()
        for index in range(min(self.import_size // 20, MAX_DESCRIPTORS + 1)):
            self.budget.check()
            values = struct.unpack_from("<IIIII", self.data, first + index * 20)
            if not any(values):
                return rows
            if len(rows) == MAX_DESCRIPTORS:
                raise Refused("descriptor-cap-exceeded")
            lookup, timestamp, _, name, iat = values
            if not name or not iat or iat % self.width or lookup % self.width or iat in starts:
                raise Refused("invalid-or-duplicate-import-table")
            starts.add(iat)
            rows.append((lookup, timestamp, name, iat))
        raise Refused("descriptor-terminator-missing")

    def _thunks(self, number):
        if number in self.cache:
            return self.cache[number]
        lookup, timestamp, _, iat = self.descriptors[number]
        if not lookup and timestamp:
            raise Refused("bound-import-without-original-lookup")
        lookup = lookup or iat
        values = []
        for index in range(MAX_THUNKS + 1):
            self.probes += 1
            if self.probes > MAX_TOTAL_THUNKS:
                raise Refused("total-thunk-cap-exceeded")
            a = self._number(self.mapped(iat + index * self.width, self.width), self.width)
            b = self._number(self.mapped(lookup + index * self.width, self.width), self.width)
            if not a or not b:
                if a or b:
                    raise Refused("iat-lookup-termination-mismatch")
                self.cache[number] = values
                return values
            if index == MAX_THUNKS:
                raise Refused("thunk-cap-exceeded")
            values.append(b)
        raise Refused("thunk-terminator-missing")

    def _name(self, rva, module=False):
        first = self.mapped(rva, 1)
        for length in range(MAX_NAME_BYTES):
            if self.mapped(rva, length + 1) != first:
                raise Refused("name-mapping-is-not-contiguous")
            value = self.data[first + length]
            if value == 0:
                text = self.data[first:first + length].decode("ascii")
                if not text or text[0] == " " or text[-1] in " .":
                    raise Refused("empty-or-unsafe-import-name")
                if module and (text in (".", "..") or any(c in '/\\:<>\"|?*' for c in text)):
                    raise Refused("unsafe-import-module-name")
                return text
            if not 32 <= value <= 126:
                raise Refused("non-ascii-import-name")
        raise Refused("name-cap-or-terminator-exceeded")

    def selected(self, slots):
        if not 1 <= len(slots) <= 8 or len(set(slots)) != len(slots):
            raise Refused("selected-slot-count-or-duplicates")
        output = []
        for va in slots:
            rva, owners = va - self.base, []
            self.mapped(rva, self.width)
            for number, (_, _, _, iat) in enumerate(self.descriptors):
                if rva < iat or (rva - iat) % self.width:
                    continue
                values = self._thunks(number)
                if (rva - iat) // self.width < len(values):
                    owners.append((number, (rva - iat) // self.width, values))
            if len(owners) != 1:
                raise Refused("slot-has-no-unique-normal-import-owner")
            number, index, values = owners[0]
            lookup, _, module, iat = self.descriptors[number]
            value = values[index]
            row = {"iatVa": f"{va:0{self.width * 2}x}", "descriptor": number,
                   "firstThunkVa": f"{self.base + iat:0{self.width * 2}x}",
                   "lookupKind": "original" if lookup else "unbound-iat", "index": index,
                   "module": self._name(module, module=True)}
            ordinal_bit = 1 << (self.width * 8 - 1)
            if value & ordinal_bit:
                if value & ~(ordinal_bit | 0xFFFF):
                    raise Refused("ordinal-reserved-bits-set")
                row["ordinal"] = value & 0xFFFF
            else:
                if value >= 1 << 32:
                    raise Refused("import-name-rva-overflow")
                row["hint"] = self._number(self.mapped(value, 2), 2)
                row["symbol"] = self._name(value + 2)
            output.append(row)
        return output
