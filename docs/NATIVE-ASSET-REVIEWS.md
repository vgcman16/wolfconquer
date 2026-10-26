# Exact native asset reviews

Local distribution-policy and publication-plan schema 2 supports two narrowly
defined native asset naming collisions. It does not approve any actual game
assets or create a full-client download. See
[client/source update tools](CLIENT-UPDATES.md) for the surrounding workflow.

Schema 2 requires `nativeAssetReviews` with 1–4096 entries. Every entry has exactly
these seven fields; unknown fields, duplicate properties and case aliases refuse.

| Field | Meaning |
| --- | --- |
| `path` | Exact source-relative payload file path, using forward slashes. |
| `collisionPath` | Exact node whose name collides with quarantine. |
| `collisionNodeType` | `directory` or `file`, as required by the category. |
| `category` | One of the two categories below. |
| `length` | Integer byte length, 0–4,294,967,296. |
| `expectedSourceSha256` | Exactly 64 hex characters identifying reviewed source bytes. |
| `evidence` | 1–128 ASCII letters, digits, dots, underscores or hyphens identifying the review evidence. |

- `native-map-object-directory`: the collision is an ordinary directory exactly
  `data/map/mapobj/<one or more segments>/obj`. The payload is an exact descendant
  file with a `.dds`, `.bmp`, `.png`, `.tga` or `.msk` extension. Listed extension
  suffixes are case-insensitive; path spelling is exact.
- `native-logs-bitmap`: the collision is an ordinary file, and both paths are
  exactly `data/main/LogS.BMP`.

Each review admits only its declared collision. Every other risky segment still
refuses, including private/control names, AutoP, backups and another unreviewed
`obj`. This is not a general filename exemption; token/userdata configuration
receives no exception. An extension is not proof of a valid or safe image format.

Inside a reviewed `obj` root, inherited asset-directory approval is disabled.
Every actual file must have its own exact review, and every intermediary directory
must prefix a reviewed file. New files, empty/unreviewed directories, missing
members, wrong types, wrong spelling, unused reviews and conflicting selections
or exclusions refuse. Reparse/device entries and unsafe paths remain forbidden.

The policy bound remains 1 MiB; a publication plan is at most 64 MiB. Existing limits
remain 200,000 payload files, 4 GiB per file, 32 GiB total, 1,024 path characters and 255
characters per segment. Publisher review checks also bound sibling enumeration
and membership traversal. A bounded failure requires review; lists are not
silently truncated.

## Source bytes and prior installed bytes

`expectedSourceSha256` binds the reviewed source file; hex digest matching is
case-insensitive. `baseSha256` retains its
existing meaning: the expected prior installed version for patching. They are
independent; a prior-install hash must never substitute for source review.

[New-ClientDistributionPlan.ps1](../tools/updates/New-ClientDistributionPlan.ps1)
checks names, ordinary node types, complete membership and supplied lengths
without opening or hashing payload contents. It carries supplied review digests;
metadata alone cannot produce or validate a content review.

[New-ClientDistributionStage.ps1](../tools/updates/New-ClientDistributionStage.ps1)
rechecks actual membership and compares source SHA256 under its existing read
handle, then verifies destination readback. Rebasing changes only `sourceRoot`;
review fields and `baseSha256` remain unchanged. The publisher independently
checks reviewed source length/digest before outputs and checks copied digests
again before blob promotion. The discovery feed changes only after all selected
content is ready.

Schema 1 rejects a `nativeAssetReviews` property, including null/empty values,
and retains ordinary names-only source behavior. The original four-argument
`ReleasePublisher.Publish` API remains available; its review-aware overload
accepts explicit typed reviews. These are local publication inputs: signed
manifest schema 1, installed-manifest validation and repoint verification remain
unchanged.

These checks assume an ordinary cooperating filesystem. They do not provide
hostile namespace protection or a multi-file transaction. A failed stage may
leave its identified partial directory without a completed rebased plan. A later
publication failure can leave already promoted immutable blobs while retaining
the prior discovery feed. Keep source files quiescent during review and copying.

## Standalone synthetic checks

Run from the repository root on Windows with PowerShell 7.4 or later:

```powershell
pwsh -NoProfile -File tools/updates/Test-ClientDistributionPlan.ps1
pwsh -NoProfile -File tools/updates/Test-ClientDistributionExclusions.ps1
pwsh -NoProfile -File tools/updates/Test-ClientDistributionStage.ps1
pwsh -NoProfile -File tools/updates/Test-ClientDistributionNativeAssetReviews.ps1
```

These use owned synthetic inputs. Their results do not approve real asset
contents, prove client compatibility, or establish a published full-client feed.
