# Prestige saved queries

Focused public-derived qualification passed437/437, zero failures/skips (52 new handler cases,233 projection cases,152 retained controls). New exact-public full CI remains pending. The
accepted full run for source `46a501fc` predates this change and does not validate
the prestige candidate.

`PrestigeRankingSnapshot.Create(IReadOnlyList<PrestigeRankEntry>)` validates the
complete input before exposing immutable `PrestigeRankingEntry` values. `TryCreate` returns no snapshot
on unavailable or malformed input. Queries expose `Entries`, `WorldRanks`,
`BestOfTheWorld`, `GetRanks`, `GetMyRank`, and retained-bucket `GetInfo`.

The 66-field slash schema maps fields 0–6 to UID, name, class category, class,
byte level, mesh, and total points; 7–19 to saved appearance; 20 to guild; 21 to
component count; 22–64 to 43 unsigned components; and 65 to the trailing empty
field. The projection checks every
numeric width, field count, source mirror, positive unique UID, and category
1–11. World category 0 is reconstructed. Names must be nonblank; name/guild text
rejects delimiters, controls, and unpaired UTF-16 surrogates while preserving valid
non-ASCII text. Catalog UIDs remain limited to positive Int32 values despite the
UInt32 wire field. Stored total points are not recomputed from components alone.

Ranking requires at least 20,000 total points, retains world 100/class 30, and
sorts descending by total. Stable `OrderByDescending` gives this managed
projection a deterministic saved-order tie policy; it does not establish every
legacy Dictionary enumeration behavior.

The supported selection is outer Show `Type=0`, first item `Type=1`, using the
existing opcode 2054 Protobuf layout. A champion response preserves saved identity
and appearance with rank 1; Profession and WeaponWar remain zero. Evidence is the
existing managed codec and plain `PrestigeRanking.Load/Save` and
`MsgRankMemberShow.Process` source schema. The selected profile is
`VisibleDx8D13`; this is not new live D13 packet or rendering proof.

Publication requires the actual connection's same current session in
`WorldSessionStore`, an explicit nonnegative manifest `prestigeRanks` count equal
to the loaded list count, and complete row validation. Matching counts cannot
legitimize a corrupt tail. Missing, incomplete, or invalid data publishes no frame.
Complete valid empty or wholly ineligible data retains `EmptyResponse(0)`;
declared zero requires an actually empty list. The whole response is serialized
before sending; serialization rejection sends no partial frame. Transport failure
after sending begins remains a transport outcome.

This immutable, statically loaded query data does not authorize live progression,
rewards, inventory, or persistence. GenericRanking pages/continuations, Viewer’s
equipment sequence, other ranking families, and dynamic refresh remain open.
