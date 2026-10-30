# Read-only public game heartbeat

Schema1, 2026-10-30. Optional local status for Warden; no game mutation or new listener.
Absent `WOLFCONQUER_PUBLIC_STATUS_FILE` disables it without file/worker creation.
Enabled configuration requires an absolute local path named `live-game-status.json`,
`WOLFCONQUER_PUBLIC_STATUS_INSTANCE_ID` (nonzero GUID D format), and
`WOLFCONQUER_PUBLIC_STATUS_ENVIRONMENT` (`development`, `test`, or `production`).
`WOLFCONQUER_PUBLIC_STATUS_CLIENT_BUILD` is an optional configured target label,
not an observation of a connected client's patch. Labels are bounded ASCII tokens.

The bounded JSON fields are `schemaVersion:1`, `source:"WolfConquer.WorldServer"`,
`instanceId`, `capturedAtUtc`, `startedAtUtc`, `uptimeSeconds`, `onlinePlayers`,
`profile`, `clientBuild`, `environment`, and `state`. UTC timestamps use ISO8601.
Start is null and uptime0 before genuine TCP listen; uptime uses the monotonic
listener clock. Count can be null when unavailable. States: `online`, `starting`,
`recovery_required`, `stopping`, `offline`, `unavailable`. Online means actual
listener acceptance plus completed/unblocked recovery and a current admitted-holder
count. It does not prove gameplay parity, client rendering, SQL health or host trust.

The typed source verifies the actual TCP server owns the exact Frame handler
delegate. Listening/start/shutdown/failure observations originate in that TCP owner.
Frame takes its existing Shared then Trades read scope, reads current Store holders
excluding ended connections, and observes initialized/blocked/pending Recovery.
It never initializes recovery, replays files, constructs sessions or grants authority.
Bound holders require their actual Store/native/player/snapshot identity; malformed
owners refuse the count. Unbound managed holders require both native pointers absent.
No account/player names, IDs, endpoints, paths, credentials or exception text enter JSON.

A single actual file-writer lease fences cooperating exporters. Every5seconds,
prepare a bounded DTO and release Frame locks before file IO. Write a fresh same-directory
temporary file and replace the configured JSON atomically. Refuse reparse/remote/device
paths. Failed IO leaves old data to expire; it never generates a healthy fallback.
Shutdown stops polling, awaits real TCP disposal, then exports its actual terminal
state. A stopped/offline assertion cannot come from exporter disposal alone.

On Windows, replacement uses `FileRenameInfoEx` with replace-existing and POSIX
semantics on the completed temporary file's actual handle. Held readers keep the
old complete image; fresh opens see the new complete image. This avoids the
observed `File.Move(overwrite:true)` access-denied failure with concurrent readers.
Unsupported filesystems or flags report unavailable and leave the prior snapshot
to expire. There is no delete-first gap or retry that claims a successful write.
The contract is documented in Microsoft's [FILE_RENAME_INFORMATION reference](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information).
Publisher diagnostics include only exception type and numeric HRESULT, not paths
or exception text. The owned tests exercise concurrent readers and a held old
handle across a genuine TCP listener start, without inventing an online state.

Bot consumer requires its configured matching instance and at most30seconds age,
rejects malformed/future input, and renders unavailable rather than CI/sample data.
The local-file lease/rename is not distributed authority or power-loss durability.
Raw noncooperating native writers remain outside cooperative read consistency.
Owned source tests and Root qualification are separate from deployment/live acceptance.
