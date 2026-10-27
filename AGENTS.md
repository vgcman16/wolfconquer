# Project engineering rules

Effective 2026-10-23: **Sanitized Parity** replaces strict copying of emulator behavior.
Read [the engineering policy](docs/engineering/SANITIZED-PARITY.md) before changes.
Read [the protocol workflow](docs/engineering/PROTOCOL-WORKFLOW.md) for feature, packet and catalog work.

- Preserve client-confirmed packet IDs, layouts, schemas, encoding and successful
  protocol sequences. Record the target client/profile and evidence. Decompiled
  community-server code is a reference, not authority over the client.
- Fix unsafe server logic instead of enforcing it in tests. Rejected operations
  must not debit resources, consume authorization, publish success or leave dirty outputs.
- Validate ownership, permissions, bounds, cooldowns, funds and capacity before
  committing. Use real transactional/idempotent mechanisms for inventory, trade,
  rewards and persistence; a local lock alone does not prove crash-safe atomicity.
- Fail safely on invalid/missing required state. Do not invent placeholder owners,
  accepting callbacks, fake queues or partial implementations to obtain green tests.
- Audit earlier bug-preserving tests; replace them with safe invariants while
  retaining meaningful coverage and valid protocol goldens.
- Record every corrected source bug and verification limits in
  [FIXED-SOURCE-BUGS.md](FIXED-SOURCE-BUGS.md). Open entries are not fixed claims.
- Address actionable CodeFactor findings. Do not game grades with blanket
  exclusions, suppressions or reduced tests. Keep methods/modules focused; no new monoliths.
- Helpers may author in assigned isolated scopes. The coordinating agent reviews,
  integrates and verifies; only it stages, commits or pushes final work.
- Preserve unrelated edits, original data, known-good client/server backups and
  running sessions. Use owned temporary fixtures for offline testing.
- Green tests/static grades are not live-client proof, deployment, full parity or
  proof that every bug is gone. Keep the current checklist honest.
