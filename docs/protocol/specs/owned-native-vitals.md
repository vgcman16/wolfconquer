# OWNED-VITALS-1: current ordinary HP and Mana publication

## Identity and scope
- Revision 2, 2026-10-29; Root's sanitized-parity director scope and uint-domain correction, based on current private a5d0aff9 source. This supersedes the implementation ordering in the earlier OPEN `owned-native-mana.md` design; managed health, Mana and FullRestore transactions already exist.
- Target: existing VisibleDx8D13 ordinary saved-account admission and active managed Frame/Store routes. No new client evidence or layout is claimed.
- Initialize physical current HP and uint current Mana from the validated admitted Character. A single Store-before-actor participant publishes both from one complete Character writer/proof. Preserve the ALREADY validated uint managed current-Mana domain; widen the former decompiled ushort backing rather than reject existing accounts. Maximum Mana retains its independent uint domain.
- Root's bounded scope decision: the public current-Mana component setter REFUSES latched owned players before mutation/output. The active managed Frame/Store routes publish through the private joint participant. A full bound component executor/preflight/ordered-send plan is deferred; no new pending-notification writer barrier is introduced. Preserve unbound component behavior separately.
- Native stamina, spell token, Soul authorization, cooldown and late whole-component executor eligibility are NOT established by this scalar participant. No native compound cast activation, SQL/global atomicity, power-loss or authenticated runtime-equality claim.

## Packet contract
- No request/layout change. Preserve current managed Frame successful sequences and unbound native component goldens. No bound public-setter update is activated; active managed routes retain their existing prepared packets and real connection publishers.
- Update packet 2176 uses the existing `LegacyMsgUpdatePacket` serializer; Mana type 2 and uint value. Existing native unbound serializer remains unchanged.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Current Mana | existing Mana update/type 2; hero field 19 | existing 2176 uint/ulong update; hero UInt64 varint | committed current Mana | existing active managed routes; FullLoading gate only for unbound components | existing managed 0..uint.MaxValue | LegacyProtoHeroInfoPacket.FromSnapshot; LegacyNativeHeroInfoPackets.Contract ulong Mana; existing goldens |
| Maximum Mana | unchanged | existing uint | maximum, distinct from current storage | existing routes | 0..uint.MaxValue | admission validation and Character projection |

## Catalog/table contract
- No new catalog. Existing saved Character scalar parser/projection, paid fields and opaque text rules remain authoritative inputs. MinMana must be genuinely parsed; no guessed fallback, clamping, wrapping or new ushort admission limit. Protobuf's larger representable domain does not establish a new gameplay maximum; this slice preserves the current uint managed domain.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Character Attributes | MinMana | parsed long; physical uint | existing validation/current publication 0..uint.MaxValue | current Mana | LegacyOrdinaryAdmissionValidation; LegacyBoundHitpointsScalarWrite.Images |
| Character Attributes | MaxMana | parsed long / uint domain | existing uint bounds unchanged | maximum Mana | LegacyOrdinaryAdmissionValidation |

## Validation and transaction rules
- Real actual registry/domain issuance, account/UID, Frame, Store holder, connection identity, client/player/storage/proficiency references, recovery and current spell/proficiency snapshots remain mandatory. A latched genuine admission binding is never cleared into an unbound setter fallback after retirement or attachment loss.
- Existing package -> Store -> client/player/map actor lock order remains. Public bound setter refuses before acquiring Store or mutating any scalar/output. Unbound setter serializes client -> player and repeats the attachment check, so admission cannot race a raw write. Active managed Frame routes use their existing operation ownership and the private joint participant rather than nesting this setter.
- Prepare full scalar-normalized Character afterimage and immutable map plans before the real writer. Any HP OR Mana change selects the SAME existing typed complete Character writer. External v2 Character+Items/spell writers must prove the identical complete afterimage. Never commit independent HP and Mana images against one preimage.
- Before commit validate actual physical HP/Mana baseline, current holder, prepared scalars/attributes, writer and mutable owner witnesses. On definite refusal no current HP/Mana, holder, file, item, authorization, cooldown or output changes. On committed uncertainty/publication/postlude/notification failure preserve committed files and fence/retire the actual issuance; no refund or retry permission.
- Prepared mailbox/session persistence which planned unchanged vitals cannot publish a producer's unprepared HP or Mana delta.
- The existing native notification bridge checks owner identity but cannot by itself prevent an old setter update waiting behind a newer Frame/CAS commit. That ordered publication seam is why the owned component setter remains refused. No actor-to-Store reentry, accepting queue, pending-writer barrier or native Socket substitute is added.

## Expected wire behavior and fixtures
- Genuine owned ordinary admission fixtures prepare saved Mana before the ordinary input baseline is captured. Cover 0/65535/65536/uint.MaxValue current values, independent uint maximum, negative/>uint refusal before commitment, joint Store/CAS writer, stale/locked file, real journal refusal/postcommit conflict, and bound/retired setter refusal.
- Remove current-Mana truncation from the native unbound cost commit/restoration, its update serializer and active RootFirstFamily manual-MP preparation. Preserve ushort spell-cost/level fields, existing maximum derivation/catalog rules, and independent in-range packet goldens. Genuine active managed potion/cast controls exercise current Mana across 65535; component fixtures migrate only former wrap requirements/type-sized witnesses.
- Actual native component `NativeCanUseSpell.Verified` has four callers (AddMana, BreathFocus, ShieldBlock, CallPet), each with late executor behavior not covered by its resource preflight. Genuinely bound use refuses before any mutation; unbound component goldens remain valid component evidence. EffectMP's separate raw setter executor is likewise not an owned managed Frame route and must not mutate bound sessions.
- The five whole executors acquire a focused real client/player exclusion scope and recheck unbound status before their first effect. This also covers CallPet's clone-removal branch before Verified and prevents ordinary admission from attaching halfway through an unbound executor. It issues no cast authority or invented owner.
- Active managed RootFirstFamily/generic preparation already plans immutable cost/cooldown candidates and uses real Store CAS. Preserve those routes and real potion/trade tests; do not blanket reject them to obtain scalar ownership tests.
- Root alone builds/runs the new and existing tests; source authorship establishes no execution result or live-client proof.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Joint saved/current HP+Mana | single full writer and participant | actual Character parser | existing managed update goldens | source implementation candidate; unqualified |
| Complete external afterimage proof | existing typed projector | Character + health/Mana/FullRestore v2 owners | existing caller goldens | must requalify together |
| Native compound cast | finite activation fence | actual four component callers | unbound component tests only | OPEN: complete executor eligibility/effect owner/commit plan absent |
| Native maximum/current widths | preserve existing managed uint domain | actual saved scalars, hero field 19 | no layout change | widen physical current Mana; negative/>uint refusal; maximum independent |

## Implementation and qualification
- Extend the existing `OwnedHitpointsPublication` participant and full Character writer selection without renaming unrelated HP modules. Add focused public component Mana refusal/unbound routing, uint current backing/commit/diagnostic DTO and manual-MP narrowing correction, plus prepared-persistence unchanged-vitals guard. Existing generic ordinary uint admission validation remains unchanged. No Store setter/Frame setter hook is activated.
- Genuine saved-owner tests plus existing ordinary HP/managed cast/consumable/trade/recovery/diagnostic suites are required. No build/test/parse/host/Git/network execution in this helper package.
- Diagnostic proof flags remain false: selected equal values do not prove exclusion of raw item/other native writers or global atomic tuples. Replace stale wording about unlocked Mana: active owned writes use the joint Store participant and the public component setter refuses owned sessions.
- WRI1's detached native Mana DTO also becomes uint; JSON keys/framing/version and both false proof flags remain unchanged. Old <=65535 values remain accepted; a previously built ushort-only query client needs the matching updated Core to decode larger observations. This is not a file/runtime acceptance claim.
- OPEN next slice: genuine complete bound native cast plan must own stamina, cooldown, weapon/Soul/sigil authorization, all late executor checks/effects and durable publication before activating the four whole components. Scalar admission/hydration is not that proof.

Root qualification2026-10-29: old-code ten-case baseline failed8/passed2.
All25 genuine owned-vitals controls passed, as did the zero-cost metadata controls.
The broad selection passed1882 with122 existing fixture skips and one invalid
component setup: uint.MaxValue base maximum plus an equipment bonus overflowed
before the cast. That setup now earns a normal independent maximum first, then
sets wide current Mana through the actual component Store. All3 component values
65535/65536/uint.MaxValue pass. No product calculator bound was weakened. Three
old native wrap expectations now retain full values and independent animation
bytes. Build09:zero errors/100 historical warnings, none in changed files.
Selections overlap. Full successor CI and live client acceptance remain pending.
