using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task GenuineUnarmedNeutralHitRetainsTheActuallyAdmittedAbsentProficiencyFile() =>
        RunFirstUseHitAsync(false, true, false, async f =>
        {
            Assert.Empty(f.Actor.PlayerItems.EquipmentItems);
            var origin = f.Actor.NativeGameClient!.OrdinarySpellBinding!.SavedProficiencies;
            var before = f.Owner.Data.Files();
            var key = Path.GetRelativePath(f.Owner.Data.Root, f.CharacterPath);
            var text = CharacterSnapshotFileEncoding.Decode(before[key], out var encoding);
            Assert.Equal(2, text.Split("MinHitPoints=2000\r\n", StringSplitOptions.None).Length);
            before[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(
                "MinHitPoints=2000\r\n", "MinHitPoints=1700\r\n", StringComparison.Ordinal))).ToArray();
            await f.AttackAsync();
            f.Owner.Data.AssertFiles(before); Assert.False(File.Exists(f.ProfsPath));
            Assert.Equal(1700L, f.Victim.Character.MinHitPoints); Assert.Equal(1700, f.Victim.NativeGameClient!.Player.HitPoints);
            Assert.Empty(f.Actor.PlayerProficiencies.Proficiencies);
            Assert.Empty(f.Actor.NativeGameClient.MyProfs!.ClientProf);
            Assert.Same(origin, f.Actor.NativeGameClient.OrdinarySpellBinding!.SavedProficiencies);
            Assert.True(origin.Matches(null)); Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
            Assert.DoesNotContain(f.Owner.Frames(CreationFixture.RuneActor), bytes =>
                LegacyProficiencyPacket.TryParse(CreationFixture.ReadFrame(bytes), out _));
            Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        }, noWeapon: true);
}
