# Offline exact-plan client staging

`New-ClientDistributionStage.ps1` consumes an already reviewed, successful JSON
plan from `New-ClientDistributionPlan.ps1`. It copies only that plan's exact file
list into a new ordinary directory and writes a new publisher plan with
`sourceRoot` rebased to the stage. It does not build, run, sign, package, publish,
change the original client, or select extra configuration files.

```powershell
pwsh -NoProfile -File ./tools/updates/New-ClientDistributionStage.ps1 `
  -PlanPath 'D:\review\approved-plan.json' `
  -StageDirectory 'D:\staging\client-new' `
  -OutputPlan 'D:\review\staged-plan.json'
```

Use PowerShell 7.4+ on the same Windows filesystem assumptions as the planner.
The stage must not exist; its parent and the fresh `.json` output-plan parent must
already exist. Original source, stage, and served publication roots must not overlap.
Both plan artifacts stay outside all three roots. Existing files/directories,
UNC/volume aliases, traversal, reparse ancestors, case collisions, unknown JSON
properties, duplicate JSON keys, unsafe paths, and quarantined private/control
names are refused. Unlisted source entries are not copied or recursively swept.

The exact generated schema is required, including both boolean fields and each
entry's `path` and `baseSha256`. File order and `baseSha256` values are retained;
the latter describes a prior install, not the current source. Bounds match the
publisher: 200,000 files, 4 GiB per file, 32 GiB total, and a 64 MiB JSON plan.

The planner is metadata-only: its list is not a historical content digest or a
signature. Current files may legitimately differ from bytes seen during an older
review. Review their contents separately; filename filtering cannot prove the
absence of secrets inside approved files. Staging preflights current metadata,
holds each source read handle against writers/deletion while copying, rejects
observable changes during that operation, flushes each new copy, and compares
its SHA256 against the exact source-stream bytes by destination readback.

No rebased plan is emitted until every copy verifies. A later fault leaves the
clearly identified partial owned staging tree for inspection; incomplete files
may remain. The error reports completed counts and does not recursively delete
anything. Choose a different fresh directory for a new attempt. A successful
`staged-not-published` response is offline copy proof only, not release approval,
client compatibility/rendering proof, or an active download feed.
