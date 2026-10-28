# Published outcomes in spell transactions

Free spell writes, paid Character+Spells transactions, and item/rune
Character+Items+Spells transactions use the same Store publication boundary.
A `WorldSessionPublicationException` means the session was published before
the observer or Frame postlude failed; it is not a preparation refusal. Each
path blocks the captured actual spell owner's Recovery and rethrows the same
typed outcome. A secondary Recovery-block failure is retained on that outcome.
The preparation classifier also excludes this exception independently. Item
transactions still dispose their prepared rank lease in `finally`, and no
success frames are delivered after the exception.

Existing precommit refusal behavior and committed-spell recovery holds remain.
The persisted single/pair Store APIs deliberately do not repeat the ordinary
callback: their writer already persists every changed field. The current paid,
free and v9 book controls arm that ordinary observer and prove it is not repeated,
then check the actual durable/current images and applicable success packets.
They do not instrument a separate Frame saver or exercise the defensive typed
rethrows. Actual notification-failure coverage belongs to the Store publication
and direct NPC paths that invoke those phases. No live-client or full integration
claim follows from these isolated controls.
