# Bounded8140 Rune classification ingestion

## Identity and scope

- Revision1,2026-10-30. Root-reserved draft FSB252. SOURCE/DATA ONLY partial
  candidate seeded solely as needed from accepted Root130. Root alone integrates
  and executes; full English/Rune horizons remain ACTIVE.
- Current managed target is VisibleDx8D13. Original DBServer resources and source
  are reference inputs; official Chinese7930/7931 resources corroborate data, not
  current7937/D13 activation or a replacement packet/profile contract.
- This milestone may decode/preserve explicit field evidence and diagnose
  ambiguous ingestion. It MUST NOT activate a unique-explicit-field merge, choose
  first/last duplicate section, default unknown to ordinary, classify by a Rune ID
  list/name, alter originals or fabricate a current profile/file owner.
- Separate4035009→4011301 and passive effect gameplay gaps remain untouched.

## Packet contract

Not applicable. No packet/schema/order, cost/cooldown, spell grant, XP, origin,
inventory/persistence or cast authorization behavior is expanded here.

## Observed source and resource facts

Original source under `征服-龙拳天下服务端-已修复/lqtxfwd/DecompiledSource/GameServer`:

- MrRayzo.Database/MagicType.cs222–232 constructs IniFile(MagicEffect.ini) and
  sets isRuneSkill from ReadByte(magic.ID+"00",IsRuneSkill,0)!=0.
- MrRayzo.WindowsAPI/IniFile.cs43–65 prefixes real ServerConfig.DbLocation and
  calls Kernel32.GetPrivateProfileStringW; ReadByte153–156 uses the existing
  ReadValue/default path. This is NOT a proven managed first/last section rule.
- Accepted Root130's LegacyNativeMagicType.Load and
  LegacyNativeItemCounterShellIni retain that native API path. Its comment states
  no host/config startup/loading route is installed. It is not authority to
  execute a second native load or mutate a global pool for this task.
- Actual managed descriptor assembly calls LegacyRuneSpellClassification through
  the already adopted WorldDataCatalog.MagicEffects. That classifier currently
  requires exactly one canonical ID00 section and an explicit validated byte.

Bounded original DBServer text reads:

| Input | Exact relevant observation |
| --- | --- |
| MagicEffect.ini6517 | [814000], Name=Vampire, no IsRuneSkill declaration |
| MagicEffect.ini28417–28423 | second[814000], Name=Caprice, IsRuneSkill=1 |
| magictype.txt528 onward |27 rows use TypeId8140, Name=Caprice, levels0..26 |
| itemtype.txt48478 onward |27 Caprice Rune SKUs4028001..27, class4000, encoded814000..814026 |

Retained official files, read without executing clients/APIs:

- `.artifacts/official-chinese-patches/20261026/extracted/7892-7930/ini/MagicEffect.ini`
- `.artifacts/official-chinese-patches/20261026/extracted/7931/ini/MagicEffect.ini`

Each has one [814000] at28177, IsRuneSkill=1 at28183. Their CP936 display text is
not a permission key. Agreement supports further diagnosis; it does not supply
the missing current selected-profile byte owner or native duplicate policy.

## Data contract and proven implementation piece

Keep MagicEffectParser's lossless section occurrences and raw entries. Do not
flatten duplicate sections or duplicate keys into a classification winner.
Add a small pure decoder/analysis for canonical numeric sections. Its ingestion
warning is bounded to the existing ushort-spell ID00 domain:

- Report Missing, UniqueExplicit, Ambiguous or Malformed explicitly. Missing is
  not byte0/false. Expose a byte value only for one valid section with exactly one
  validated explicit IsRuneSkill entry/attribute; byte semantics retain native
  zero versus nonzero without a new bool default.
- Preserve declarations with their occurrence, source line, exact raw key/value
  and raw line. Duplicate sections stay Ambiguous even if only one contains an
  explicit declaration. Repeated explicit keys/aliases stay Ambiguous, including
  equal values; conflicting or malformed raw/typed evidence cannot authorize.
- Validate canonical section identity, unique occurrence identities, raw entry
  key/value agreement and explicit byte bounds. A dictionary-only injected flag
  or last-key projection cannot conceal missing/duplicate raw declarations.
- This result is detached parser evidence, NOT a live classification capability,
  actual configured file owner, current catalog proof or permission token.
- A bounded catalog diagnostic may surface ambiguous/malformed explicit Rune
  classification at ingestion. It must not change raw catalogs, classifier
  acceptance, descriptor flags, successful packets or unknown ordinary behavior.

Observed severity/caller trace: WorldDataDiagnostic contains only Code/Message;
WorldDataStartup147–157 logs diagnostics and then returns the catalog. No new
severity model or startup gate is added. The one Factory AddRange uses code
`AmbiguousRuneSkillClassification` and a `WARNING:` message. A bounded flag-only
scan of the original DB found only814000 among duplicated sections that actually
declare IsRuneSkill (two sections, one declaration). Ordinary missing metadata
and duplicate ordinary sections produce no new warning.

## Ownership and unresolved activation boundary

No production activation/merge is authorized. LegacyRuneSpellClassification's
ambiguous8140 refusal must remain intact. Any future production join must be
issued by the actual already adopted/configured input owner, pin its complete
real file-byte image/raw rows/index/fields and recheck it at publication. A
SourceFile label, matching name, detached official file, dictionary row, source
path adjacency or hash alone cannot establish that authority.

Finite missing inputs before changing activation:

1. Root may later verify effective duplicate-section behavior using an owned
   temporary INI through the EXISTING managed wrapper/OS API, after its full gate.
   This helper will author/read source only and MUST NOT execute that API.
2. Bind the exact selected live/import input bytes and matching target profile;
   establish whether corrective ingestion or a different current resource owner
   is intended. Neither7930 nor7931 alone activates7937 or VisibleDx8D13.
3. Review an explicit correction policy if native legacy behavior conflicts with
   current proven data. Do not silently convert source default0 to absence or
   adopt the proposed unique-explicit merge before Root makes that decision.

## Regression evidence and limits

Author isolated decoder/ingestion cases for actual duplicate shape, unique
explicit0/1/nonzero bytes, absent field, duplicate/equal/conflicting aliases,
malformed/out-of-range values and raw/typed inconsistencies. Preserve existing
MagicEffectParser duplicate-section/key goldens. An actual adopted catalog
control must continue refusing ambiguous8140 and observe its explicit presence;
unique resource decoding proves only metadata parsing, never profile activation.

The two AUTHOR-ONLY Windows observation cases use the established
WindowsNativeIniTheory/LegacyNativeIniCompleteOwnedProfiles and existing wrapper
with an owned absolute override path. Unique missing0/explicit1 controls precede
each duplicate-order read. ReadString and ReadByte must agree and all owned bytes
must remain exact; ITestOutput records the observed value without a first/last
assertion. A future pass of those cases is not itself an activation decision:
Root must inspect the observations and close the configured-input/profile gap.

No builds/tests/runtime, API/Win32/native/client, Git/config/process/host action is
authorized. All tests remain authored UNRUN until Root qualifies. This bounded
parser piece and an honest ambiguity diagnostic are progress toward the actual
owner join, not evidence that8140 gameplay/classification is complete.
