# Connection lifetime and SQL recovery qualification

Reviewed on 2026-11-03. The internal handoff now refuses closed, occupied,
foreign-Frame and initialized-native connections before ticket consumption.
Only actual successful redemption binds the connection against another enter;
later load/send failure cannot authorize a second ticket or identity switch.
The private deny-only marker grants no SQL/native publication authority.

| Check | Actual result |
| --- | --- |
| First affected run `20261103T164109595Z-82a43f90` |382 passed, 2 failed, 0 skipped; all13 new lifetime rows passed |
| Repaired controls/neighbors `20261103T170628834Z-d3c61f93` |19 passed, 0 failed/skipped; build88.218s/test13.241s |
| Fresh owned MariaDB12.2.2 on34389 |44 unique provider Facts passed, 0 failed/skipped |
| Exact lost-COMMIT-ACK case |Passed in0.6703224s; actual server success withheld, real provider unknown outcome, cold read and same-operation replay |
| Named-UI formatter |45 Python controls previously passed; unchanged during later wording composition |

The two initial failures were earlier tests expecting MissingSnapshot output on
an EOF or foreign-owned connection. They retain those real setups and now assert
the actual signed ticket remains identical/unredeemed, no output/session/permit,
same native/player with UID0, complete files and actual UID high-water/generation
unchanged, including a later refused creation request. Production was not relaxed.
The xUnit2031 cleanup preserves the exact predicate and Single assertion; the
warning is absent from the repaired build. No tests were removed or skipped.

Root inspected actual TRX rows and the SQL result. All44 names were unique and
the real ACK-loss test was present. ChildTreeSettled/CommandsSettled were true;
data/private options were absent,34389 had no listener, and primary/cleanup
exception lists were empty. Original databases/services and saves were untouched.

The earlier [public c476 gate](FULL-WINDOWS-20261103-C476.md) passed all eight
stages but did not contain these new changes. A new exact-public-source Windows
gate remains required for this batch. These component/provider results are not
a native SQL backend migration, complete7937 actions, power-loss safety, host
trust clearance, client rendering or live acceptance.
