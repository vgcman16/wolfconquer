# Literal UI resource keys

Target: retained official Chinese client7937, UTF8 Cn_Res.ini. Offline text
proposals only; no client lookup or gameplay authority is inferred from names.
This refines earlier named-UI specifications' uppercase-only plan grammar.

Observed resource evidence includes mixed-case suffixes and76 numeric-tooltip
keys across77 physical rows. The parser already preserves their literal bytes;
the older plan validator rejected them.

Plan keys require uppercase STR_ followed by ASCII letters, digits or underscores,
or exactly STR_BTN_TIP_[0-9]+-[0-9]+. Preserve spelling and leading zeros. Refuse
all other hyphenated forms, Unicode digits/dashes, punctuation and lowercase
prefixes. Reject casefold-colliding selected names before row selection/output;
retain existing selected case-alias holds in every input. No normalization,
numeric conversion, control-ID interpretation or fallback lookup is added.

All existing input pins, family bindings,200-key/64KiB-plan/16MiB-input bounds,
encoding/path/create-only-output rules, duplicate meanings, machine-value and
ordered-token checks remain. Public fixtures use synthetic text.

Two new API/CLI positives failed against the old tool (one error,one failure).
The integrated expanded suite passed53/53,0failed/skipped,in28.053s. Eight new
methods cover exact mixed-case/tooltip bytes, leading zeros, duplicates, BOM and
line endings, prior/unselected bytes, syntax/alias refusals, retained token/value
guards and no-output malformed-key/stale-pin errors. FSB-351 records this bounded
qualification. No rendered resource, native lookup, live7937 behavior or C#/SQL
qualification follows.
