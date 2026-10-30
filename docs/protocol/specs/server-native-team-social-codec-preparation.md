# FSB-221: Team UI and social codec preparation

## Identity and scope

2026-10-29, native source-shaped TeamElite names/match UI, TeamArena score and
KnowPersons/TradePartner codecs. Existing old7899/VisibleDx8D13 source/fixture evidence; no
new7937 schema, dispatcher, permissions, ownership authority or live activation.

## Packet contract

- Team name: caller-selected UInt16 opcode, UInt32 state/teamUID, fixed32 name.
- Match UI: caller-selected opcode; UInt32 state/effect; opcode2270 alone has an
  extra zero word; opponentUID/zero, fixed32 name, eight zero bytes, time/zero.
- TeamArena2190: two UID/rank/fixed32-name/damage rows and final UInt32 one.
- Social2139: UID, byte action/online, reserved UInt16 zero, four UInt32 metadata
  fields, fixed64 signature, UInt32 one and fixed32 name.
- TradePartner2004 retains its asymmetric UInt16 hours write and signed Int32
  hours read, existing metadata order and128-byte frame. No schema repair is guessed.
- Headers/seals and native fixed-string character-count UTF8 byte behavior stay.
  Unknown enum bits remain representable. Social reader online is true only for
  signed byte1, with no new boolean normalization.

## Catalog/table contract

Not applicable. No catalog meanings or gameplay rules change.

## Validation and transaction rules

Required stream/names/signature reject before publication. All five complete
builders use actual FSB209 scratch-frame preparation before any target bytes,
cursor or Size are committed. Team-name and both social getters return local tuples
through ReadPrepared; caller out parameters change only after complete parsing.
Ordinary refusal preserves input/target state and existing out values. No dummy
owners, accepting transports or invented successful acknowledgments are added.
Caller-exclusive packet lifetime remains required; this is not a concurrency,
SQL, crash or transport-delivery transaction.

## Expected wire behavior and fixtures

Keep independent existing successful bytes, caller-selected IDs and2270's extra
word. Replace null-name/signature dirty-prefix controls with complete initialized
target preservation. Add genuine malformed fixed-record/tail controls requiring
whole reader output and cursor preservation; use corrected code only.

## Evidence alignment and qualification

Native-broad02 retained six null-name/signature exception mismatches in the three
affected test families. Current codecs write prefixes before fixed-string faults;
current getters sequentially publish outputs before their final read. These are
source defects, not client-confirmed successful behavior. New source is unbuilt
and unrun until the combined gate; live rendering and whole gameplay remain open.
