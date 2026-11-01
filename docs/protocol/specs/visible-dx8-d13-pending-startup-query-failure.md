# Pending startup query failure ownership

Status: OPEN source implementation; unbuilt and unrun by the author.

A Store observer can throw after native/holder publication but before the
private query execution adopts the returned holder. Failure handling cannot
assume its last local session reference is the last publication. It also cannot
turn an arbitrary refreshed holder into continuation permission.

The prepared admission query records only its actual privately registered
invocation. Cleanup requires that the consumed plan's invocation has bound a
real private execution. `FenceFailure` retains the original binding, client,
player, native identity, Frame and login operation. Under lifecycle, package,
connection, native and actor gates, it blocks recovery and clears only the
still-same binding. It does not validate or grant gameplay on a new holder, and
it never clears a replacement binding. A retired or replaced owner is left
alone. Calls already inside a connection or actor gate do not reverse the lock
order. The original exception is preserved; cleanup exceptions are aggregated.

The actual completed startup receipt delegates cleanup to that same original
invocation. This covers a second-query preparation failure before the successor
invocation starts without refreshing the old admission origin. Failures before
any real invocation binds remain the original admission owner's responsibility.

This authority is cleanup only. It cannot issue an XP credit, select a resumed
session, accept a cache or claim an output completed. Genuine earned-pending
tests must cover publication-before-observer-throw, output failure, original
binding retirement and replacement-binding preservation. Those integrated
controls remain unrun and are required before qualification; no synthetic
positive origin or completed receipt is a substitute.
