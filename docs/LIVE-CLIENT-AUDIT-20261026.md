# D13 live client audit - October 26, 2026

Root inspected the actual staged client through Computer Use after the user
manually logged in. This is a main-surface audit, not an exhaustive check of every
button or acceptance of full gameplay. The target cached DX8 executable has SHA256
`D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5`.
World159620 and client157916 used the disposable Body1008 fixture. The user and
root both confirmed the full character model visible in Twin City/map1002.

## Actual observations

| Surface | Observed result | Meaning and remaining work |
| --- | --- | --- |
| World, character movement | Full body and NPC models render; actual movement is logged | One local manual-login/render/movement trial works. This is not multiplayer or restart acceptance. |
| Character general/details | Name and level120 display; attack100-101, defense0, magic attack1, magic defense0; Class shows `?`; basic labels overlap values; side panel numbers have missing labels | Staged basic labels still use long strings although the source patch already registers Str:/Agi:/Vit:/Spi:. Class10 is absent from the staged profession catalogs; changing to1000 affects real gameplay and is not a text-only fix. Extended missing-label cause remains open. |
| Equipment and alternate equipment | Full body preview and empty slots display | Matches the unequipped fixture. Equipping, derived combat and persistence were not tested. |
| Inventory | One potion, silver100, CP50, boundCP0 display | Matches actual saved state. No item use/drop/trade was performed in this audit. |
| Skills/spells/proficiencies | Empty lists open from character tabs | The actual saved spell/proficiency files each contain count0. No missing skill grant is established by an empty list. |
| Quest journal | Catalog entries and text render; accepted5/40 appears; task35007 has empty reward/objective output | Actual owned quest file has count0. The client count/catalog is not proof of five accepted server-owned quests. |
| Random daily tasks | Actual opcode2419 payload `080610001800` has no receiver | Exact D13 RTTI identifies `CMsgRandomDailyTask`; semantics and genuine quest ownership must be decoded before integration. |
| QuestList/QuestData | Mode7 is ignored; task35007 parses but finds no runtime owner and sends0 responses | Client-confirmed mode7 is outside the current source enum1-4. Catalog presence must not fabricate accepted quests or rewards. |
| Friends | Offline Diesel[PM] row has level0 and an error portrait | Current and native offline association packets deliberately send mesh/level0. Target-client presentation needs investigation; this does not prove a missing friend handler. |
| Clan and team entry | Clan view correctly says no membership and displays creation/join controls; team view opens empty slots with create/join controls | No clan/team was created or joined. Membership, invitations, permissions and multiplayer behavior remain untested. |
| Subclasses and detail view | Six catalog choices/page1of2; owned ranks/study points0; labels/descriptions are garbled | Actual Open response has entries0/studyPoints0. Untranslated GB18030 SubProfessionInfo.ini is a separate concrete client-text seam. No enrollment/promotion/spending was tested. |
| Crafting/enhancement | Full menu and tabs render; eligible-equipment area is empty | Expected for this inventory. No enhancement, socketing, composition or economy transaction was exercised. |
| Arena/arena rankings | Rank14,4000points and saved-player rows render; self row appears | Arena statistics-backed query responses work. Matchmaking, matches, rewards and season lifecycle remain untested. |
| Other rankings | RankMemberShow type0 requested3 returns0; GenericRanking QueryCount/None returns0 | RankMemberShow is an unconditional empty placeholder. General/Prestige ranking branches are also empty in source, distinct from working arena rows. |
| Achievements | Actual2307/action2 request is logged without adoption | Genuine achievement hydration, ownership, updates and persistence remain unintegrated. Native method presence is not a complete host join. |
| Daily activity schedule | Six Chinese event entries/page1of3 display | Client presentation only; events were not entered and their lifecycles are unproved. |
| Branding/localization | Rayzo remains in the top-left server caption; many Chinese labels, clipped English and mojibake remain | English/branding work is incomplete. ProfessionalName tables lack legacy class10; its correct profile mapping needs proof. |
| Welfare/auction/scroll/shirt/bottom skill entries | Some clicks produced no visible panel | A separate Welfare Center click with a fresh log boundary emitted no non-ping world request. This does not establish its failure cause. Do not assign opaque2424 or action482 to controls by proximity. |
| Trade/social/PK/chat entry menus | Menus/selection prompt open; prompt canceled and settings unchanged | No target selected, transaction completed, membership changed, or setting changed. This is entry UI evidence only. |

## Hang and audit boundary

During the general-ranking icon click, Computer Use timed out and the client
became unresponsive. Fresh process checks confirm the same retained client identity
is still running and consuming CPU; the server remains running. A single bounded
Frida thread-metadata capture collected3 samples and detached without errors.
Accurate unwind candidates include C3/graphics/runtime work but do not establish
a fault, infinite-loop cause, malformed response or specific bad asset. No further
UI input was sent to that hung process after the failed recovery observation.
Root subsequently retired only that exact retained test-client identity, preserved
the same WorldServer and reopened client160728. The user manually logged in again.
The server restored415/339 through SetLocation and CompleteLogin;15 subsequent
actual client movement packets walk to410/354, matching the later save. A spawn
reset is not evidenced by that walk. Clan/team entry views then opened normally.
Later menu navigation timed out again and the user independently confirmed that
the second client is frozen. Further input stopped; the current process is retained.
This pass is not described as an all-buttons audit or a reliable-client claim.

The prior client155180 exited unexpectedly according to the user. Its cause is
unproved. A separate native-probe cleanup PID-identity defect caused root to cancel
Windows40420961276; there is no established causal link to either client incident.

## Ordered work

1. Correct and verify native-probe cleanup with retained process identities before
   another full CI run. Preserve the real orphan-child cleanup positive control.
2. Recover the client and correlate one action at a time with fresh log boundaries;
   investigate the ranking hang without changing successful packet sequences.
3. Decode RandomDailyTask2419 and QuestList mode7 from the exact target; join real
   saved quest/achievement ownership instead of inventing UI success state.
4. Replace ranking placeholders with actual validated providers and wire genuine
   native achievement lifecycle/persistence.
5. Correct client text encodings, label layouts and confirmed class-display mapping
   in staged copies, then recheck the actual rendered panels.
6. Test populated owned skill/equipment/quest data and two-player trade/social/event
   scenarios. Empty synthetic records and menu rendering are not those proofs.

Detailed source-line triage is retained locally in
`.artifacts/helper-live-ui-audit-next/README.md`. Logs, sampler output and lifecycle
metadata remain local. Original saves/assets/routes and the restore ZIP are retained.
