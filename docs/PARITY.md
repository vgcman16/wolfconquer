# Reconstruction progress and parity

The working estimate is **about 55% toward strict legacy-server parity**, with a
deliberately broad **40-70% planning range and low confidence**. This is an
engineering judgment, not measured coverage, a release score or a completion
promise. It is not 55% of a complete English client or of newer-client features.

The estimate comes from the October 19, 2026 native-source audit. It read all 784
C# reference files (260,144 lines), but compared only representative target
methods and their callers, not every target method. Reading the reference is not
implementing it. October 22 review retains that estimate: subsequent item, native
loader and tooling increments do not justify inventing a new project percentage.
There has not been another exhaustive source-to-target equivalence audit.

This is a project-level planning view, including documented local work. The
sanitized public snapshot can lag local implementation. It does not imply that
every local feature is in this checkout or that this commit has been deployed.

[![Animated planning tracker](parity/progress.gif)](parity/PROGRESS.md)

[Static image](parity/progress.png) / [Current domain ledger](parity/PROGRESS.md)
/ [Machine-readable assessment](parity/status.json)

## What the bar means

The fill stays fixed at the reviewed estimate. Only the highlight animates; it is
not a live job or a claim that development is progressing while you watch. The
range is a subjective scenario envelope, not a statistical confidence interval.
No file/line count, passing-test count or green badge calculates the percentage.

The main remaining work is shared ownership and complete load/save integration;
trade item/money transactions; guild/union actions; event/reward/reset lifecycles;
housing; cross-server and casino workflows; and exact combat/death caller/order
semantics. Existing substantial implementations are credited, while facade
handlers and unbound components are not treated as completed systems.

Full English localization, newer-client-only behavior, current-commit live
acceptance, SQL/restart/failure scenarios and broad multiplayer testing remain
separate acceptance work. A limited earlier local login/walk/pickup/save milestone
does not prove these. Intentional security/economy differences must be documented;
parity is not permission to reproduce exploits or embedded credentials.

## Keeping it current

`docs/parity/status.json` is the reviewed source of truth. A gameplay change should
update the affected domain and its remaining work. Move the estimate only after
an evidence-backed reassessment; update `estimate.asOf` then. `reviewedAt` records
the latest ledger review, not a fresh whole-source audit. Ordinary docs, tooling
or test-count changes can leave the estimate unchanged.

The **Parity tracker** GitHub workflow runs after pushes to this repository's
`main` and can also be requested manually. It validates the ledger, runs renderer
tests and refreshes only the generated GIF, static PNG and Markdown ledger. It
records the triggering source commit separately from the assessment dates. A
generated-only follow-up commit is automatic, does not recursively re-trigger
itself, and never edits the assessment to raise its percentage.

The card is an ordinary animated GIF supported by
[GitHub's image rendering](https://docs.github.com/en/repositories/working-with-files/using-files/working-with-non-code-files).
The publication uses the repository token's documented
[non-recursive push behavior](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow).

This documentation-only job uses a GitHub-hosted runner, not the Windows game PC.
It cannot build, launch, deploy, log in to or restart the game. The existing
Windows checks remain trusted-owner/manual only. No outside pull-request code is
executed on the local PC.

To reproduce locally with Python 3.12+:

```powershell
python -m pip install -r tools/parity/requirements.txt
python -m unittest discover -s tools/parity/tests
python tools/parity/generate.py --root . --commit <source-commit-sha>
python tools/parity/generate.py --root . --commit <source-commit-sha> --check
```

The renderer has no Git, network or runtime-control behavior. Generation never
changes the assessment JSON. The workflow is the only automatic publication
step, and a stale/conflicting branch refuses a normal push rather than force
pushing. See [validation](../VALIDATION.md) for actual test evidence and limits.
