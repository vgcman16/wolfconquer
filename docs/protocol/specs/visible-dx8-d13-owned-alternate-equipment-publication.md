# Owned alternate-equipment publication

## Identity and scope
Revision1, 2026-10-30. Target VisibleDx8D13 and the real ordinary Store/native
binding. The new Rune alternate-provider positive exposed an existing split:
the managed toggle changed WorldSession.AlternateEquipmentActive while the real
native Equipment.Alternante remained false. Native Rune.WornObjects therefore
selected the wrong set and the valid alternate cast was refused.

## Packet contract
Existing MsgItemUsage Alternante/UnAlternante requests and peer spawn sequence
remain unchanged. Original MsgItemUsuagePacket.cs2280..2330 flips the actual
Equipment.Alternante before equipment query/view publication. This correction
joins that scalar to the existing managed Store publication; it does not claim
the complete original QueryEquipment/Ninja alternate lifecycle.

## Catalog/table contract
No new table. Use the actual binding's owned equipment object, never a newly
constructed same-client replacement. Existing item/catalog graph checks remain.

## Validation and transaction rules
The actual prepared Store publication captures expected and proposed alternate
values from the real current session. Require its native equipment identity and
old flag to match before any writer, repeat the plain proof at final publication,
then assign the new native flag in the existing native/holder publication block.
No callback, setter side effects, detached accepting owner or new durable format
is introduced. The mode remains session state under the existing persistence
contract. Missing/replaced/drifted native ownership refuses before publication;
existing committed-uncertainty fencing remains unchanged.

## Expected wire behavior and fixtures
Retain the real normal/alternate IronShield cast and exact resource assertions.
Add an actual Frame toggle roundtrip proving the same native equipment owner,
managed/native mode and native worn-Rune selection agree with all saved files
unchanged. Add a real bound Store native-flag-drift refusal with exact session,
files, output and recovery state preservation. Strengthen the existing alternate
cast positive to assert the mode join before casting.

## Evidence alignment
Current handler: WorldPacketHandlers.ItemUsage.cs alternate branch. Actual native
field: LegacyNativeEquipItemStorage.Alternante. Actual consumer:
LegacyNativeRuneItemStorage.WornObjects. Source authority and current failing
alternate cast are separate from live-client acceptance.

## Implementation and qualification
Root-owned correction in the isolated Rune integration candidate. Tests UNRUN;
no original client/server/session, route, save or AV configuration changed.
