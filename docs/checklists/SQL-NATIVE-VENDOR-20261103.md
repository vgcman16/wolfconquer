# Vendor authority qualification queue

Updated2026-11-03. Composition after cleanfb0c5009's actualSQL14 qualification.
The first booth gate on e42e06b1 built successfully, then finished436passed /
3failed /0skipped. Its failed source and results remain retained. No public push.

The three failed positives had invalid fixture premises: two schedules changed
the advertised sash before buying, and the native-join fixture supplied60-field
starter metadata rather than the complete66-field stack contract. Reviewed test
repairs use an unrelated sash for FIFO/send checks and complete starter metadata
at actual catalog construction. Production lineage/cap checks remain unchanged.

## Actual component and provider qualification

Source9060769192709e16ce4fcc9da31764f5b41b63ae passed the expanded affected run
20261103T043253635Z-3f79b29e:534passed/0failed/0skipped. Build159.757s,
test313.269s; finished04:40:48.0653133UTC. Root inspected actual TRX rows,
including all three repaired cases, and found0changed-path warning lines.
The filter covers vendor/booth/trade-runtime/item-lineage, joined-native and
ordinary-login/admission neighbors; it excludes opt-in SQL integration.

The same clean source then passed the separate command:

```text
pwsh -NoProfile -File tools/dev/Invoke-SqlGameplayCheck.ps1 -Port 34384
```

Root inspected all20 distinct actual Passed TRX rows,0failures/skips. The new
six Facts exercise registered native Gold/CP booth transfers before every socket,
exclusive file-lease refusal/retry, same-node authority across unrelated warehouse
publication, replacement-node refusal then registered remove/relist, and cold SQL
reauthentication/file reopening. Both authority Facts each exercise Gold and CP.
The previous14 auth/trade/refusal/EOF/recovery Facts also passed unchanged.

Private receipt owned-sql-auth-48b331ab7f7e4abe977577358bb34191 reports Qualified,
MariaDB12.2.2,20Facts,ChildTreeSettled/CommandsSettledtrue,DataRetainedfalse,
no primary/cleanup exceptions. Root separately confirmed data/ and
client.private.ini absent; the exact command/session terminated0.

- [x] Root reviewed/composed28 explicit source/test/spec paths. The qualified
  trade checkpoint and its failed/targeted/provider evidence remain retained.
- [x] Include selected Suspicious refusal and the four authored SQL/native booth
  start/list/show/buy, lease/refusal/retry and cold-auth/replay controls.
- [x] Keep exact public listing/wire shape; retain actual advertised native node
  lineage and immutable server66 stack metadata/generation through final WAL.
- [x] Preserve valid same-node canonical reprojection; hold changed/replaced
  native roots/children, stale metadata and invalid selected bounds.
- [x] Include real registered detached controls, native operations/metadata and
  two SQL/native authority Facts. Detached tests explicitly have no native actor.
- [x] Preserve public DATA-only compatibility only for genuine unbound components.
  Source review's issued-null/empty-generation ambiguity was corrected with private
  factory provenance, then registered refusal/positive controls were added.
- [x] Compose the overlapping Admission predicate without losing Suspicious or
  new issuer/cap checks. Correct an older test's invalid class with-expression;
  preserve its assertions. Root added explicit actual metadata-type prerequisites.
- [x] Build and run affected vendor/booth/lineage/recovery neighbors. Inspect every
  actual failure and changed-path warning; no broad suppression or fake owners.
  The repaired run also includes native-join and ordinary-login neighbors.
- [x] Run the closed owned SQL command in an idle window on a fresh explicit
  loopback port. The dynamic inventory is20 Facts after this intake: old14 plus
  four booth and two authority Facts. Require all20 distinct Passed rows, no skips,
  actual provider/native/file/output assertions and owned cleanup.
- [ ] Run the appropriate complete Windows gate and review/publish only the
  qualified source. Focused results and oldSQL14 cannot stand in for it.

FSB327-329 have bounded component/provider proof; full/live gates remain open.
No SQL character/item migration,
universal crash safety, live7937 profile, English rendering or host-trust claim.
Characters/items remain journaled INI/BIN. Operator-provided execution holds,
original data, backups, running sessions and unrelated work remain preserved.

[Contract](../protocol/specs/d13-vendor-listing-incarnation.md) ·
[Earlier actualSQL14](OWNED-SQL-AUTH-20261103.md).
