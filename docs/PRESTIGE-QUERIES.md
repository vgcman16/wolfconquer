# Prestige saved queries

Original focused qualification passed437/437 (52 handler,233 projection,152
retained controls), zero failures/skips. Exact source `adcae028` passed Windows
40456938513: core27,168 passed/164 existing exclusions, updater434, launcher57
and both synthetic parser stages. The later numeric-grammar correction is covered
by 1,295 public-derived passes/129 existing exclusions and 698 private passes;
its new exact-public full check remains pending.

`PrestigeRankingSnapshot.Create(IReadOnlyList<PrestigeRankEntry>)` validates the
complete input before exposing immutable `PrestigeRankingEntry` values. `TryCreate` returns no snapshot
on unavailable or malformed input. Queries expose `Entries`, `WorldRanks`,
`BestOfTheWorld`, `GetRanks`, `GetMyRank`, and retained-bucket `GetInfo`.

The 66-field slash schema maps fields 0–6 to UID, name, class category, class,
byte level, mesh, and total points; 7–19 to saved appearance; 20 to guild; 21 to
component count; 22–64 to 43 unsigned components; and 65 to the trailing empty
field. The projection checks every
ASCII decimal grammar, numeric width, field count, source mirror, positive unique UID, and category
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
