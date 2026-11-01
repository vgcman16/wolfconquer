# Level-up equipment query: open owner join

Target: VisibleDx8D13 managed server. The retained community reference
`DecompiledSource/GameServer/MrRayzo.Client/GameClient.cs`, `UpdateLevel`, first
publishes level and source stats or points, then calls
`Equipment.QueryEquipment(Equipment.Alternante, CallItems: false)` and restores
hitpoints to the resulting status maximum. This is source evidence for the
sequence, not independent client confirmation or authority for unsafe behavior.

The current source-only combat-XP draft projects and commits Character vitals,
but it does not run that full equipment query or prove that its completed
HP, mana, magic-attack and related caches correspond to the new source stats.
The reborn 139-to-140 control adds points without changing those stats and
cannot prove this join. `LegacyOwnedCombatExperienceAutoStatsTests` exercises
an actual admitted unarmed monster hit with two real equipped items, a parsed
Taoist source-stat row and a subsequent real status query. Its expected
post-level result includes both item contributions. The test is unrun and
states the intended repaired behavior, not a claim that the current draft passes.

The next implementation must preflight an actual query from the privately issued
combat-XP purpose and exact recipient Frame, holder, native player, selected
equipment, catalog and derived-spell/file owners. Query work may require a
Character/Spells lock set and outputs. The current 2203 prepared rebuild is a
reference for actual query ownership, preflight, completed-epoch publication and
postcommit fencing; its attribute-allocation purpose and exclusive operation
lease cannot be borrowed or forged for XP. In particular, do not upgrade a
shared Frame lease or run a nested Frame/barrier while an XP or team gate is
held. Determine a genuine issuance and lease boundary before activation, retain
the team membership scope only for the synchronous recipient commit, and release
all such gates before awaited packets.

Commit/publication and query failure must not advertise a successful complete
level-up or permit stale caches as current. A clean precommit refusal leaves
the recipient XP/Character/native state unchanged; uncertain/postcommit failure
must fence its actual owner and require reconciliation. Previously committed
damage, rewards and other recipients are separate outcomes. Preserve unrelated
opaque state, saved bytes, real packet order and the actual full query's resource
semantics. Before qualification, add real interleaving, changed metadata,
file/derived-spell and output failure controls and verify the real Frame lease
and recovery behavior; no test-only callback or copied metadata grants authority.
