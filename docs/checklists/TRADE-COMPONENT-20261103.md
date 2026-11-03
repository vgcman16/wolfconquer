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
That corrected case remains unrun at this source checkpoint. The historical
521/1 outcome stays failed; subsequent targeted evidence must be reported separately.

All14 real SQL Facts and the appropriate complete gate remain required. No actual
client/updater/native-inspection launch or live7937 proof follows. Original data,
routes, sessions and backups remain unchanged.
