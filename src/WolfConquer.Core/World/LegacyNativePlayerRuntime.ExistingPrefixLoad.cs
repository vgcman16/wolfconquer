using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyNativePrefixValues(
    uint Uid,
    uint Class,
    ushort Level,
    bool RelicSpirit,
    uint Online,
    bool SpawinEffect,
    bool SpawinEffectSkills,
    uint DeityLandLuckyPoints,
    uint HeroPoints,
    uint ClassExperience,
    uint SavePromote,
    byte SkinUnlock,
    byte DoomosaurSkin,
    byte PrayingElfSkin,
    byte PolarDragonSkin,
    byte ChargeChi,
    byte ChargeGuildSkill,
    ushort Body,
    ushort Face,
    string Name,
    string Spouse);

public sealed partial class LegacyNativePlayerRuntime
{
    internal sealed class ExistingPrefixLoad
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePrefixValues before, after;
        private readonly uint job;
        private readonly ushort transformation;
        private bool published;

        private ExistingPrefixLoad(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client,
            LegacyNativePrefixValues before, LegacyNativePrefixValues after, uint job, ushort transformation)
        {
            this.player = player; this.client = client; this.after = after;
            this.before = before; this.job = job; this.transformation = transformation;
        }

        internal static ExistingPrefixLoad? TryPrepare(LegacyNativeGameClientRuntime client, CharacterSnapshot snapshot,
            LegacyNativeItemCounterShellIni ini)
        {
            if (client?.Player is not { } player || !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake ||
                player.Name is null || player.Spouse is null ||
                snapshot.Uid is <= 0 or > uint.MaxValue || snapshot.Class is < 0 or > uint.MaxValue ||
                snapshot.Level is < 0 or > ushort.MaxValue ||
                player.UID != 0 && player.UID != snapshot.Uid || player._class != 0 && player._class != snapshot.Class) return null;
            var before = Read(player);
            var job = player._class; var transformation = player._transformationid;
            var relic = ini.ReadBool("Character", "RelicSpirit", false);
            var after = new LegacyNativePrefixValues(checked((uint)snapshot.Uid), checked((uint)snapshot.Class),
                checked((ushort)snapshot.Level), relic, checked(before.Online + 1),
                ini.ReadBool("Character", "SpawinEffect", false), ini.ReadBool("Character", "SpawinEffectSkills", false),
                ini.ReadUInt32("Character", "DeityLandLuckyPoints"), ini.ReadUInt32("Character", "HeroPoints"),
                ini.ReadUInt32("Character", "ClassExperience"), ini.ReadUInt32("Character", "SavePromote"),
                ini.ReadByte("Character", "SkinUnlock", 0), ini.ReadByte("Character", "DoomosaurSkin", 0),
                ini.ReadByte("Character", "PrayingElfSkin", 0), ini.ReadByte("Character", "PolarDragonSkin", 0),
                ini.ReadByte("Character", "ChargeChi", 0), ini.ReadByte("Character", "ChargeGuildSkill", 0),
                ini.ReadUInt16("Character", "Body", 1002), ini.ReadUInt16("Character", "Face", 0),
                ini.ReadString("Character", "Name", "None"), ini.ReadString("Character", "Spouse", "None"));
            var candidate = new ExistingPrefixLoad(player, client, before, after, job, transformation);
            return candidate.IsCurrent() ? candidate : null;
        }

        internal static LegacyNativePrefixValues Read(LegacyNativePlayerRuntime value) => new(
            value.UID,
            value._class,
            value._level,
            value.RelicSpirit,
            value.Online,
            value.SpawinEffect,
            value.SpawinEffectSkills,
            value.DeityLandLuckyPoints,
            value.HeroPoints,
            value._classexp,
            value.SavePromote,
            value.SkinUnlock,
            value.DoomosaurSkin,
            value.PrayingElfSkin,
            value.PolarDragonSkin,
            value.ChargeChi,
            value.ChargeGuildSkill,
            value.body,
            value.face,
            value.Name,
            value.Spouse);

        internal static bool MatchesValuesPlain(LegacyNativePlayerRuntime player, LegacyNativePrefixValues value) =>
            player.UID == value.Uid && player._class == value.Class && player._level == value.Level &&
            player.RelicSpirit == value.RelicSpirit && player.Online == value.Online &&
            player.SpawinEffect == value.SpawinEffect && player.SpawinEffectSkills == value.SpawinEffectSkills &&
            player.DeityLandLuckyPoints == value.DeityLandLuckyPoints && player.HeroPoints == value.HeroPoints &&
            player._classexp == value.ClassExperience && player.SavePromote == value.SavePromote &&
            player.SkinUnlock == value.SkinUnlock && player.DoomosaurSkin == value.DoomosaurSkin &&
            player.PrayingElfSkin == value.PrayingElfSkin && player.PolarDragonSkin == value.PolarDragonSkin &&
            player.ChargeChi == value.ChargeChi && player.ChargeGuildSkill == value.ChargeGuildSkill &&
            player.body == value.Body && player.face == value.Face && player.Name == value.Name && player.Spouse == value.Spouse;

        internal uint PreparedUid => after.Uid;
        internal bool IsCurrent() => IsCurrentPlain();
        internal bool IsCurrentPlain() => !published && ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            !client.FullLoading && !client.Fake && player._class == job && player._transformationid == transformation && MatchesValuesPlain(player, before);

        internal bool IsPublishedCurrent() => IsPublishedCurrentPlain();
        internal bool IsPublishedCurrentPlain() => published && ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            !client.FullLoading && !client.Fake && player._transformationid == transformation && MatchesValuesPlain(player, after);

        internal void PublishPrepared()
        {
            player.UID = after.Uid;
            player._class = after.Class;
            player._level = after.Level;
            player.RelicSpirit = after.RelicSpirit;
            player.Online = after.Online;
            player.SpawinEffect = after.SpawinEffect;
            player.SpawinEffectSkills = after.SpawinEffectSkills;
            player.DeityLandLuckyPoints = after.DeityLandLuckyPoints;
            player.HeroPoints = after.HeroPoints;
            player._classexp = after.ClassExperience;
            player.SavePromote = after.SavePromote;
            player.SkinUnlock = after.SkinUnlock;
            player.DoomosaurSkin = after.DoomosaurSkin;
            player.PrayingElfSkin = after.PrayingElfSkin;
            player.PolarDragonSkin = after.PolarDragonSkin;
            player.ChargeChi = after.ChargeChi;
            player.ChargeGuildSkill = after.ChargeGuildSkill;
            player.body = after.Body;
            player.face = after.Face;
            player.Name = after.Name;
            player.Spouse = after.Spouse;
            published = true;
        }
    }
}
