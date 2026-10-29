# VisibleDx8D13: health consumption preserves unrelated trade offer identities

Root execution: build04 had zero errors; the unchanged two-case test produced
one pass/one failure against the original health producer, with the real final
confirmation logging `offeredItemChangedOrMissing:refunded`. After the single
production-plan change, build05 had zero errors and both cases passed. The
broader successor passed587/587 with zero skips. Full CI remains pending; no
live client was used.

## Identity and scope
- Revision 1, 2026-10-28. Follow-up to the frozen 11-path health transaction cut.
  Root reported its focused 57/57 checks passed; that did not cover this real
  cross-feature ownership seam. This follow-up itself has not executed.
- Source-derived defect: health permits an unoffered potion during a managed
  trade, but publishes EquipmentMoveInputs.Stable's wholly copied item graph.
  Trade offers retain their original Closure.Source item and deposited-row
  references. Later ValidLegacyTradeOffers -> Closure.MatchesSource requires
  those references, so unrelated health use invalidates a valid existing offer
  and final confirmation refunds/cancels with offeredItemChangedOrMissing.

## Packet and data contract
- No packet/catalog changes. Preserve literal existing request/accept/offer and
  both confirmation packets, D13 health item/max/max+current outputs and successful
  trade completion action4 for both real participants.
- Detached copies remain the deterministic health planning/persistence input.
  Complete raw image and graph validations, shared Character projector, v2 WAL,
  current owner/PIN/trade gates and before/after witnesses remain required.

## Ownership and commit rule
- Build the live final item graph from the ACTUAL current original graph by
  replacing/removing only the selected potion row. Retain exact object/child
  identities for every unchanged row, including another item's real offered
  closure. Keep the original source graph unchanged before commitment.
- Independently derive the detached aftergraph and consumption response from
  copied Stable inputs. Require live/detached modeled afterimages and response
  bytes equal before preparing persistence/Store publication. Capture the live
  aftergraph and retain its final proof as well as original source witnesses.
- Persist the same complete exact detached Character/Items afterimages through
  the existing real v2 writer, then publish the live graph. Never rebind an offer,
  relax trade's exact identity proof, create a party or block all managed trades.
- Consuming a UID already offered remains a precommit refusal: HP, cooldown,
  item stack, files and outputs stay unchanged and its original offer stays valid.

## Genuine regression and qualification
- Reuse OwnedEquipmentBehaviorFixture's actual two-client saved inputs, registry
  bootstrap, owned runtime, native ordinary Login, PIN, real common Store and
  actual Frame dispatch. No fake party, session injection or accepting saver.
- Actual request0/accept1/offer6 establishes the offered item/closure. One case
  offers an unrelated Meteor and uses a health potion: require literal HP10->80,
  stack3->2, both durable images before first health output, then BOTH actual
  confirm9 packets must finish a valid transfer with completion4 for each client.
- A second case offers the potion itself: actual use refuses unchanged, both
  confirms still transfer its full stack3 and HP remains10. Check original offer
  identity, native/Store/saved HP, final UID uniqueness, durable files and recovery.
- The new regression must run against the current old health producer FIRST;
  defer identity assertions until after final confirms so the old producer's
  actual trade rejection/refund is observed rather than a synthetic send fault.
  Root retains real red output, then composes the narrow plan fix and runs green.
- Root alone builds/tests/integrates. This source does not prove SQL, power loss,
  arbitrary cross-client gameplay, live-client acceptance or other consumables.
