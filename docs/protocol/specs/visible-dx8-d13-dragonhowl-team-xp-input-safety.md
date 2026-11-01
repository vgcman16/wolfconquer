# Dragonhowl Team XP selected input safety

SOURCE ONLY / UNBUILT / UNRUN, 2026-11-01, VisibleDx8D13. Extends frozen08
through a narrow preflight validation call; no owner or global XP policy change.

The current server-rate reader skips malformed/nonfinite supplied overrides and
continues to a lower-priority alias, environment or default. The proc checked
calculation then sees a valid fallback and may debit/kill/reward despite invalid
selected input. This intake rejects invalid selected saved input before those
effects. The actual first PRESENT source is selected in existing precedence:
UserExpRate, ServerExperienceRate, ExperienceRate, MonsterKillExperienceRate;
MaximumMonsterKillExperienceRate then MaximumServerExperienceRate;
RateExp, DoubleExperienceRate then DExpRate. Present malformed, nonfinite or
negative selected values refuse. Valid distinct-name precedence is preserved;
unused lower-priority values are not parsed or normalized by the new validator.
Absent override families retain existing environment/default behavior. The
existing checked arithmetic still enforces finite products and signed saved-XP
width; the new helper is DATA validation, never authorization.

All live regression inputs are saved before real Bootstrap/admission. Real Team
ingress, native19 gear201 and Physical17490/17650 issue the operation. Eligible
outside-line recipient(830,594) receives a life500 monster's 10XP share. Negative
cases preserve exact holders, native values/stamp, caches, files, item/spell
images, monster500HP and all output. Required second-level stats/threshold data
are checked across the whole two-monster plan before first cost or kill.

Independent positive bounds: 10 * 2^59 = 5764607523034234880 fits signed Int64 and
stays below explicit threshold9223372036854775807; 10 * 2^60 does not fit and
must refuse. Eligible rate0 gives zero award but still requires the real roster.
An alive member at distance19 is ineligible; its unused NaN rate must not cause
an invented XP policy requirement. It joins while visible at distance10 and
uses actual owned Jump to distance19 before the tested proc; all policy DATA
was saved before Bootstrap. Valid first-source1 with lower sources NaN
retains XP10. Missing Spells remains genuinely absent and no target player is hit.

Same-key case aliases are distinct from named source precedence. Saved-image
admission rejection is recorded separately if that guard prevents a live proc
from being reached; no raw mutation after admission is used to fake later proof.
No new client packet layout is introduced. Literal2176 XP and decoded2239 target
goldens retain the earlier fixture contract. Full suite, live acceptance, global
generic-XP cleanup and all-target crash atomicity remain open.
