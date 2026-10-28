# World session publication and observers

## Identity and scope

2026-10-28, source-derived correction to public4d9280fe. Target remains
Chinese7899 / VisibleDx8D13. This is unqualified until the recorded tests run.

## Packet contract

No packet IDs, fields, encoding or successful frame order change. Callers retain
the actual committed result and original native issuance when deciding whether
to deliver later frames. A postpublication observer may retire or replace that
issuance; old notifications must then remain suppressed.

## Catalog/table contract

Not applicable: this changes session publication phases, not a data layout.

## Validation and transaction rules

1. Retain the existing package -> Store -> actor order, current-holder checks,
   immutable plans, final native witnesses and preparation reentry refusal.
2. Publish the prepared native graph and actual holder while their real gates
   remain held. Dispose actor preparation before any persistence postlude.
3. The concrete owning WorldServerFrameHandler may run its existing typed
   persistence postlude under the exact current Store gate. It verifies that the
   issuing Store is its own and the supplied record is still current. An arbitrary
   observer delegate is not this owner or a successful persistence participant.
4. Release every package, Store and actor gate before invoking the public
   sessionChanged observer. Its legitimate retire/update may use the normal
   Store APIs; no exception to the preparation-time reentry guard is introduced.
5. Assign the actual committed result before the observer. An observer exception
   happens after publication and cannot establish an unchanged rejection or
   roll back committed state. No observer runs on failed CAS/preparation.
6. Capture the exact final native issuance and original optional binding under
   the publishing gate, alongside the canonical result. Never re-resolve those
   objects after a callback. An exception from either postpublication phase is
   `WorldSessionPublicationException`, with `PublishedSession`, original
   `InnerException` and `Stage` (`FramePersistence` or `Observer`). This proves
   historical memory publication, not successful durability of every sidecar.
   Stronger inner committed/pending journal outcomes and existing Recovery
   reasons remain intact.
7. Frame-phase failure quarantines under the already-held package/Store gate.
   Observer-phase failure reacquires the normal package -> Store order. Retire
   only the retained binding and clear a holder only if its current issuance
   equals the original captured identity. A genuine remove/reissue survives,
   even if it reuses the exact record/account/UID. The real owning Frame and
   original binding block their actual Recovery; no substitute is constructed.
8. Compensating callers must catch this typed outcome before broad preparation/
   I/O catches. Six Labyrinth commit paths and DisCity DarkHorn retain their pre-CAS file changes,
   suppress success output, block applicable actual configured Recovery and
   log a published outcome requiring recovery. They never restore before-images
   based on a postpublication error or call it an unchanged rejection. Genuine
   false CAS and definite prepublication writer failures retain compensation.

## Expected wire behavior and fixtures

Use genuine Store registrations to prove observer removal/reissue, cross-holder
updates, committed output retention and rejected preparation reentry. Use the
existing genuine ordinary-admission fixture to prove the concrete Frame
persistence postlude still writes saved spell evidence. Keep boss/status and
other existing delivery-suppression controls meaningful.

Relevant source-authored acceptance families are
WorldSessionPublicationObserverTests, WorldSessionStoreNativeIdentityCasTests,
WorldSessionStoreCompleteLoginTests, WorldSessionStoreConcurrencyTests and the
four LegacyLabyrinth FloorAdvance/FrozenGrotto/SimonEntry/SimonDiamond families.
LegacyDisCityDarkHornParityTests covers the additional actual compensating path.
The controlled observer barrier allows actual removal while the observer waits;
preparation-time nested removal still refuses. Real same-record reissuance is
separate from a later record on the original issuance. An actual Frame missing-
root diagnostic-route failure proves the distinct Frame phase and actual
Recovery block without pretending a file was saved. Labyrinth cases retain
known after-images, suppress output/replay and preserve real prior writer-
refusal, authorization-change and stale-CAS controls. None has run in this helper.

## Evidence alignment

WorldSessionStore.Set/CAS, CompleteLogin and SpellProducers currently invoke the
public callback while their Store-gate thread scope remains active, even after
actor disposal. RefuseBoundSpellReentry therefore rejects legitimate observer
retirement in the terminal full CI. Frame persistence is currently supplied
through that same delegate and its bound autosave requires the actual Store
gate. These two responsibilities must be separated, not globally unguarded.

## Implementation and qualification

WorldSessionStore.PublicationObservers owns the concrete Frame hook and public
notification. Root updates the four publication callers and the real Frame
constructor/postlude. The failure classifier and actual compensation callers
are a separate isolated continuation in helper-ci-observer-outcomes-4d928-next.
Tests and full CI are pending. This is not a general
callback transaction, native SaveClient/SQL closure or live-client proof.
Existing unbound sidecar saves can still log gaps rather than throw, and the
new exception type does not make those writes globally atomic or crash-safe.
A bounded direct World Set/CAS catch audit found no additional adjacent broad
compensation blocks beyond these seven. BotJailRelease compensates only false
CAS and its infrastructure filter excludes this exception type; MonsterDeath
Map1020 has no surrounding compensating CAS catch. This is not proof of every
indirect helper call chain.

## Secondary fencing outcomes

Public observers may overlap after their own publication scopes end. Each receives
the actual previous/final transition captured at publication; another publication
may already have advanced the current holder. Observer timing alone does not prove
that its historical result is still current. Concrete Frame persistence retains
its actual current-owner gate before notification.

The typed publication exception keeps the original stage, published session and
cause even when a recovery block, binding retirement or holder fence itself
fails. Its `FencingFailure` records each such secondary exception; independent
fencing steps continue. When the package scope cannot be entered, the holder
quarantine may remain incomplete: the code does not invert the Store gate
order to force it. A caller must treat that secondary detail as a real fencing
failure, not evidence of successful quarantine.

Identity checks protect a newly issued holder from clearing the old holder's
identity. This does not establish that the new owner is usable: it can share
the same world Recovery, which the original publication failure blocks.
The direct gate-order control uses a real package producer and managed Store;
it does not prove ordinary native re-admission or an unblocked shared Recovery.
