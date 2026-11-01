# Dragonhowl lethal-monster / team-level / player-death overlap

Status: SOURCE ONLY, UNBUILT / UNRUN. This independent two-case regression is
initially expected to fail on the qualified branch that still holds lethal
monster proc activation. It specifies the required complete chain; it does not
qualify the later production intake. Target profile: VisibleDx8D13. No client
execution or live packet acceptance is claimed.

## Genuine inputs and owners

`OwnedDragonhowlExperienceOverlapTests` uses the existing owned equipment
fixture's actual legacy19 metadata publisher and ordinary admission. The actor
has saved equipped combat gear 201 and exactly the selected learned spell
17490 or 17650. The request is Physical, addressed to a real Frame-registered
monster; it is not a direct client Magic request. Test setup sends real Team
Create / InviteRequest / AcceptInvitation packets and checks the resulting
roster. Map 1018, DynamicID 77 and Free PK mode 0 keep the same-team peer an
applicable later player target on the line. All stats, gear, spell, roll and
catalog data are saved before admission. No native class, status, timer or
cache is forged after admission.

The family is an ordinary level-10, 500-HP monster. It selects no floor drop
(source drop roll 0), but selects a real 5-CP reward, so the reward chain cannot
pass as an entirely empty preparation. It has no applicable boss, study, mail,
special-map, quest or event reward. Those branches remain separate requirements.
The catalog uses concrete arrays for selected monster, spell, threshold and
class-stat rows, plus the same legacy19 gear row used by the native publisher.

## Independent expected progression and wire order

The actor has Strength 1000 and neutral gear bonuses. The spell's non-player
bonus is 250 percent: 1000 times 350 percent, then the actual equal-level
120-percent multiplier, yields requested monster damage 4200. Monster defense
is 0 and defense2 is 10000. The peer receives 500 / 50 = 10 team XP; neither
newbie nor spouse multiplier applies. A real level-10 threshold of 1 gives
level 11 / XP 9. The exact Taoist auto-stat row grants Vitality 20 / Spirit 20,
so the query computes HP 24*20 + 3*20 = 540, mana 30*20 = 600 and the late
magic profile produces MagicAttack 21. The actual level-up query and HP restore
must finish, and its marker must be durably cleared, before the later player
receive. This is not a borrowed old cache or a pure future DATA result.

The minimum selected player bonus is 2 percent; neutral defense, equal saved
battle power and the source no-penetration 50-percent increase give 1530.
Ironbone 17650 applies the source 75-percent player multiplier, yielding
1147 after integer truncation. Either hit kills the freshly restored 540-HP
peer. The real death query must retain the rebuilt maxima and magic cache,
publish native/holder/file HP 0 and the dead flag, and leave level 11 / XP 9.

Assertions require the CP update before the peer level update; a genuine 2301
level query and HP restore before the peer final EXP 2176; then the second
2301 death query and HP-0 2176 before the actor's 2239 animation. Independent
literal D13 bytes cover level 11, XP 9, HP 540 and HP 0. The animation must
contain both targets in monster-then-player order with the independent damage
values. A later real status request must expose HP 540 / mana 600 / magic 21.

At final EXP output, the test reads the actual saved peer image, current holder
and native owner and requires marker absence and restored HP. At animation it
requires the actual final dead state. At completion it compares every owned
file byte-for-byte against the captured baseline with only the independently
enumerated Character scalar replacements. Item, spell, proficiency, gear,
metadata and unrelated files therefore remain exact. Actor mana is 493,
stamina 95 and CP increases once by 5; actor XP and learned-spell XP remain 0.

## Limits

This tests ordered committed phases, not all-target crash atomicity, an outbox,
fresh-request deduplication, live-client acceptance or complete monster rewards.
The real current proc death owner covers death metadata, query/native status,
HP/derived-spell journal22 and team notifications; this test does not invent a
separate PVP penalty/reward writer. No interruption/restart claim is made by
these two success cases. The test observer reads and asserts only; it never
reenters the Frame or supplies an accepting owner/callback.

The only existing fixture edit appends optional `combatGear` at the end of
`LegacyItemWorldDataFixture.Create` and forwards it to the catalog. Every old
positional argument and default is retained. Root alone integrates and runs
this intake with the complete later production chain.
