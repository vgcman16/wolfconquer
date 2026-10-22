# Security notes

This development snapshot is not hardened for public production use. Do not expose
test services, dashboards, or database ports to the Internet as a setup shortcut.

Keep the dashboard on loopback and runner credentials outside Git. The Windows
runner executes code with its account's permissions; it is not a sandbox. Do not
enable untrusted pull-request execution or share private game/server data with jobs.

Never post credentials, account/save exports, or sensitive logs in public issues.
Agree on a private channel with the repository owner before sharing sensitive
vulnerability details. No private reporting service or response-time guarantee is
claimed by this file.
