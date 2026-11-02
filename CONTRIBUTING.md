# Contributing

This is source-available work in progress, not a complete or production-ready emulator.
Read [publication scope](PUBLICATION-SCOPE.md) before contributing content.
Follow [Sanitized Parity](docs/engineering/SANITIZED-PARITY.md): preserve confirmed
client protocol compatibility and fix unsafe emulator behavior, including tests
that previously enforced source bugs. Record repairs in [the bug tracker](FIXED-SOURCE-BUGS.md).

- Keep changes modular; do not add another monolithic handler.
- Explain behavior/source evidence and add constructed tests for changes.
- Use [focused development checks](docs/engineering/TEST-WORKFLOW.md) for a
  change and its dependency neighbors; run the full Windows gate once per
  reviewed integration/publication batch.
- Report actual failures, warnings, skipped coverage and the exact checked scope.
- Never commit credentials, game assets, database exports, captures, or personal/local operational logs.
- Review outside contributions before explicitly requesting checks on trusted main.

Bug reports should include the public commit, expected/actual behavior,
reproduction steps, and sanitized diagnostics. Test totals and CodeFactor grades
are not parity percentages. No blanket source license is established here;
dependency licenses do not authorize unrelated source/content redistribution.
