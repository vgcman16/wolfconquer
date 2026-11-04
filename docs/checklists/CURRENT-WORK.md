# Current reconstruction checklist

Updated 2026-11-04 UTC. The full English client/server goal remains active.
Root alone reviews, integrates, qualifies, stages, commits and pushes.

## Verified published baseline

- [x] Exact public `f88da998` completed all eight Windows stages: **35,758 passed,
  0 failed, 271 skipped**. All 664 Chinese7937-named rows passed. The skips are
  119 opt-in SQL and 152 external-fixture cases, not executed passes.
  [Full result](FULL-WINDOWS-20261104-F88D.md).
- [x] Earlier SQL authentication/handoff source passed 124 ordinary tests and all
  119 actual provider Facts before that full gate. True account and character IDs
  remain distinct. [Handoff results](SQL-AUTHENTICATED-HANDOFF-20261103.md).
- [x] New-client catalog/codecs/Echo focused checks passed 836/836. All 57,093 main
  and 1,693 sub-item rows parse/roundtrip 67 structural fields; complete meanings
  and runtime mapping remain open. [7937 status](7937-STATUS-20261103.md).

## Reviewed successor: component/provider qualified

- [x] Real SQL-to-connection lifetime retention, file/GM fallback fencing,
  disposal-only failed-resource ownership and strong TCP cleanup/retry handling.
- [x] Corrected all six initial affected failures: five shared a half-close test
  fixture defect; one exposed delayed EOF denial before awaited SQL cleanup.
  The corrected completed run passed 102 cases, including all 27 new ordinary
  cleanup cases and 58 GM cases. Its long history test then passed separately
  with all 129 real logins and an appropriate watchdog deadline.
- [x] Complete fresh owned SQL family: **134 passed, 0 failed/skipped**; all source
  names matched actual results. Actual commands/children settled, listener and
  private database/options removed, no primary/cleanup errors.
  [Results and retained failures](SQL-CONNECTION-CUSTODY-20261103.md).
- [x] Named-UI text tools: isolated expanded 84 Python controls passed; Root also
  rechecked all 18 new final-source variant API/CLI controls. Old uniform-route
  checks remain. Differently formatted duplicate labels retain their own values.
- [x] Actual private English proposal: **4,748 keys / 4,814 physical rows**.
  Prior/unselected bytes and formatting are preserved. Separate UStrRes proposal:
  1,191 keys. Resources are unapplied/unrendered; no whole-client percentage.
  [English results](NAMED-UI-ENGLISH-20261103.md).
- [ ] Publish the reviewed source/tests/docs and verify its single full Windows
  integration gate. Earlier baseline passes do not qualify the new full source.

## Next work and remaining end-to-end gaps

- [ ] Review and qualify the separately authored real SQL admission/close
  generation transaction and its ACK-loss tests. Connect exact SQL identity,
  revision, Online, normalized images, recovery, real native/Store publication,
  UID ownership and every reachable gameplay writer. Native gameplay currently
  saves files; no shadow file fallback or metadata grant is acceptable.
- [ ] Complete the matching account/world wire bridge, actual session/profile
  activation, catalog semantics/runtime adapters, Echo actions/costs/saving and
  opcode2350 item producers/actions. [7937 detail](7937-STATUS-20261103.md).
- [ ] Continue gameplay/social/events, actionable static defects, remaining
  English resources and distribution. Profile the measured admission cost
  before removing work; retain all real lifetime/history safety coverage.
- [ ] After the execution hold clears, verify target-client login, gear/mail,
  rendering and stability; migrate one local endpoint at a time with a login
  check and single-file rollback after each change.

Preserve original sessions, saves, routes, backups and restore ZIP. The legacy
client/updater/native-inspection execution hold remains; no antivirus changes.
Green component tests are not deployment, full parity or live-client acceptance.
