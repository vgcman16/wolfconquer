using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Theory]
    [InlineData("set")]
    [InlineData("compare")]
    [InlineData("transaction")]
    public Task GenericPublicationWithTheActualEarnedFlashingRegistrationPreservesItsNativeOwner(string route) => Run(async f =>
    {
        await EarnFlashingGuardStateAsync(f);
        var before = CaptureFlashingGuardState(f);
        PublishUnchangedFlashingGuardControl(f, route);
        AssertFlashingGuardState(f, before);
    });

    private static async Task EarnFlashingGuardStateAsync(OwnedEquipmentBehaviorFixture f)
    {
        AssertNoFlashing(f.Current(Actor));
        var start = f.ActorFrames.Count;
        await f.SendAsync(Actor, Request());
        var bytes = Assert.Single(f.ActorFrames.Skip(start), Animation);
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(Target, hit.Uid); Assert.Equal(582U, hit.Damage);
        Assert.Equal(MsgAttackEffect.Penetration, hit.Effect);
        Assert.Equal(99_418, f.Current(Target).Character.MinHitPoints);
        Assert.Equal(99_418, f.Current(Target).NativeGameClient!.Player.HitPoints);
        Assert.True(f.Current(Actor).NativeGameClient!.OnAutoAttack);
        Assert.False(f.Recovery.IsBlocked);
    }

    private static void PublishUnchangedFlashingGuardControl(OwnedEquipmentBehaviorFixture f, string route)
    {
        var before = f.Current(Actor);
        WorldSession? final;
        switch (route)
        {
            case "set":
                f.Store.Set(f.Connection(Actor), before, out var written);
                final = written;
                break;
            case "compare":
                Assert.True(f.Store.TryCompareAndSet(f.Connection(Actor), before, before, out final));
                break;
            case "transaction":
            {
                // Real configured writer, not an accepting callback. Exact saved
                // no-op images exercise this API without changing gameplay state.
                var character = File.ReadAllBytes(f.CharacterPath(Actor));
                var spellsPath = Path.Combine(f.Data.Spells, Actor.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
                var spells = File.ReadAllBytes(spellsPath);
                LegacyTradeJournalChange[] changes =
                [
                    new(LegacyTradeJournalFileKind.Character, before.AccountId, character, character.ToArray()),
                    new(LegacyTradeJournalFileKind.Spells, before.Character.Uid, spells, spells.ToArray()),
                ];
                var calls = 0; var reason = string.Empty;
                bool ActualConfiguredWriter()
                {
                    calls++;
                    Assert.Same(before, f.Current(Actor));
                    return LegacyTradeJournal.TryCommitCharacterSpells(f.Data.Users, f.Data.Spells, changes, out reason);
                }
                Assert.True(f.Store.TryCommitSession(f.Connection(Actor), before, before, ActualConfiguredWriter, out final), reason);
                Assert.Equal(1, calls);
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
        Assert.Same(before, final); Assert.Same(final, f.Current(Actor));
        Assert.Same(before.CanAttackFlashingRegistration, final!.CanAttackFlashingRegistration);
    }
}
