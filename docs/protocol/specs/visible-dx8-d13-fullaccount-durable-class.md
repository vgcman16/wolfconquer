# visible-dx8-d13-fullaccount-durable-class: Recoverable current-owner class reset

## Identity and scope

Revision1, 2026-10-27; Root-authorized correction to the existing FullAccount
NPC465123 class options1..11. Target is the retained VisibleDx8D13 managed packet
contract. No new client evidence, request handler, class formula or native owner
is introduced. This source batch is focused-qualified; its new exact-public full gate remains
pending. Prior full8e success is separate evidence.

Current managed replacement defects are save-before-unconditional-Set, two-file
in-process rollback without a durable joint record, and post-save serialization/
sender prerequisites. These are not automatically Rayzo-original defect claims.
PIN/security/eligibility/resource gates and the eleven existing grants remain.

## Packet contract

Keep the actual typed factories, successful field widths, UTF8 text and ordering.
Prepare every direct frame, including the four pure equipment-refresh frames,
before durable commitment. The full mutating EquipmentQuery pipeline is not used.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| NPC/options |465123 /1..11 | Existing NPC dispatch | Selected class reset | One branch | Existing grant table | AccountSecurity.cs |
| Stats | Existing MsgUpdate | Existing typed scalar widths | Initial Agility/Strength/Vitality/Spirit/Attributes | Five frames | Existing projection | Class.Projection.cs |
| Removed spells | MsgAction109 | Existing typed action | Clear selected old membership | Per original row | Existing clear policy | Class.cs |
| Level/action | MsgUpdate Level, MsgAction92 | Existing codec | Target140 and visible level notice | Only if level changes | No formula changes | Class.cs |
| Level stats/equipment/HP | Existing update/status/perfection codecs | Unchanged | Existing source-level branch | Existing conditional order | Actual current calculators | ItemUsage.Equipment.cs |
| Class/spell/disconnect | Existing Class update, opcode2101, actual close route | Unchanged | Final target class/zero-progress grants | Existing duplicate add-packet order retained | Eleven definitions unchanged | Class.Definitions.cs |

Actual claimant constructor send/disconnect routes are required before writing.
Visible action92 peers require actual send routes. New internal readonly
HasSendRoute/HasDisconnectRoute merely report immutable constructor presence;
they do not invoke/replace a delegate or prove delivery. Postcommit send/close
errors keep committed state and cannot become unchanged-rejection diagnostics.

## Catalog/table contract

The existing class grant table and stat/HP/status calculators remain authoritative
for this slice. Preserve all existing family boundaries, duplicate Warrior1020
add packets, source stat ordering, class140 target, rebirth2 and attribute+900
formulas. Do not fix unrelated promotion/balance rules or supply missing metadata.
Level-changing work retains the existing required real WorldData presence.

Character path is configured UsersRoot + AccountId.ini; spell path is configured
PlayerSpellsRoot + Character.Uid.bin. AccountId and UID may differ. Preserve exact
Character encoding, unknown fields/other sections and all unrelated resources/PIN
state. Require the actual saved Character to match the captured current modeled
baseline before changing the approved fields. Capture exact file bytes for CAS.

Spell rows remain Int32 count plus12-byte records. Validate complete modeled
membership against actual saved rows; preserve opaque byte11 in retained unchanged
rows. Cleared/reset/new rows follow the existing actual grant semantics and zero
progress encoding. Missing saved spell data may be represented by a null before
image and a complete prepared after image; no invented file is required.

## Validation and transaction rules

1. The actual trade party gate precedes the Store connection gate, which precedes
   file locks. Frame dispatch already owns the real world-operation lease: do not
   add a nested lease or unrelated vendor gate. Refuse ended connections, active
   trade/escrow, blocked/pending recovery, stale/value-equal replacement sessions,
   wrong account/UID/native binding and missing required routes before durable work.
2. Capture the real Store holder identity and exact expected record. Optional
   native absence stays absent; present native ownership must be reciprocal/current.
   Capture complete input fields/known collection storage before mutable metadata.
   Prepare private stable class/spell/gear/item inputs, projection and all frames.
   Required PIN admission is read from the actual existing runtime binding; do not
   consume or fabricate authorization. Preserve PIN raw fields in the file image.
3. Under the real expected-session TryCommitSession callback, revalidate actual
   recovery readiness for configured Users/Spells and live journal clearance.
   Recompute required projection/frame results against real metadata before the
   last plain owner/input fence; detect changed catalogs rather than invent defaults.
   Known source backing is checked after all comparer-capable reads. Only actual
   participants are used; no accepting saver/provider or temporary owner swap.
   Level-changing status frames also capture the actual identity-bound
   LegacyNativeMagicQueryFields. After the final input/comparer reads, compare
   that value through the existing real cache Read before journal commitment.
   Level-unchanged work performs no unrelated cache capture/check. This closes
   the late wrapped-dictionary lookup window without inventing cache state.
4. Read both actual source files under the same path coordinator and prepare exact
   after images before journal publication. Preserve Character encoding/layout and
   spell padding. A stale/changed preimage cannot be silently adopted or overwritten.
5. Consume the real distinct v8 Character+Spells journal. Existing v1/v2 target
   Character+Items; v6/v7 have ordinary-admission preservation authority and cannot
   authorize this class reset. Do not fabricate an Items participant or label reset
   as ordinary admission. The shared pending slot serializes cooperating durable work.
6. v8 API, owned by the journal helper:

```text
LegacyTradeJournal.TryCommitCharacterSpells(
  string usersRoot, string spellsRoot,
  IReadOnlyList<LegacyTradeJournalChange> changes, out string reason)
// bounded test/recovery overload adds before out:
LegacyCharacterSpellsJournalInterruption { None, AfterPending, AfterCharacter, AfterSpells }
LegacyTradeRecoveryState.IsReadyForCharacterSpells(usersRoot, spellsRoot)
LegacyTradeRecoveryState.TryRequireLiveJournalClear(out reason)
```

   Changes are exactly Character(AccountId, required before/after) and
   Spells(CharacterUid, optional before/required after). Embedded Character UID
   before/after equals Spells FileId. This world's completed startup recovery and
   actual configured Spells route are required; pair-only readiness is insufficient.
   Normal caller uses the no-interruption overload.
   After matching the actual configured recovery routes, capture canonical absolute
   trimmed Users/Spells roots for all paths/writer calls while retaining literal
   context/session identity checks. Fully qualified trailing-separator forms remain
   valid; relative paths never gain authority through normalization.
7. The journal's actual stage/flush/exact-image/rollback engine determines outcomes.
   Pre-publication refusal changes neither files, current record, authorization,
   resources nor success/close outputs. Exact rollback is a rejected operation only
   when the engine proves it. Pending/mixed/ambiguous or committed-before-memory
   outcome fences the actual RecoveryState and is logged as RecoveryRequired,
   never as "no mutation". Do not replay a live pending journal to revive stale state.
8. Only journal success lets TryCommitSession publish the fully prepared session;
   it intentionally skips duplicate _sessionChanged persistence. No further
   serializer, metadata lookup, allocation or native setter follows disk commitment
   before the holder store. Network publication is outside held gates, bound to the
   same holder lifetime; skip retired peers/owners without rolling back commitment.
9. Restart recovery uses explicit configured Users/Spells and exact recorded
   before/after images, never reexecutes the attribute bonus or grant projection.
   Unrecognized/mismatched domains retain evidence and refuse. This is process-
   restart recovery, not SQL, multi-process, power-loss or full crash atomicity.

## Expected wire behavior and fixtures

- Preserve all existing11-class goldens, duplicate spell-add packets, level/source
  stat branches, visible action92, resource/PIN/sentinel fields and disconnect order.
- Existing tests must observe both real files/current Store at the first actual
  send, not depend on the bypassed ordinary _sessionChanged callback.
- Use distinct AccountId/CharacterUid in positives. Configure a genuine recovery
  owner with actual Users/Spells routes and complete startup recovery; never set
  private ready flags or infer spells from an Items-only owner.
- Stale same UID, value-equal replacement, different account/UID, retired/wrong
  Store and ended/trading connection refuse before file or success/close output.
- Actual read/share/replacement failures preserve proved before images or retain
  journal recovery evidence. Test AfterPending/AfterCharacter/AfterSpells through
  the real bounded journal mechanism, then fresh configured restart recovery.
- Exercise malformed/changed raw Character/spells, valid opaque padding, changed
  metadata and final lookup/source mutation with complete resource/output witnesses.
- Missing actual send or close route refuses before journal creation; genuine
  send/close failure after commitment keeps saved/current state and truthful status.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Class rules/outputs | Packet/table sections | Actual11-class definitions | Existing FullAccount tests | Retained valid contract |
| Current owner before persistence | Transaction1..3 | Real Store/trade/recovery owners | New stale-owner controls | Required correction |
| Two-file recovery | Transaction5..9 | New strict v8 domain | Real interruption/restart tests | Distinct authority; helper-owned |
| Opaque preservation | Table/transaction4 | Actual12-byte spell schema | Padding controls | No invented field meaning |
| Client/runtime completeness | Exclusions | No new evidence | None | Open; no deployment/native adoption claim |

## Implementation and qualification

The integrated implementation is consumed by the existing FullAccount class-reset
path. It passes the shared354-case public/private selection, including49 direct
v8 controls and53 FullAccount controls. All52 old-source baseline names remain;
the corrected-fixture baseline was39passed/13failed. The earlier15 setup failures
are not business-defect evidence. Final public staging cleanup controls pass2/2.

See [actual scope and limits](../../FULLACCOUNT-CLASS-TRANSACTION.md). A new
exact-public full gate is pending. No deployment, live-client, SQL, native graph,
power-loss or multi-process completion follows. The ordinary account=UID loader
invariant is unchanged; distinct-key fixtures exercise the direct file API only.
