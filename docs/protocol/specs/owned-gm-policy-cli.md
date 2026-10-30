# GM policy creation tool

2026-10-30. Offline operator tooling for the explicit GM policy; not a packet or
new catalog schema. Uses the existing owned-admission configuration and saved
Character contract. Actual client command activation remains VisibleDx8D13 and
requires the real owned Frame/runtime, PIN and policy checks in the GM spec.

```
WolfConquer.DataTool create-gm-policy <NEW-absolute-policy.json> <existing-owned-admission.json> <accountId> <characterUid> [inspect|mail|both|catalog|all]
```

The optional selection defaults to both, preserving exactly inspect.self and
mail.testkit.self. inspect and mail retain their original single capabilities.
Explicit catalog grants only mail.catalogitem.self; explicit all grants all three.
Every selection is an exact lowercase closed value. Existing policies and the
default are not widened. Account and character are explicit positive uint decimal values;
names, prefixes, Discord state and saved Role attributes do not grant anything.
The tool consumes the actual strict LegacyOwnedAdmissionOptions.TryReadFile
configuration and its nonzero ExpectedDomain. It neither bootstraps nor opens a
UID registry, infers a domain from a catalog nor contacts a running server.

It requires the configured Users/<accountId>.ini ordinary local file, reads at
most16MiB, preserves UTF8/BOM or existing Windows1252 decoding, rejects duplicate
Character sections/keys and parses the complete CharacterSnapshotParser schema.
The actual saved UID must match the requested character. No LoadByAccountId call,
nobility expiry or file save occurs. The configuration value and selected saved
character bytes are compared again before output creation.

Output must be a new absolute local .json file under existing ordinary directories.
Existing files/directories, network paths, alternate data streams and observed
reparse ancestry refuse. The tool serializes UTF8 without BOM, self-validates with
LegacyGmPolicy.Parse, then creates using FileMode.CreateNew, FileShare.None, flushes
to disk and compares the exact readback from its handle. Pre/post path checks
reject observed reparse changes; they do not attest host trust or exclude every
uncooperative path/restore ABA race. A write failure may retain an incomplete
inert output; no automatic delete, overwrite, retry or live activation occurs.

Success writes a local JSON report with policyCreated=true and activated=false.
Review the resulting file before configuring WOLFCONQUER_GM_POLICY_FILE in a
separately owned staged server. This command does not edit environment settings,
restart a server, modify saved characters, grant a running connection or execute
any legacy client. Do not treat policy creation as live-client qualification.

Implementation: src/WolfConquer.DataTool/Program.GmPolicy.cs, the three registered
route/help hunks in Program.cs, and tests/WolfConquer.Tests/LegacyGmPolicyDataToolTests.cs.
The authored tests invoke the actual built DataTool process with isolated
bootstrap:false fixtures. They cover capability choices, retained existing output,
invalid IDs, mismatched UID, absent/malformed/ambiguous/oversized saved state,
output constraints and actual help routing. No executions were performed by the
author; Root owns build, regression results and publication.
