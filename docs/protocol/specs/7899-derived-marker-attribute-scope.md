# Derived spell marker attribute scope

Target: managed ordinary character publications for VisibleDx8D13/7899.
Evidence: exact94006 full-run failures in BankCps, warehouse, magic and completion
controls; LegacyDerivedSpellOrigins normalizes every map key before reading one
marker. This is a server DATA correction, not a new packet or producer authority.

- Capture only supported plain BCL attribute maps with their actual comparer and
  raw row witness. Do not invoke an arbitrary custom comparer or callback map.
- Preserve every unrelated case-distinct attribute and PaidNobility row. Reading
  a derived marker must not normalize, collapse or reject unrelated field names.
- Match ManagedDerivedSpellOrigins case-insensitively. Multiple matching names,
  including two empty markers, remain ambiguous and refuse the publication.
- Keep canonical marker syntax, exact owned spell membership, Rune overlap
  refusal and the actual native/admission/transaction owners unchanged.
- Copied reconciliation DATA must not alias later source map mutations. It must
  not acquire any grant/publication capability or rewrite successful wire bytes.

Validation: wrapped/unwrapped ordinal maps with unrelated case-distinct fields,
ambiguous marker controls, existing reconciliation negatives, and the actual
previously failed Store/handler controls. New tests and repair are unqualified
until Root records their actual results; full release and live acceptance remain
separate gates.
