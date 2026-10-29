# D13 Caprice paired stamina publication — source successor pending qualification

## Identity and scope

- Revision3, 2026-10-29; original spec plus Root-approved typed pair design,
  source-only implementation/tests, not executed or client-qualified.
- VisibleDx8D13, existing Caprice rune4028000–4028027 and player receive branch1.
- Baseline c6c plus the strict numeric receive-stamina package. The shared
  `TryReadLegacyRuntimeStamina(CharacterSnapshot,out ulong)` is a real dependency.
- Scope: both current player records and their genuine saved/native owners at the
  transfer boundary. Already accepted outer spell cost and receive prefixes are
  historical commitments; transfer refusal grants no refund or duplicate retry.
- Branch2 XPList, chance/stamp policy when target stamina<20, and other receive
  procs are outside this increment.

## Packet contract

Keep successful D13 protobuf update packets and order: attacker stamina first,
then target stamina, using the existing opcode2176/schema. Existing independent
40→60 and80→60 literals remain the successful wire oracle in the numeric package.
No stamina success update may precede a proven paired commit. Delivery failure
after actual commitment must retain that historical result rather than refunding.

## Catalog/table contract

The real equipped Caprice row and Consolidation counter-roll remain prerequisites.
Preserve the existing rune-level/rate/branch rules; do not invent a new table,
stamina cap or missing-row owner. Branch1 requires both unsigned current values,
target>=20 and attacker<=UInt64.MaxValue-20 before preparing its deltas.

## Validation and transaction rules

1. Retain both actual connection issuances, exact records, actual bound client,
   Player and registry/domain/file owners through the real pair boundary.
2. Prepare complete immutable participant graphs, required images and frames
   before either transfer half publishes. A stale or retired participant refuses
   the transfer before either half or the transfer's cooldown stamp publishes.
3. A real writer refusal must preserve both transfer balances/stamp and neither
   publish a success packet nor overwrite an independently newer target record.
4. Two complete final proofs precede both graph stores. Use the existing global
   connection ordering plus actual actor/file orders; a local lock alone is not
   saved-state atomicity. Never invoke two single-owner saves as an atomic pair.
5. If a real durable writer reports uncertain/postcommit failure, preserve the
   exact outcome and fence only genuine captured owners; do not report unchanged.
6. Accepted outer Mana/cooldown/early receive prefixes remain accepted on later
   transfer failure. Unperformed target damage is not a completed outer attack.

## Existing APIs and implemented source boundary

`PlayerAttacks.PrepareLegacyPlayerMagicReceive` currently calls attacker `Set`
after modifier preparation. Nonlethal target stamina+HP follows later in
`PlayerReceive.Commit`. A genuine target Character deny-replace failure can thus
leave attacker+20 while target-20 never publishes.

The new closed `TryCommitCapricePair` uses real `ProcParticipant` records and
retained issuances under the existing ordered Store gates. Both genuine bound
players must share the actual Recovery and canonical Users root. Both real Frame
prepared Character writers receive closed numeric/Stamina+attackerStamp projection
data; complete native HP/Mana/map proofs stay active. No accepting saver callback
or generic extra-attributes bag is added.

Version11 adds a Users-only, two-distinct-Character domain to the existing actual
journal pending/recovery engine; held version10 is unchanged. Full raw beforeimages
are pinned, known durable identity/scalars are validated against the exact Store
records, both full afterimages preserve unrelated text/encoding, then both files
commit before both native/holder stores. The typed afterimage proof includes the
resource fields, so ordinary HP/Mana external-writer checks are not bypassed.

Current exact managed runtime stamina is authoritative at this boundary. Legitimate
previously transient costs/regen may differ from saved Stamina; equality of those
values is not required. This boundary persists those current balances plus the
validated transfer. No permanent saved-stamina anchor is invented. Other runtime
stamina writes remain transient unless their actual producer saves them.

Real prepublication access refusal returns without either graph publication;
pending/mixed journal outcome blocks actual Recovery and retires both holders.
Actual writer-true/final-proof failure retains committed classification. Later
Frame/observer/send failure cannot compensate files or grant a duplicate retry.

The actual receive calls this pair boundary immediately after primary modifier
preparation, before the old attacker Set or later damage/death/tournament paths.
Refusal stops the unperformed receive, retaining already accepted accounting and
early prefixes. Target HP damage remains a subsequent independent commitment.

After the pair, its receipt separately tracks the exact accepted actor and target
records. Reused primary modifiers, tournament writes, transform staging and the
nonlethal damage publication use the original issuance plus that exact expected
record in the real Store CAS. Each successful call retains the actual canonical
out-final. An independently newer same-issuance record is never overwritten by
historical paired data. Failure throws the dedicated committed-continuation outcome
`LegacyCapricePairContinuationRefusalException`; target conflict is
`capricePairCommittedContinuationRefused:targetRecordChanged`. The exception retains
actual historical PublishedAttacker/PublishedTarget. It grants no refund/retry and
does not quarantine or erase a legitimate newer record. A writer refusal while the
same exact record remains current is distinguished as PublicationRefused.
The dedicated type derives from Exception rather than a generic preparation-error
type. The two actual enclosing nested CombatGear receive bodies explicitly rethrow
it, retaining this historical refusal instead of converting it into proc quarantine.

Concrete completed transform/query return records may advance continuation tracking
only if that exact returned object is still current under the ORIGINAL issuer token.
This is not a UID-only latest-owner refresh or permission to replay old payloads.

Physical `LegacyNativePlayerRuntime.Stamina` remains separate ushort storage;
the numeric runtime snapshot accepts UInt64. Existing prepared native publication
joins HP/Mana, spell and proficiency ownership, not a proven stamina participant.
No new narrowing, cap, physical setter or fabricated hydration is authorized here.

Physical-stamina authority remains open. This source preserves ulong managed
values without narrowing and deliberately leaves physical ushort Stamina unchanged.
Joint owned physical HP/Mana participants remain coherent through the existing
complete prepared publication. No full native-stamina parity is claimed.

The pair receipt prebuilds the existing attacker-then-target frames and retains
both genuine issuances/bindings. Each still-unsent stamina field must match before
its pending send; after attacker Send completes, legitimate changes to that already
sent field do not stale the target output. The two pending fields are independent:
a superseded actor reply is skipped without suppressing a still-current target
reply. Pending-field drift has an explicit committed/notification-skipped diagnostic.
Identity or transport
failure fences original issuances and remains a committed failure. No mutation locks
cross sends. A field mutation during its own in-flight send is still not globally
ordered without a shared output reservation; successful delivery is not promised.

The source-supported same-receive LightOfStamina restore remains valid. Its closed
continuation requires100 and builds the later actor100 frame BEFORE the actual CAS;
only successful publication updates the receipt's pending actor frame/value. The
unchanged successful sequence is Light100, then Caprice100, then target60, including
the source duplicate actor100 update. No stale actor60 reply is emitted. Light's
own notification uses current-field/issuer checks too. Light remains an independent
transient stamina mutation: the earlier pair files60/60 stay committed; this change
does not add a saved-Light writer or claim saved100.

## Expected fixtures and qualification

Use genuine ordinary-admitted owned fixtures with actual catalog/PIN/registry,
Recovery, Store, physical players and files. Cover a real Windows Character
replace refusal, a competing exact target record between actual receive stages,
and target retirement at an actual accepted-cost sender. Preserve accepted Mana7
debit and unchanged unrelated files, native owner pins and successful controls.

The two OLD-API controls use actual Frame Send and real sender lock/retirement;
they do not claim a private receive-stage interleave. Root observed one real old
failure (attacker60 rather than40 after target replace refusal) and one retirement
control pass. The successor adds real Store exact-record/retirement checks, both
saved images before first output, large previously transient balances, pending-field
output semantics, transport failure, and direct journal prefix/fresh recovery cases.
The original numeric literal-wire success controls remain required.

The separate continuation test package supplies actual returned private receive
preparations and a real Store target70 publication before the existing damage
commit entry, requiring exact newer record/HP preservation, historical pair files
60/60 plus Mana493, and the dedicated refusal. It also exercises the genuine full
Frame Caprice+Light coexistence with literal packets, target damage421 and current
owned/native/save checks. These are authored source controls until Root executes.

All successor checks are authored, not executed here. Root alone reviews/builds/
executes, records the source-bug entry and qualifies the appropriate full gate.
Existing journal guarantees remain process-restart recovery; no power-loss,
multiprocess, entire-cast atomicity or live-client claim is introduced.

## Root local qualification - 2026-10-29, combined 108 candidate

This appended execution record supersedes earlier source-authored/pending wording
for the locally qualified composition; those historical findings remain above.
Root build04 completed with 0 errors / 114 historical warnings. Corrected focused02
passed 51/51 and neighbors01 passed 3614/3614, with 0 failures/skips in each selection.
These are local source/fixture checks, not a new full Windows gate or live proof.

The prior full Windows40867334704 passed exact531a9aa3 (Core 29,833 passed / 152 existing
fixture skips, Updates 446, Launcher 57). That separate published source predates
this Caprice/shared-journal/status composition and does not qualify this candidate.
No new full gate has run yet. Live/client, whole-cast/SQL/native-physical-Stamina,
multiprocess and power-loss guarantees remain outside this qualification.
The genuine old-API Caprice baseline retained 1 failure / 1 pass: target-file refusal
exposed attacker 60 instead of 40; the retirement control already passed. The new
focused set includes actual pair commit/refusal/output, returned receive-stage
newer-target70 preservation, Caprice+Light coexistence, nested proc continuation,
and real v11 prefix/fresh recovery. The initial3 recovery test failures were an
unrelated-file assertion resolving fixture-relative paths from the test bin;
only that test path resolution was corrected, retaining both actual afterimages
and Users-only recovery assertions. Physical ushort Stamina and later target HP
damage remain separate. Light100 is transient; pair saved60/60 stays historical.
