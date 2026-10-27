# Target job-family qualification

This batch extends existing class-family limits from suffix 51 through 57. Its
nine production files and 37 test files preserve packet layouts, reward amounts,
grant order, cost ordering and successful saved-record formats. The related
[source-bug entries](../FIXED-SOURCE-BUGS.md) are FSB-141–145.

## Target evidence and scope

The VisibleDx8D13 primary `ProfessionalName.ini`, pinned as
`EFB4852AEDDDD26F1B5A16AC814FE3243A54D4266AE1089FDBE357C5D6867EB6`, contains 634 known IDs: families 1/2/4/5/6/7/8/9/16 cover
000–057; Water 13 and Fire 14 cover 002–057. Its display text was edited, so
English names are not independent class authority. The numeric IDs and
non-display columns corroborate these bands in the newer Chinese-table
intersection; variant tables do not extend this target beyond 57.

No new family mapping is assigned to the 17 observed unmodeled IDs in groups
11/12/15/19. The broad `IsTaoist` span extends to 14057 while retaining its lower bound and
inherited interior values; this compatibility span is not a new eligibility
rule. The distinct Lee item gate ending at 8049 remains unchanged. Prestige's
World-only 10000–10002 disposition is not a guild permission or reward family;
10002 remains an absent-table compatibility literal.

Shared predicates now recognize the confirmed stages. `State.PositionArray`
uses those predicates in its existing selection order, retaining stat keys and
fallback. Independent physical HP/Mana limits, ordinary Water mana and equipment
Water/Fire mana are aligned without changing coefficients or numeric widths.
Physical calculators retain UInt16 arithmetic; ordinary mana retains long
arithmetic. Equipment mana retains its separate item and UInt16-derived class
contributions; its Pirate 7051 limit is untouched.

Guild `Profession` admits these stages only through existing permission bits;
missing permissions still refuse. Five configured boss-package families retain
their exact rewards through stage 57. Other families remain `Other` for this
policy; Pirate ranks 8/9 still have no Pirate bonus. Trojan login-grant selection
recognizes current/first/second lineage through 1057, preserving missing-only
grant 1110, existing twelve-byte spell records, flags and order.

## Qualification

The public tests-only baseline had **335 failures, 1,671 passes and zero skips**
across 2,006 cases. Corrected focused qualification passed **2,006/2,006**, with
every exact baseline test name retained. Private corrected selection **40466 also passed 2,006/2,006**, zero failures
or skips. Its complete test-name/outcome set matches the public selection.

Actual managed consumer controls include ordinary `RefreshVitals` and equipment
`EquipmentQuery.Prefix` mana, genuine Ninja/Pirate cold mailbox recovery, PIN,
journal/reward/replay, and transactional ordinary-login spell publication and
repeat behavior. `RefreshVitals` coverage does not certify the whole level-up
transaction. State table selection, physical native-named HP/Mana calculators
and Guild permission APIs have direct API coverage; no production adoption or
guild-join workflow is inferred from those tests.

The earlier full-source `252bb950` passed
[Windows40501867920](https://github.com/vgcman16/wolfconquer/actions/runs/40501867920):
27,926 core passes with 164 existing NotExecuted cases, updater 434 and launcher
57; all eight steps exited zero. Parser checks covered 20 synthetic receipt
cases and 13 synthetic TRX cases, without network probes. The generated-output-only
`773b9871` revision is code-equivalent to that source. This newer class batch
still requires its own exact-source full gate.

## Persistence and remaining work

Valid retained version-5 mailbox journals replay their pinned old postimages;
they do not gain a newly recognized bonus. Before-only retries recompute current
rewards: Ninja stages 5052/5057 at every rank and Pirate 7052/7057 at ranks 1–7
can produce recipe/intent conflicts, retaining recovery debt. Pirate ranks 8/9
can resume only if the complete payload and genuine prerequisites still match.
No automatic migration or compensation is implemented.

Version-6/7 ordinary-admission validation recomputes expected lineage grants, so
old pending records that omitted the spell may refuse and require owned
reconciliation. The older reachable startup Ensure/Persist helper can return
learned state after a missing save route or logged IO failure; that best-effort
save defect remains open. Journal versions and schemas are unchanged.

Other direct bounds, `InfoStr`'s byte class parameter, CLI/runtime adoption,
whole equipment/rebirth/grant durability, live-client behavior and distribution
remain separate work. These results do not establish deployment or full parity.
