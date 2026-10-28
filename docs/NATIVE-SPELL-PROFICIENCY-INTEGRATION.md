# Native spell and proficiency integration

2026-10-28. Target: Chinese7899 / VisibleDx8D13; no packet/profile changes.

The replacement joins the actual admitted session, native spell map and native
proficiency map through one current owner. Both producer directions prepare
canonical snapshots and frames before publication. Retired/replaced slots cannot
create an unbound fallback. The genuine login prefix validates original UID0;
hydration stays after journal commitment.

Paid learning uses real Character+Spells journals. Skill-book/rune changes use
Character+Items+Spells; account filenames and character UIDs may differ. Saved
spell evidence is separate from memory, advances only through a real writer,
preserves retained byte11 and refuses external disk drift. Ranking and owned
teleport/vendor presentation retain the original issued identity.

Unbound proficiency loads validate the whole bounded image before map merge.
Rooted saves use atomic replacement after preparation. Opened saves write the
exact complete image; post-write I/O failures have no rollback claim.

Validation:2405 selected core passed, zero failures, two existing skipped teleport
cases. Final opened-file refinement:81 focused passed, zero failures/skips.
Selections overlap. Earlier211- and44-failure runs were retained and corrected.
Full CI and actual client acceptance for this integration remain required.

Remaining: configured bound proficiency saved-image ownership, native aggregate
SaveClient/SQL durability, wider cast/event/social/trade parity, broader native
item formats, newer patch gameplay activation and English UI acceptance. Decoded
7935 catalogs/codecs do not establish completed gameplay. No new overall parity
percentage, deployment or OS crash/power-loss guarantee follows from these checks.
Running sessions, original saves, routes and restore ZIP remain preserved.
