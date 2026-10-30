# FSB-214: Claim native message-box callbacks before invocation

## Identity and scope
- Revision1,2026-10-29; Root-owned offline follow-up after the70-path packet batch.
- Baseline91040398 plus captured70 Root overlays, including genuine StaticReply Size and truncated-input controls. Existing7899/VisibleDx8D13 evidence; no7937 or receiver registration.
- Production scope: `LegacyNativeMsgStaticMessage.Handler.cs` and `LegacyNativePlayerRuntime.MessageBoxState.cs`. Caller map finds MessageOK/MessageCancel/StartMessageBox only in those2production files; no actual production installer, reflection/ref field user or live producer is fabricated.

## Packet contract
- Native2320 component handler consumes the existing GetStaticMessage UInt32 LE Accept at the current cursor. Keep the complete-input bounds and current-cursor behavior; do not invent another opcode/layout or box identifier.
- Only Accept1 is admitted. Other values return after valid parsing and do not dereference a missing client, preserving existing bounded controls.
- BlockTeleportMap membership retains callbacks/deadline. Future deadline (`StartMessageBox > DateTime.Now` at claim) admits OK, falling back to Cancel only when OK is null. Expired/equal deadline clears the currently claimed callback pair without invoking either. The deadline value itself stays unchanged.

## Catalog/table contract
Not applicable; no data/catalog change.

## Validation and transaction rules
- Parse a complete response before callback authorization changes. For Accept1 require the actual nonnull user/player, matching player.Owner and current user.Player reference. Invalid owner state refuses; no substitute player/client or accepting callback.
- Establish a private actual per-player cooperative gate. The existing public state names become gated properties over private backing values: implicit null/null/default birth remains. This deliberately replaces unsafe raw field writes, while preserving ordinary property assignment syntax for existing callers.
- Add an internal SetMessageBox(ok,cancel,deadline) operation for atomic installation of the actual pair/deadline. Future real producers must use it. Separate public property assignments are individually serialized but do not constitute one atomic multi-field installation.
- Under that same gate, validate owner/current player and blocked map, observe the actual current deadline/pair, and atomically remove both current callbacks before returning the selected delegate. Expired cleanup also occurs on the current state in that critical section. There is no stale snapshot or post-callback blanket cleanup.
- Invoke the claimed delegate outside the gate, passing the same actual user. A throw propagates unchanged but cannot restore the consumed claim. Reentrant/parallel duplicate responses cannot claim that old pair again. A replacement installed by the callback or another cooperating writer remains because the old response performs no later cleanup.
- This is at-most-once in-process callback claiming. It is not crash-safe action execution, durable authorization, inventory/economy atomicity, a journal or proof of complete gameplay. A callback can still fail after partially executing its own unrelated action.
- The wire carries no box ID. A delayed valid Accept1 can consume a currently installed new box; this slice cannot prove response-to-box correlation without client evidence. No fabricated nonce or client schema change is added.
- Reflection/native/noncooperating backing-state changes, current Player/Map replacement and broader player graph mutation do not share this gate. No whole-client concurrency or lifetime guarantee is claimed.

## Expected wire behavior and fixtures
- Keep current StaticReply captured Size and Root truncated callback/input/clock/queue preservation control.
- Keep same-client OK priority, Cancel fallback,11 blocked maps, nonaccepted values and expired-window controls. Replace throw-replay and replacement-erasure requirements with consumed-before-invoke and replacement-retention invariants.
- New controls use actual owned native clients and independently initialized2320-style reply frames. No live OS send. Cover once/retry after throw, reentrant duplicate, callback replacement+throw, cooperating parallel duplicates, installation while a callback is blocked, expired-cleanup/installation race and invalid physical ownership.
- Ordinary malformed/nonaccepted/blocked/invalid-owner responses retain callbacks/deadline/queue as applicable. Callback throwing preserves its exception, already consumed authorization and any newly installed replacement.

## Evidence alignment
| Requirement | Existing source/control | Status |
| --- | --- | --- |
| Invoke before clearing permits replay on throw/reentrancy | Handler currently invokes delegates then nulls slots; StaticResponse throw control requires retained callbacks | Confirmed defect |
| Post-invoke clearing erases new state | StaticResponse replacement control requires replacement cleared | Confirmed defect |
| Existing successful framing/admission | GetStaticMessage and StaticResponse positive/blocked/timewindow controls | Preserve |
| Production callback installer | No production writer outside assigned2files in current source | Missing lifecycle producer; expose real installation operation, do not claim live integration |
| Durable execution/cross-box response correlation | No journal or box ID in supplied contract | Unproved, explicitly excluded |

## Implementation and qualification
- No receiver registration, loader/live configuration or original data change.
- Helper authors source/tests/spec/FSB214 fragment only. No build/test/restore/dotnet/native/client/process/SQL/Git execution or actual checkout mutation.
- Root alone reviews/integrates and qualifies after the current70-path batch. Candidate tests are unbuilt/unrun, and no fixed/live/full-parity claim follows.
