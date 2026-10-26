using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private sealed record PrefixPlayerState(uint Uid, uint Online, uint Class, ushort Transformation,
        bool Relic, bool Effect, bool Skills, uint Deity, uint Hero, uint Experience, uint Promote,
        byte Skin, byte Doom, byte Elf, byte Dragon, byte Chi, byte Guild, ushort Body, ushort Face,
        string Name, string Spouse);

    private static PrefixPlayerState ObservePrefixPlayer(LegacyNativePlayerRuntime player) => new(
        player.UID, player.Online, player.Class, player.TransformationID, player.RelicSpirit,
        player.SpawinEffect, player.SpawinEffectSkills, player.DeityLandLuckyPoints, player.HeroPoints,
        player.ClassExperience, player.SavePromote, player.SkinUnlock, player.DoomosaurSkin,
        player.PrayingElfSkin, player.PolarDragonSkin, player.ChargeChi, player.ChargeGuildSkill,
        player.Body, player.Face, player.Name, player.Spouse);

    private static CharacterSnapshot ReadPrefixSnapshot(LoginInput input)
    {
        var load = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, load.Status);
        return Assert.IsType<CharacterSnapshot>(load.Snapshot);
    }

    private static byte[] WritePrefixFields(LoginInput input, string fields)
    {
        var bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(input.InitialBytes).Replace(
            "RelicSpirit=True\r\n", "RelicSpirit=True\r\n" + fields, StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }

    private sealed class PrefixCatalogScope : IDisposable
    {
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface> previous = LegacyNativeHairfaceStorageTable.Hairfaces;
        internal PrefixCatalogScope(params LegacyNativeHairfaceStorageTable.Hairface[] rows) =>
            LegacyNativeHairfaceStorageTable.Hairfaces = rows.ToList();
        public void Dispose() => LegacyNativeHairfaceStorageTable.Hairfaces = previous;
    }
}
