# Visible DX8 D13: held bound NPC stat writers

## Identity and scope

Draft 2026-11-01. Target Chinese 7899 / VisibleDx8D13. This is a temporary
fail-closed boundary for existing NPC source models, not NPC parity or a new
client protocol. Previously unbound component paths retain their existing
behavior. The public privileged AdminUtility NPC route is already disabled.

## Packet contract

Existing NPC packet contracts are unchanged. The held bound mutation does not
publish item removal, stat/point updates, success dialogs or equipment refresh.
No unconfirmed client failure response is introduced.

## Catalog/table contract

The existing Reallot/LevelAbdo models own their prior lookup rules. This hold
does not invent catalog rows or bypass validation.

## Ownership and transaction

Reallot reset and allocation refuse after their existing pure calculations but
before item/Character persistence when the session or connection has an
admitted native stat attachment. AdminUtility and LevelAbdo likewise hold a
bound level grant if Level, Experience, or the calculated stat tuple changes.
Existing non-stat and
unbound paths are unchanged. The native AddExtraAtributes whole executor requires
an unbound owner before touching either extra or unallocated points.

These are holds, not complete implementations. Reallot reset still needs a real
joint item/Character transaction and stat owner; allocation and level NPCs need
their own bound durable publication. Combat XP/level progression is separate
and is not authorized by these holds.

## Evidence and validation limits

`WorldPacketHandlers.NpcEntrypoints.Utilities.cs`,
`WorldPacketHandlers.NpcEntrypoints.AdminUtilityNpcs.LevelOrProficiency.Mutations.cs`,
`WorldPacketHandlers.NpcEntrypoints.SocialCraftingServices.LevelAbdo.cs`, and
`LegacyNativePlayerRuntime.ExtraAttributes.cs` contain the prior persistence or
native mutation ordering. The new actual-admission component tests inspect
unchanged holder, native values, item/files and output. All source and tests are
unrun; this is not live-client, crash-recovery, SQL or full-feature evidence.
