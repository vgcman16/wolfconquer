# Complete SQL admission data

This extends the [admission image plan](SQL-ADMISSION-IMAGE-PLAN.md) with a
complete typed data reader and an explicit native-prefix preparation consumer.
The source passed229 combined focused C# tests, including all23 new data cases,
and all43 actual SQL Facts,0failed/skipped. [Results and limits](../checklists/SQL-COMPLETE-ADMISSION-RESULTS-20261103.md).

`SqlOrdinaryAdmissionImagePlan.ReadPreparedData()` reads the exact prepared
bundle with the existing Character, item, spell, proficiency, quest and complete
CombatGear parsers. It attaches the actual quest sidecar to Character.PlayerQuests.
The returned Source is the exact immutable after-image bundle, retaining raw
bytes, opaque padding and explicit Missing/Present distinctions. Missing optional
sidecars have legitimate empty parsed models; Present-empty stays Present.

Each read reparses owned bytes into independent mutable model graphs. Caller
changes to dictionaries, quest intentions, item/perfection objects or nested
gear arrays cannot mutate the plan or a later read. Typed models do not replace
the retained raw images and are not serialized back into storage by this reader.
Structural parsing does not supply unknown gameplay meanings or publication
eligibility. Source is historical storage DATA, not a current revision witness.

`LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan` consumes this closed
plan internally, requires exact Source/Core/account/player identity association,
and carries its hydrated quests into PreparedSnapshot. Its optional PreparedData
returns a fresh complete graph. PreparedSnapshot defensively copies dictionaries,
quest rows and intention arrays. The older core-only preparation remains separate
and returns null PreparedData; it does not imply sidecar hydration.

The shared native preparation retains genuine socket-born participants, actual
before-state and catalog checks, observed EOF denial and checked native Online
increment. It uses no shadow file or caller-supplied snapshot/acceptance callback.
It publishes no player, native item/book/gear owner or Store session and commits
no SQL write. Its prefix readiness is not native wallet, SQL revision, item UID
or account authentication custody.

Independent controls cover nonempty six-image data, seven item locations and
nested perfection, literal quest/spell/proficiency records, gear arrays, encoding,
presence, caller mutation and real native refusal/currentness. One new real SQL
case joins actual Found-record planning, observed-revision save and cold-read
data to native preparation while checking peer and UID ledger preservation.
All43 provider Facts actually executed and passed with owned cleanup verified.

Full authenticated SQL-native admission still requires the actual target
account-to-world contract, privately issued Store/revision/UID lifetime, complete
native sidecar publication, every gameplay writer and durable recovery, followed
by permitted live-client acceptance. No backend, dispatcher, routes or client
resources are switched by this component. Existing execution boundaries remain.
