# Publication scope

This repository starts from a separate export of the accepted source checkpoint `74f432f6a3c8c12fdfabf7f605da33f27c2316ed`, followed by publication-specific edits. The original repository history, local working changes, and operational records are not part of this repository. The checkpoint identifier is provenance, not a commit in this new history.

The export includes the C# source and project files under `src/` and `tests/`, the solution, and SDK selection. It excludes game/client binaries and assets, database exports, capture files, deployment configuration, package caches, build outputs, private operational documentation, and the original root launch/repair/capture scripts.

The 2026-10-22 update imports later source/test changes through the verified private
checkpoint `bb91474c459c432612a7ea70f6ab4d934736caa5`, with the same publication
exclusions and a new validation run. It does not import that checkpoint's private
history, deployment state, or later unverified working files. Native-style loaders,
player state, equipment, spell, packet, and transport owners remain development
work; their presence is not full login, database, host-integration, or game parity.

The publication edits remove embedded client-text catalogs and captured HTTP response bodies. Client patching and loader responses require explicitly supplied content. Missing configuration produces an unavailable result rather than a successful empty operation. Packet tests that used recorded traffic are replaced with constructed fixtures; captured startup-residue replay is omitted. Machine-specific paths and operational endpoints are replaced with portable configuration or synthetic examples.

Tests dedicated to the excluded launch/repair/capture scripts are also excluded. One test tied to a private live equipment snapshot is omitted. A further 165 test methods require omitted external seed data and now have explicit skip reasons, documented in [external fixture coverage](docs/EXTERNAL-FIXTURE-TESTS.md); 150 of these previously returned without assertions when those files were missing. These changes alter the test inventory. Results from the original checkpoint do not establish that this edited snapshot passes; only the checks recorded in [VALIDATION.md](VALIDATION.md) apply to this repository.

The current update has 166 explicitly skipped external-fixture methods; the initial export had 165. See the external-fixture document for the added prayer receiver theory.

No game content, production credentials, or original private history should be added to this repository. Keep externally supplied client files, loader payloads, database data, and local configuration outside the source tree or in ignored directories.

Additional public-only differences: original deployment keys/promotional endpoints
are empty configuration fields; the legacy Execute2 database path no longer
overrides caller configuration with embedded credentials and rejects missing
configuration before constructing a provider connection. The legacy provider is
restored from NuGet rather than an excluded private DLL. These deliberate export
differences must not be represented as exact original-runtime behavior.

Existing compatibility cipher constants remain part of the published wire codec;
they are not account credentials or a security boundary. Clearing the new native
configuration fields does not remove those pre-existing protocol constants.
