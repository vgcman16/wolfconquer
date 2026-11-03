# Trade component qualification

Sourcebd49e854c302c52cca4c798d175feb6704298a93. Root ran the reviewed Affected
filter over trade, owned eligibility, startup trade/vendor recovery and tool
neighbors using Invoke-FocusedCheck.ps1. This is not the full publication gate.
Run20261103T025707466Z-43900952 started02:57:08UTC and ended03:09:02UTC.

Build passed168.643s. Tests ran544.752s:521passed,1failed,0skipped of522 actual
TRX rows. Root inspected the failed row and these actual class totals:

| Class | Passed | Failed |
| --- | ---: | ---: |
| LegacyTradeItemMetadataTests |24|0|
| OwnedTradeEligibilityTests |22|0|
| LegacyTradeRuntimeTests |100|0|
| LegacyOwnedStartupTradeRecoveryTests |107|1|
| LegacyOwnedStartupVendorRecoveryTests |90|0|
| LocalDevelopmentToolsTests |16|0|
| SqlGameplayCheckToolsTests |6|0|

Other selected trade neighbors passed too; the table is a subset of522 rows.
The only failure was CapturedPairUidCannotBeBorrowedByAnUnrelatedPhysicalOwnerWhenTargetIsAbsent.
Its setup called the normal writer while a real configured trade journal was
pending. Production correctly threw pendingConfiguredJournalWrite before the
intended cold collision assertion. This is not a failed collision assertion.

The narrow fixture repair first asserts the real write refusal and all-file
immutability. It then writes only a create-new external/corrupt physical image
in its owned temporary directory and retains actual startup refusal. A positive
counterfactual changes only that foreign UID at the same path to a demonstrably
unused value below the real historical highwater, then requires real recovery,
fresh native Frame admission and cold reopening. No production file changed.
The corrected case subsequently passed1/1,0failed/skipped, on cleanfb0c5009 in
run20261103T032100937Z-e248970c: build164.230s/test15.416s; actual case5.703s.
The historical521/1 outcome stays failed. No shared fixture or production file
changed in that retry, so it does not require repeating the unaffected521 rows.

All14 real SQL Facts then passed on cleanfb0c5009 against fresh owned MariaDB;
[actual result](OWNED-SQL-AUTH-20261103.md). The complete gate remains required. No actual
client/updater/native-inspection launch or live7937 proof follows. Original data,
routes, sessions and backups remain unchanged.
