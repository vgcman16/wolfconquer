# Owned SQL commit acknowledgement loss

This is actual provider qualification, not a new persistence implementation or
a claim that native gameplay uses SQL. Existing real SaveBundleAsync marks
commit outcome unknown when MySqlTransaction.CommitAsync fails after the commit
attempt; the exact operation ID/descriptor must be retained for reconciliation.

Add an owned managed loopback proxy between the real repository and fresh owned
MariaDB. Use uncompressed, nonpooled plaintext only for this loopback fixture;
never change a live connection or log credentials/payloads. Relay bounded MySQL
packets unchanged until the real client sends COM_QUERY COMMIT. Read the actual
server success acknowledgement, withhold that packet and close the client side.
This causes a genuine provider observation failure after a real database commit.

The controls must prove the exact unknown-outcome exception, successful ACK
withheld by the fixture, all six images and UID rows actually committed on an
independent cold direct read, and replay of the SAME request/operation without
another revision or duplicated effects. No fake repository or accepting saver
may substitute for that sequence. Settle all proxy loops and owned connections;
retain unrelated and primary/cleanup errors. Normal SQL tests remain intact.

All44 actual provider Facts passed on fresh owned MariaDB12.2.2,0failed/skipped.
The exact lost-ACK case passed in0.6703224s; Root read its actual TRX row and
the complete owned result/cleanup. Actual success was withheld before the
provider reported uncertainty, then independent reads and same-operation replay
verified six images, UIDs and no duplicate effects. See the
[actual qualification](../checklists/HANDOFF-SQL-RECOVERY-20261103.md).

This is separate from FSB-337's Character-file paid-expiry uncertainty and does
not close that record. It grants no authenticated connection, native publication,
shared UID allocator, full persistence, power-loss or live7937 acceptance claim.

The fixture's narrow framing/command/response assumptions are checked against
[MariaDB packet framing](https://mariadb.com/docs/server/reference/clientserver-protocol/0-packet)
and [COM_QUERY](https://mariadb.com/docs/server/reference/clientserver-protocol/2-text-protocol/com_query).
The [official MySqlConnector transaction source](https://github.com/mysql-net/MySqlConnector/blob/master/src/MySqlConnector/MySqlTransaction.cs)
uses a text commit command. These references describe the fixture protocol;
the actual owned provider run must still prove that the interruption occurred.
