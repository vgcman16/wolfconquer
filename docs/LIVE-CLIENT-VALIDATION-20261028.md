# Local client validation - 2026-10-28

The exact public source `be2740aea8f3295b57901fcd64d58a5e9cfc11c8`
passed [Windows run 40742984664](https://github.com/vgcman16/wolfconquer/actions/runs/40742984664).
All eight steps succeeded between 18:13:39.1971730 UTC and 19:03:23.7027898 UTC:
Core 29,280 passed with 164 existing skips, Updater 446 passed, Launcher 57
passed, and synthetic parser checks 20+13 passed without network probes.

The exact runner Release files were staged without a rebuild into a newly
created, genuine owned local fixture. The three existing local bridge roles
and routes were retained; original saves were not touched. The selected client
was Chinese 7899 using the `VisibleDx8D13` profile. The local login used the
existing held-scan-code helper after instantaneous key input did not complete
the interaction. This describes the observed input route, not general login
automation or a credential-handling claim.

The actual client displayed the full character and Twin City scene. The
synthetic character `LootFixture` had Body 1008, Class 1000 and Level 120. Six
movement messages were observed; the saved position changed from map 1002
(410,354) to (416,357). Money remained 100 and Conquer Points remained 50.

The copied client was closed through ExitGame and its process returned code
18; that is the observed exit code, not a zero-exit or crash-free result. World
then stopped gracefully with code 0 and released its port. World restarted on
the same exact binaries and the same genuine fixture/domain, without seeding
or bootstrapping it again. A fresh client and new login showed the full body and
scene again and the saved (416,357) position. Selected saved fields remained
unchanged after that login.

Three unsupported opcode 2307 startup requests were observed per login. No raw
packet payload is published here. GUI labels remain partly Chinese.

After the baseline, a private A/B trial changed one pinned English resource
field in the copied client. A fresh client reported the native window title
`WolfConquer`; the subsequent local login succeeded and Twin City loaded. No
other client or route file was changed for that trial. This confirms that the
tested local configuration displayed the changed title; it does not establish
a complete client rebrand, translation, feed or public asset distribution.
The equipment/status panel rendered the body preview, level120 InternTrojan,
HP2850, attack150---151 and M.Atk1. Inventory showed the expected potion and
balances100 gold/50 CP. Empty equipment slots match this gear-free fixture.
Ten top-level forge categories opened: level upgrade, composition, sockets,
dragon soul, gem composition, purification/refinery, blessing/enchantment,
smelting, artifact fusion and equipment spirit. No final forge or consumption
action was performed. This establishes those views, not their transactions.
Subclass labels contained mojibake; many forge labels remained Chinese.

The owner then requested gear/material grants and a local sender that delivers
through the running mailbox without relogging. That sender is under development
and is not part of the qualified be2740ae build or this live acceptance result.

This is bounded local client and file-backed persistence evidence. It does not
establish SQL persistence, crash or power-loss recovery, every menu, complete
English localization, full gameplay parity, or 7935 runtime compatibility.
The earlier failed public 9769 full run and its original unproven CrackStar
timeout cause remain documented in the regression repair record; the focused
46/46 successor result and this full gate do not explain that original timeout.
