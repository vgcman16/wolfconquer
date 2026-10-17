# Publication scope

This repository starts from a separate export of the accepted source checkpoint `74f432f6a3c8c12fdfabf7f605da33f27c2316ed`, followed by publication-specific edits. The original repository history, local working changes, and operational records are not part of this repository. The checkpoint identifier is provenance, not a commit in this new history.

The export includes the C# source and project files under `src/` and `tests/`, the solution, and SDK selection. It excludes game/client binaries and assets, database exports, capture files, deployment configuration, package caches, build outputs, private operational documentation, and the original root launch/repair/capture scripts.

The publication edits remove embedded client-text catalogs and captured HTTP response bodies. Client patching and loader responses require explicitly supplied content. Missing configuration produces an unavailable result rather than a successful empty operation. Packet tests that used recorded traffic are replaced with constructed fixtures; captured startup-residue replay is omitted. Machine-specific paths and operational endpoints are replaced with portable configuration or synthetic examples.

Tests dedicated to the excluded launch/repair/capture scripts are also excluded. One test tied to a private live equipment snapshot is omitted. A further 165 test methods require omitted external seed data and now have explicit skip reasons, documented in [external fixture coverage](docs/EXTERNAL-FIXTURE-TESTS.md); 150 of these previously returned without assertions when those files were missing. These changes alter the test inventory. Results from the original checkpoint do not establish that this edited snapshot passes; only the checks recorded in [VALIDATION.md](VALIDATION.md) apply to this repository.

No game content, production credentials, or original private history should be added to this repository. Keep externally supplied client files, loader payloads, database data, and local configuration outside the source tree or in ignored directories.
