# Client resource proposal tool

This source-only tool compares three explicit, bounded ordinary flat numeric resource files: original Chinese, its translated English, and current Chinese. It produces a proposal only. It has no apply, launch, routing or client-destination command. Python 3.10+ and the standard library are sufficient.

```powershell
python tools/client_resources/propose.py original.ini english.ini current.ini --write-proposal
```

Without `--write-proposal`, it prints a report and writes no files. With it, the tool creates a fresh random child under `tools/client_resources/proposals/`, containing only `UStrRes.ini` and `merge-report.json`. Install/run the tool from an owned workspace, never a client directory. Paths and their ancestors must be ordinary, not symbolic links or reparse points. Inputs are capped at 1 MiB. UTF-16/UTF-8 BOM is detected; for a BOM-less input supply its individual `--original-encoding`, `--english-encoding`, or `--current-encoding` (`utf-8`, `utf-16-le`, `utf-16-be`, `cp936`). No encoding is guessed.

A value is imported only when original and current Chinese match exactly, the English differs, the complete ordered format signature matches, and the value encodes in the target. Numeric key spelling, order, comments, whitespace, BOM and line endings come from current. Duplicate IDs, row injection, malformed syntax and unrepresentable text are refused or held. Percentage and navigation-arrow exceptions are narrowly scoped to the evidenced keys; unknown syntax remains held. A report contains hashes, keys and counts, never input values.

Key 100077 is an observed machine lookup literal and is always held unchanged,
including in automatic proposals without a reviewed-exceptions file. It is not
a visible string to translate.

Optional `--reviewed path.json` enables the two explicit reviewed 7937 exceptions, branding key 10375 and changed donation key 11090, together. The bounded UTF-8 JSON must have `schemaVersion: 1`, `targetBeforeSha256` of the entire actual current input, and exactly two `entries`, each with numeric `key`, `expectedValueSha256` (SHA-256 of its exact current value in UTF-8, uppercase hex), `english`, and a nonempty `reason`. Supply reviewed values privately; none are embedded here. Each entry must still be unchanged after automatic merge, match the current value hash and exact current formatting signature, fit the target encoding, and contain no injected line. Thus the donation value must preserve its actual two `%I64u` tokens; old English with `%d` is rejected. The machine lookup literal 100077 must remain unchanged in reviewed completion. This file is a user-provided review record, not authenticated authority or translation proof.

Synthetic controls (no client resources) can be run from the repository root with `python -m unittest discover -s tests/client_resources -p "test_*.py"`. The original qualified private reference controls are separate; this public bundle includes no private tables or reviewed values. A successful proposal does not prove translations, client rendering, protocol compatibility or permission to replace a client file. Input path checks are not protection against hostile concurrent writers.
