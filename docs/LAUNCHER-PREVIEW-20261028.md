# Windows launcher and updater preview: 2026-10-28

[Download the tooling preview](https://github.com/vgcman16/wolfconquer/releases/tag/launcher-preview-20261028). This contains the WolfConquer launcher
and updater tools only. No full game client, bootstrap configuration, signing key
or operational client feed is included. Chinese client7899 / cached DX8 D13 is
the current target; English work and gameplay parity remain incomplete.

## Package

- [Windows x64 ZIP](https://github.com/vgcman16/wolfconquer/releases/download/launcher-preview-20261028/WolfConquer-Launcher-Tools-20261028.zip): 3,146,358 bytes.
- [Checksum](https://github.com/vgcman16/wolfconquer/releases/download/launcher-preview-20261028/SHA256SUMS.txt) and [instructions](https://github.com/vgcman16/wolfconquer/releases/download/launcher-preview-20261028/README-DOWNLOAD.txt).
- Exactly nine runtime files, including both tools and their shared update engine.
  No PDBs, account data or game assets. The ZIP is unsigned; a checksum is a byte
  comparison aid, not a digital signature.
- Windows x64 and .NET9 Desktop Runtime x64 are required; runtime is not included.
  Runtime metadata specifies9.0.0 minimum; use an appropriately patched runtime.

Extract the whole ZIP into a new tools folder and keep the nine files together.
Open `WolfConquer.Launcher.exe`. Opening the launcher does not update or launch a
game. The Source page accepts an explicit clean checkout; local changes cause a
refusal rather than a reset. Client actions require separately supplied trusted
configuration and a curated signed release.

## Actual verification

The package was rebuilt from exact public source
`224f1eb5dfc1ed148ccd53252f833339b5622f7c`, whose
[full Windows check](https://github.com/vgcman16/wolfconquer/actions/runs/40646084998)
passed. These binaries are newly rebuilt outputs, not CI-produced artifacts.

The launcher-only publish succeeded. Independent ZIP checks matched the exact
nine names, every entry's bytes, archive length and SHA256. Extracted updater CLI
help exited0. Both Client and Source XAML preview renders completed and were
visually inspected. An independent second review confirmed ZIP contents and
release wording; it did not establish source-to-binary reproducibility.

The ZIP and checksum were then downloaded through public HTTPS without tokens,
cookies or authentication and matched the local bytes. SHA256:
`D14A54E8C3E8D84DBE4F64765C37E8E51E92332E188DD89299F898FCD1EF9AAA`.
The release tag points to the qualified source above.

[Client preview](https://github.com/vgcman16/wolfconquer/releases/download/launcher-preview-20261028/client-preview.png) and
[Source preview](https://github.com/vgcman16/wolfconquer/releases/download/launcher-preview-20261028/source-preview.png) show the tools' own UI with explicit
visual-preview banners; sample progress bars are not download results.

These checks do not prove a fresh game installation, network update/recovery,
client login, latest7935 runtime support or live gameplay. Portable configuration,
a sanitized full-client package, real signed client feed and distribution
acceptance remain open. The complete-distribution forum milestone stays held.
