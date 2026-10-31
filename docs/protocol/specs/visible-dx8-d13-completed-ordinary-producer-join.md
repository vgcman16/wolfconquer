# FSB-256 draft: completed ordinary producer history and owned Rune recovery

## Identity and scope

- Revision 4 source candidate, 2026-10-30; Root owns qualification and integration. The approved authenticated creator, starter producer and completed-history join are authored and independently source-reviewed. All new C# remains unrun.
- Target: existing VisibleDx8D13 managed Runtime/Frame/Store and configured file/UID domain. This introduces no 7937 activation or client acceptance claim.
- Source baseline: frozen FSB-255 successor, with its 70-path delta preserved independently. This candidate is source only and unqualified.
- Intended behavior: a genuinely completed ordinary starter transaction must not permanently fence later Rune actions or their owned startup recovery merely because its completion history remains. Active, unknown, corrupt, foreign or incomplete work remains fenced.
- Exclusions: duplicate starter grants, inferring entitlement from empty equipment or missing files, accepting an archive index/name/hash alone, general mailbox completion acceptance, generic pending-journal bypass, Decompose live abort reconciliation and deployment.

## Packet contract

No new packet, opcode, encoding or successful Rune response sequence. The existing authenticated InternalWorldEnter transport can establish creation authority before the existing NewRole request; this is an explicit internal-transport capability, not proof that the protected native D13 ticket-to-wire binding has been implemented. Direct legacy MsgLoginGame.Key cannot establish this authority. Existing client-confirmed Rune outputs and ordinary admission ordering remain unchanged.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| MsgLoginGame.Key | Existing decoder | Existing uint | Claimed character key | Existing packet | Not an authenticated creation permit | WorldServerFrameHandler.HandleLegacyWorldLoginAsync |
| PendingLegacyCharacterCreation.CharacterUid | Managed only | long | Missing-snapshot request association | One per connection | Contains no redeemed account/create entitlement | WorldServerFrameHandler.LegacyDuplicateLogin.cs |
| NewRole name/body/class | Existing decoder | Existing layout | Requested new role | Existing packet | Existing syntax/body/class checks do not establish birth authority | WorldServerFrameHandler.HandleLegacyNewRoleAsync |

## Catalog/table contract

No gameplay catalog determines completed-work authority. Existing starter profiles/templates are projection inputs only. The historical recipe pins complete configured routes, before-image hashes, Character/Spells/Proficiencies after-image hashes and item templates; these hashes establish relationships inside the actual producer transaction, not fresh permission to grant items.

| Stored shape | Validation | Behavioral relation | Evidence |
| --- | --- | --- | --- |
| LegacyOrdinaryAdmissionRecipe | Canonical bounded binary roundtrip; account; exact configured roots; 1..4 valid unique equipment positions; exact request/digest/operation/receipt binding | Historical ordinary starter projection | LegacyOrdinaryAdmissionRecipe.cs; UidOwner.Completion.cs |
| ProducerArchive v1 | Strict canonical JSON, complete intent/allocation, domain, exact retirement generations, prior anchor, before-state digest | Durable output of an actual completed-work retirement | LegacyItemUidRegistry.ArchiveFormat.cs |
| ArchiveCatalog | Full bounded file read, anchored chain/count/hash, union of nonoverlapping allocations/generations and unique operations | Registry-owned historical custody, not materialization permission | LegacyItemUidRegistry.ArchiveCatalog.cs |

### Explicit history-reader budgets

The prior full history walk had the existing 32KiB per-file archive limit without an aggregate count/byte guard. This change applies explicit resource-safety policy at `ReadArchiveCatalog`, before its contents can supply a completed-history result:

- At most65,535 actual filesystem entries in the history directory, including any candidate next-retirement file. Count each enumerated entry before reading it; unexpected names/directories still refuse, rather than being skipped.
- At most256MiB of actual complete archive bytes in one full walk, including the separately staged archive if that existing transition path permits it. Charge the complete bytes returned by the existing stable bounded file reader with checked arithmetic before retaining the entry in the catalog. A single read is already capped to32KiB; stop immediately when the aggregate would exceed budget.
- Retain the existing32KiB archive-file,8KiB recipe,128 active-producer-intent and256KiB aggregate active-payload limits. Name scanning has its own separate proposed65,535 Character-file/256MiB complete-image budget and existing journal16MiB per-image cap; it is not charged as UID/raw-catalog scanning.
- These are explicit engineering resource limits, not inferred client population/gameplay limits. Over-budget state fails with a precise resource-bound reason and remains untouched. This does not claim support for arbitrarily large production history or indexed archival compaction. No file outside the configured history/Users roots is exempted.
- The common full catalog reader receives these fixed guards so an earlier generic open cannot perform an unbounded walk before the new typed join checks its own limits. Direct requested-archive reads retain their existing exact-index/hash/per-file validation; the typed join always performs the complete bounded chain/union validation.

## Validation and transaction rules

### Existing real completion authority

1. Actual LegacyOrdinaryAdmissionUidOwner owns the current exclusive registry/domain and complete configured roots.
2. TryReadCompletedIntent binds the canonical recipe/request/allocation and compares the real complete current saved inputs to recipe.IsAfter.
3. Only that owner can mint LegacyOrdinaryAdmissionCompletedWorkProof; its private constructor copies the exact full producer snapshot.
4. TryArchiveCompletedOrdinaryAdmission requires this proof to match the exact retained intent/allocation and current state, then publishes the exact retirement/archive transition.
5. The durable archive records that completed transition. Subsequent legitimate changes invalidate the old after-image equality but do not undo that historical completion. Existing ordinary reconciliation already checks IsAfter only for active work and separately checks archived entitlement uniqueness.

### Closed historical reader

- Add a narrowly typed completed-ordinary-history result issued inside the actual registry/custody implementation after the full anchored archive is read and validated, never by an external caller supplying a writer string, digest, receipt or boolean.
- Validate each accepted full archive as the known ordinary producer recipe: current domain; exact configured routes; canonical operation/request/payload; matching allocation; canonical account/Character relationship; one completed ordinary history per character; no active normal work for that character. Preserve the archive chain, all ranges, high-water, generations and bytes.
- Require zero active producer intents and no outstanding ordinary allocation row before a Rune operation, game-journal recovery or namespace-only cleanup may use this result. Other well-formed untyped allocation receipts retain their existing coexistence behavior and give no game authority.
- Refuse other archive producer kinds, unknown versions, malformed recipes, mixed/duplicate histories, missing/changed chain members and unknown staged work. This is not blanket archive acceptance.
- Fresh pending-game recovery uses the actual new exclusive OwnedStartupCustody and its captured state/archive/current-root checks. Live Decompose uses the actual current Runtime/registry and existing lock/current checks. No generic open or recovery entry point gains authority.
- Do not compare current Character/Items/Spells against the original starter afterimages: later legitimate gameplay changes them. Do not materialize, regrant or infer completed gameplay merely from the historical reader. The reader only distinguishes known completed history from unresolved debt under the configured-journal trust boundary.
- The reader does not cryptographically attest that an old process executed a writer. It consumes trusted configured durable history through the actual publisher's complete canonical retirement/recipe/allocation chain; it is not an authenticity oracle for arbitrarily forged raw files. Genuine fixtures exercise the current private completion publisher. Historical ticket digests, writer names and hashes alone never authorize a new birth or a new player action.
- Preserve existing full configured journal/stage clearance, exact captured pending identity/current target checks, raw physical UID uniqueness/high-water rules and same-lease transfer/rescan. Existing namespace retirement removes only its exact typed reservation row and preserves completion history byte for byte.

### Genuine creation prerequisite discovered before implementation

The current source cannot produce a new ordinary starter completion through a live owned API:

- Both LegacyOrdinaryAdmissionUidOwner.TryAllocate overloads and MatchesPending deliberately refuse the retired admission allocator.
- Owned HandleLegacyNewRoleAsync refuses ordinaryAdmissionUidCreatorRequired before the old derived-UID writer.
- PendingLegacyCharacterCreation holds only a character key from the legacy login packet. AccountHandoffService is redeemed by the separate internal world-enter path; its authority is not joined to legacy NewRole.
- The old creator checks syntax/body/class and Character FileMode.CreateNew; no complete configured-sidecar absence or unique-name owner is joined.

A fixture may not manufacture a positive producer intent/archive to work around this. A real authenticated account/connection creation permit, exact absence/name ownership and atomic starter producer will be implemented as prerequisites. Existing mailbox completion is a different producer and will not be substituted to claim ordinary creation.

### Actual creator APIs and authority chain

1. Only the private successful `HandleWorldEnterRequestAsync` redemption branch may install a private active `WorldServerFrameHandler.AuthenticatedCreationRedemption` reference after a real MissingSnapshot result, then request `LegacyOwnedAdmissionRuntime.TryPrepareAuthenticatedCreation(actualRedemption, ...)`. Its closed current check requires that exact Frame-private installed reference and connection/thread/lifecycle; constructing an inert evidence object cannot install it. A public `AccountHandoffRedeemResult` or ticket record supplied by a test/caller is not a permit. The Runtime installs its own private active permit only after consuming that genuine Frame evidence under the actual owner gate. Account ID is positive uint and maps to the same new Character UID, as the actual ordinary recipe requires. Pin the actual Frame/Runtime, current registry/domain/configuration, connection transport lifetime/send/disconnect routes, native pre-character instance/player with UID0, username and redeemed ticket identity. A reused ticket, different connection/Runtime/native owner, ended transport or existing character cannot take the permit.
2. The connection-local pending creation state may refer to that registered private permit. The raw legacy-key missing-snapshot path can still send its existing diagnostic/NewRole response but never obtains a permit or falls back to derived item UIDs.
3. A dedicated Runtime creation serialization scope owns name admission and the complete transaction through final completion. A new `LegacyCharacterCreationNameClaim` is created only under that scope and the actual current exclusive registry lease. It scans the real configured Users root, parses complete ordinary Character images, compares actual normalized names (never hash codes), rejects ambiguous aliases/malformed evidence and duplicate names, and pins membership/current bytes until commit. Persisted Character names and retained actual creation recipes reserve names across restart. No accepting name-index callback or caller-provided free-name flag exists. Root explicitly chose full-string `OrdinalIgnoreCase` collision refusal as a deliberate sanitized server naming policy replacing the original collision-prone `GetHashCode` uniqueness; client casing semantics are not established by that choice. Preserve actual name encoding/roundtrip and reject ambiguous representation.
4. Require every configured ordinary participant leaf absent: Character, Items, Spells, Proficiencies, Quests and CombatGear, plus the configured character mailbox leaf. Directories, aliases/reparse evidence and malformed/mixed existing state refuse. Existing global detained/redeem/claim files remain participants of the ordinary raw namespace scan and are never erased.
5. `LegacyAuthenticatedCharacterCreationProjection.Capture` plus the privately constructed `LegacyAuthenticatedOrdinaryCreationProducer` consume the actual Runtime `CreationLease` and name claim, validate the existing NewRole name/body/class mappings and genuine adopted starter item/catalog rows, freeze the existing tuned starter projection, prepare deterministic images before reservation and bind the complete current before-absence state. The actual private permit is consumed only when the real producer intent is durably issued; preflight refusal does not burn it. Any uncertain issued transition fences creation/retry until the real reconciliation path completes it. This includes an uncertain UID preparation result, every later false return/cancellation/exception and failed completion, not only explicit interruption controls.
6. A dedicated owner method prepares the canonical creation recipe with actual `Registry.TryPrepareProducerIntent`, then reserves with `TryReserveProducerIntent`. The retired generic admission TryAllocate ABI stays refused. The real receipt supplies every physical starter UID; no local/derived allocator is restored. Prepared generation, payload, allocation and operation identity remain exact through commit and archive.
7. `LegacyTradeJournal.TryCommitAuthenticatedOrdinaryCreation` is a new closed version19 producer entry point. It accepts only the actual prepared plan/capability, six create-only targets with null BEFORE and complete nonnull AFTER, the exact mailbox-absence input, actual declared creation recipe and real allocation. It uses ordered file locks and the existing durable pending/stage/replacement pipeline; no version7 preservation rule is relaxed. It validates complete parser/save graphs, exact projected item template/range, canonical Character/spell/proficiency/quest/gear projections and name/current/permit proofs before pending publication and at actual writer boundaries. No Store/native success publication or success packets precede durable commitment.
8. After all six afterimages are exact, the actual ordinary completion owner captures its private CompletedWorkProof and archives the exact producer intent/allocation through the existing real registry publisher. Only then does ordinary admission publish its normal native/Store state and successful startup. Failure after durable game commit is completion/reconciliation debt, never an uncommitted rejection or permission to issue a second grant.

### Durable recipe and recovery shape

- Preserve existing `ordinary-admission-v1` decoding and historical semantics. Add a distinct canonical authenticated creation recipe encoding; do not reinterpret the old hash-only payload as a new creation permit.
- The canonical binary payload contains the `authenticated-ordinary-creation-v1` kind, account uint, username, lowercase64hex redeemed-ticket digest, actual normalized name, body/class-selection ushort, prepared local DateTime ticks, six explicit kind/root pairs in Character/Items/Spells/Proficiencies/CombatGear/Quests order, exact mailbox root, then length-delimited canonical strict metadata JSON. Before-absence membership and all five non-Items images/templates are derived by this known version's pure canonical projector; no arbitrary afterimage or HP scalar is accepted in the payload. Items AFTER is derived from those actual templates plus the real registered receipt. The ticket digest alone grants nothing; the private live issuer and trusted configured durable producer intent are the two lifecycle authorities.
- Bound the payload by the actual existing 8KiB per-intent and 256KiB aggregate limits; at most four starter roots, with actual unique positions. Preflight enough generation room for prepare, allocation and retirement and the actual operation/intent table limits. Reserve history count room plus the existing worst-case32KiB archive bound before intent issuance, so normal completion cannot exceed the new full-history resource budget. Reject before issuance if the exact real recipe cannot fit.
- Version19 has exactly Version/TransactionId/Changes/AdmissionDomain/CreationMailbox/OrdinaryCreation root fields. OrdinaryCreation has exactly Payload (Base64 binary recipe), PreparedGeneration (positive ulong) and Allocation (the actual canonical nine-field receipt). The complete request is derived from the recipe digest and bound against the real registry intent; it is not a second caller-selectable request. The six ordered Changes have null BEFORE and exact reconstructed nonnull AFTER, with mailbox absence as a required input. Strict readers reject unknown fields/kinds/versions, mismatched domains/routes/account/UID/request/generation/range, malformed aliases and any non-create-only target. Protect the exact birth range from physical ownership anywhere except the captured Items target; ordinary references still obey the pinned high-water rule.
- An actual retained new creation intent must be recoverable even if interruption occurred before the game WAL. A fresh exclusive configured creation-recovery owner may decode only this known complete recipe, validate full game-journal/stage/name/current absence and raw namespace clearance, reserve its existing intent if not yet allocated, and publish the exact version19 journal. This finishes the previously authorized durable intent; it never derives entitlement from historical completion, empty files or a name. Old hash-only unallocated/mixed work remains fenced because it cannot reconstruct missing business images.
- Pending version19 recovery may coexist only with its own exact actual creation intent/allocation and previously validated known completed ordinary archives. Unrelated active producer work remains denied. At partial replacement, every target must be exactly its captured null BEFORE or recorded AFTER and every stage/pending identity remains exact. The same actual exclusive lease survives recovery, postscan and normal owner transfer.
- After journal completion, the real ordinary reconciliation owner observes all exact afterimages and performs its existing private completion/archive path. A second startup sees completed history and does not reconstruct or regrant starter items. Later Rune mutations are accepted by the typed completed-history reader without comparing obsolete starter afterimages.
- The pre-WAL recovery candidate stays entirely unpublished. Its private Runtime startup authority requires the actual current registry and claimed ordinary owner; after a real missing allocation is reserved, `OwnedStartupCustody.BorrowAuthenticatedCreation` captures the same existing exclusive Registry FileStream and pinned state/archive proof. It checks reserved physical UID occupancy before publishing the exact19 pending record, then uses the existing captured-record owned recovery pipeline. Success requires private archive completion and fresh raw scan before normal recovery/readiness and Runtime publication. All failures return no Runtime, preserve primary and cleanup diagnostics and release the actual lease.
- Foreign stage/temp/backup evidence in any of the seven roots refuses. Only exact six recorded transaction stage paths with bounded correct afterimage prefixes are recognized; no caller supplies exempt roots or ignored paths. The actual preparing/pending identity is rechecked at live write boundaries and at recorded startup publication.
- Historical starter HP is a specific projection dependency, not an arbitrary captured scalar. The existing creator uses `ApplyLegacyFirstCharacterStarterScalars` plus actual `LegacyMaxHitpointsCalculator` inputs. The live producer rejects an existing InnerPower rank for that genuinely fresh UID as stale progression; it may not manufacture a zero fallback. The exact canonical fresh Character, no Rune/equipment enhancements, empty Gear and absent progression/Chi/subclass/Beast/transform/status inputs prove all other contributors zero.

### Exact fresh projection API and HP witness

`LegacyAuthenticatedCharacterCreationProjection.Capture(WorldDataCatalog catalog, uint account, string name, ushort body, ushort classSelection, DateTime preparedAt)` is a metadata-only actual catalog capture, not permission. `.Rebuild(account, name, body, classSelection, preparedAt, metadata)` is the pure historical projector. It exposes copied complete Character/Spells/Proficiencies/Quests/CombatGear images and copied actual `LegacyOrdinaryStarterTemplate[]`; its live `.IsCurrent()` pins complete actual raw/index references, required fields and optional absences. Root's creation permit/lease remains independently required for execution.

The historical metadata DTO has only these closed dependencies:

| Member | Exact shape | Validation and use |
| --- | --- | --- |
| Items | Unique rows sorted by ItemId: uint ItemId, string RawHitpoints, string RawDurability | Actual starter item IDs; raw itemtype fields18/21; canonical numeric/native bounds; no durability fallback. HP charges each physical template, including two roots of the same item type. |
| Profession | Null for actual absence, otherwise exactly9 raw fields | Fields0ID/1Class/2Rank/3..7five encoded bonuses/8empty; actual parser and typed index agree; only the actual new class row. |
| RuneStorageZeroScore | Required array; zero or more exactly9-field rows in actual source order | Fields0ID/1requiredScore exactly0/2..7three type-value pairs/8empty; actual parser and typed agreement; complete score0 membership, not caller-selected bonuses. |
| InnerPowerRows | Required empty array | Actual raw/index lookup for the new UID has no row. A present row is stale progression and refuses live creation. This cannot supply a selectable arbitrary HP bonus. |

The exact pure HP formula is checked `B + equipment + profession + storage`, where `B` is `LegacyFirstCharacterStarterProfiles.CalculateHitpoints` using that class's fixed fresh attributes; equipment is the sum of captured item18 HP per actual physical starter template; profession adds positive encoded bonuses whose type is6 (`value/10000==6`, amount `value%10000`); storage adds every positive type1 value from each actual score0 row. The total must fit uint and matches the existing fresh source calculation (which takes max withB). No AfterHP, generic bonus scalar or arbitrary metadata catalog argument appears in the durable recipe. Canonical fresh images prove every other calculator contributor zero. The projector must also require parsed saved Name equals the requested normalized name, avoiding silent plain/base64 name reinterpretation.

Actual `LegacyMaxHitpointsCalculator` uses a checked Int32 account conversion for the InnerPower keyed lookup. This creator therefore requires account<=Int32.MaxValue; it does not silently omit that dependency for larger native uint IDs. Root accepted this explicit current owner boundary.

## Expected wire behavior and fixtures

- Required positive: actual authenticated creation producer completes its real UID intent, six complete configured saved images and private completion archive; actual admitted newborn performs a legitimate supported durable action that changes its Character image; a same-domain preexisting registered Rune actor then performs a genuine Rune/Decompose WAL interruption and owned recovery with that completed history, followed by a second restart without replay.
- Required preservation: completion archive/state anchors and unrelated receipt rows remain exact throughout ordinary admission, Rune commit, namespace cleanup and restart.
- Required negatives: active/unallocated/allocated incomplete producer debt; unknown producer; corrupt/foreign/missing archive; changed recipe/root/account/allocation; duplicate completion; stale or ended custody; stage/pending drift; unrelated UID collision. Negative mutations may only alter evidence captured from a real positive producer for that producer's test.
- Creation owner failures must precede UID reservation/files/success output. Post-intent or postcommit uncertainty requires real reconciliation rather than a fresh grant/retry.
- No positive handwritten history, accepting persistence callback, fabricated account/create permit or fake owner.
- Newborn eligibility is not invented: canonical creation remains Reborn0/Level1. Current RuneBag inventory-to211 acquisition requires Reborn>=2/Level>=15 and still publishes the session before its best-effort item save. This is an explicitly OPEN unsafe acquisition seam, not fixed by this milestone. Do not inject Rune UIDs or edit newborn progression to claim new-character Rune acquisition. Root accepted the same-domain real Rune actor fixture plus a real durable newborn Character change as the proof of historical completion compatibility. Independent projection tests must compare the full existing HP calculator for every supported class and positive profession/score0 rows.

## Evidence alignment

| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Genuine ordinary completion | Existing real completion authority | Not applicable | Required actual producer fixture | Completion/archive machinery exists; creator retired |
| Completion survives later Rune changes | Closed historical reader | Not applicable | Required real later mutation/restart | Existing ordinary reconciliation already separates archived history from active after-image checks |
| Fresh birth authority | Genuine creation prerequisite | Starter profile is projection only | Required authenticated creation fixture | Missing account-to-NewRole creation permit/owner; no activation inferred |
| Unknown work stays fenced | Closed historical reader | Not applicable | Required captured-evidence refusals | Existing conservative fence retained until approved join |

## Implementation and qualification

- Actual production islands: authenticated permit/name/absence/EOF owner; pure fresh creation projection with actual catalog witness; registered producer/strict version19 writer; fresh startup same-lease completion; known completed ordinary history join. The external `PATHS.txt`/`DELTA-PATHS.txt` lists77 paths (63 production,13 test,1 spec),52 added/25 changed versus frozen255, with no new deletion.
- Helper split: pair_host_tests authored12 owner production paths and20 test outcomes; pair_source_tests authored8 projection production paths, one focused existing catalog-fixture hunk,27 data outcomes and24 genuine producer outcomes. Lead authored the central schema/producer/recovery/history joins and3 archive-budget data controls. All74 new outcomes are authored UNRUN. Independent source review cleared each group after the recorded findings were corrected; this is not compilation or runtime qualification.
- No build/test/runtime/API/Git action was performed by helpers. Root qualification and finite inherited255 corrections are required before integration. Root's sole executing gate and its actual retained receipts are separate evidence.
- FSB-256 remains a draft. Full Rune/gameplay/production recovery and full client parity remain active. Native D13 authenticated ticket-to-wire binding, account registration/SQL creation, general completed mailbox work, the unowned legacy creation route, production-scale archive compaction and RuneBag acquisition remain outside the implemented evidence. The new owned internal-transport creator does not establish those capabilities.


## Root qualification — 2026-10-30

Solution Build03 and final fixture test Build06 passed zero errors; no changed
C# warning remains. The first combined selection passed1,809/failed49/skipped0
(1,858 total). Actual TRX shows all49 failures were new fixture endpoint assertions
before their genuine bodies; the existing127.0.0.1:5818 wire golden stays unchanged.
Only those two test expectations changed. Corrected creation02 passed81/81,
zero failures/skips in3m20, including all49 genuine creator/owner/recovery outcomes
and32 projection/resource data controls. The81 and1,809 sets overlap; this is not
one fresh1,858 green run. Production is unchanged between them. The earlier
failed TRX/source are retained; exact-public fullWindows remains pending.

The lifetime supplement is included: actual completed/EOF reference retirement
and full known-history deny prevent repeated starter creation after saved-leaf
removal, while issued uncertain work retains its exact range. Native D13 protected
ticket binding, registration/SQL, physical power loss,7937/English/live acceptance
remain open. Helpers authored source; Root alone ran the actual checks.
