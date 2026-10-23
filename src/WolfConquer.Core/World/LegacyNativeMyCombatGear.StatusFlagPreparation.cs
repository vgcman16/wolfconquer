using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private PreparedSelectedStatusValues PrepareSelectedStatusFlagValues()
    {
        var client = user ?? throw SelectedStatusUnavailable("client missing");
        var player = client.Player ?? throw SelectedStatusUnavailable("player missing");
        var vector = player.BitVector ?? throw SelectedStatusUnavailable("flag owner missing");
        uint uid = player.UID;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            if (!ReferenceEquals(user, client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || !ReferenceEquals(player.BitVector, vector))
                throw SelectedStatusUnavailable("physical owner changed");
            var words = vector.bits ?? throw SelectedStatusUnavailable("flag words missing");
            var wordImage = (uint[])words.Clone();
            var registrations = vector.ArrayFlags ?? throw SelectedStatusUnavailable("flag rows missing");
            ConcurrentDictionary<string, double[]>? consultedInfo = null;
            var selected = new PreparedSelectedStatusValues(
                    HolyProtection: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.CannonBarrage | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "HolyProtection", 1, true, ref consultedInfo),
                    PhoenixBlessing: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Top8Weekly | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "PhoenixBlessing", 1, true, ref consultedInfo),
                    NobleSpirit: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.DragonPierceTortoise | LegacyNativeMsgUpdate.Flags.Top4Weekly,
                        "NobleSpirit", 1, true, ref consultedInfo),
                    FlameShield: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Confused | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "FlameShield", 2, true, ref consultedInfo),
                    FlowKnack: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.TopWaterTaoist | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "FlowKnack", 2, true, ref consultedInfo),
                    DivineEmptiness: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.AzureShield | LegacyNativeMsgUpdate.Flags.PaperDance,
                        "DivineEmptiness", 1, true, ref consultedInfo),
                    WackeSpirit: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.CTF_Flag | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "WackeSpirit", 1, true, ref consultedInfo),
                    CrackMantra1: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.TopPirate | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "CrackMantra1", 1, true, ref consultedInfo),
                    CrackMantra2: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Freeze | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "CrackMantra2", 1, true, ref consultedInfo),
                    Sense: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.WildSigilBurning | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Sense", 2, false, ref consultedInfo),
                    Diabolize: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Ghost | LegacyNativeMsgUpdate.Flags.MagicDefender | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Diabolize", 1, true, ref consultedInfo),
                    Barrier: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Water | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Barrier", 1, true, ref consultedInfo),
                    Shell: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Shell", 1, true, ref consultedInfo),
                    Storm: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.BladeFlurry | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Storm", 1, true, ref consultedInfo),
                    Thrash: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.KineticSpark | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Thrash", 1, true, ref consultedInfo),
                    Thunder: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.PathOfShadow | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Thunder", 1, true, ref consultedInfo),
                    Torrent: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Lightning | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Torrent", 1, true, ref consultedInfo),
                    Tide: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Wind | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Tide", 1, true, ref consultedInfo),
                    Splash: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.SuperCyclone | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "Splash", 1, true, ref consultedInfo),
                    Sailing: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.LotusDemon | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Sailing", 1, true, ref consultedInfo),
                    Vast: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.Earth | LegacyNativeMsgUpdate.Flags.MagicDefender,
                        "Vast", 1, true, ref consultedInfo),
                    ColdBloodline: ReadSelectedStatusValue(vector, LegacyNativeMsgUpdate.Flags.TopDragonLee | LegacyNativeMsgUpdate.Flags.HawksAmbition,
                        "ColdBloodline", 1, true, ref consultedInfo));

            // Final actual membership queries can invoke the existing comparer.
            // Follow them with callback-free bitmap and physical-owner checks.
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.CannonBarrage | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.HolyProtection.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Top8Weekly | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.PhoenixBlessing.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.DragonPierceTortoise | LegacyNativeMsgUpdate.Flags.Top4Weekly)) != selected.NobleSpirit.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Confused | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.FlameShield.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.TopWaterTaoist | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.FlowKnack.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.AzureShield | LegacyNativeMsgUpdate.Flags.PaperDance)) != selected.DivineEmptiness.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.CTF_Flag | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.WackeSpirit.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.TopPirate | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.CrackMantra1.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Freeze | LegacyNativeMsgUpdate.Flags.Top4Weekly | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.CrackMantra2.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.WildSigilBurning | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Sense.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Ghost | LegacyNativeMsgUpdate.Flags.MagicDefender | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Diabolize.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Water | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Barrier.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.TopSuperGuildWarFiveStars | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Shell.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.BladeFlurry | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Storm.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.KineticSpark | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Thrash.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.PathOfShadow | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Thunder.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Lightning | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Torrent.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Wind | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Tide.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.SuperCyclone | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.Splash.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.LotusDemon | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Sailing.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.Earth | LegacyNativeMsgUpdate.Flags.MagicDefender)) != selected.Vast.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (vector.ContainFlag((int)(LegacyNativeMsgUpdate.Flags.TopDragonLee | LegacyNativeMsgUpdate.Flags.HawksAmbition)) != selected.ColdBloodline.Active)
                throw SelectedStatusUnavailable("flag selection changed");
            if (!ReferenceEquals(user, client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid ||
                !ReferenceEquals(player.BitVector, vector) || !ReferenceEquals(vector.bits, words) ||
                !ReferenceEquals(vector.ArrayFlags, registrations) || !wordImage.AsSpan().SequenceEqual(words) ||
                (consultedInfo is not null &&
                    (!ReferenceEquals(Info, consultedInfo) || !LegacyNativeCombatGearTable.m_Load)))
                throw SelectedStatusUnavailable("prepared input owner changed");
            return selected;
        }
    }

    private SelectedStatusValue ReadSelectedStatusValue(LegacyNativeStatusFlagsBigVector32 vector,
        LegacyNativeMsgUpdate.Flags flag, string name, int requiredLength, bool firstUsed,
        ref ConcurrentDictionary<string, double[]>? consultedInfo)
    {
        if (!vector.ContainFlag((int)flag)) return default; // Inactive: do not read GetValue/Info.
        var info = Info ?? throw SelectedStatusUnavailable(name);
        if (consultedInfo is not null && !ReferenceEquals(consultedInfo, info))
            throw SelectedStatusUnavailable("named-value owner changed");
        var values = GetValue(name); // Genuine source availability/comparer semantics.
        if (values is null || values.Length < requiredLength)
            throw SelectedStatusUnavailable(name);
        consultedInfo = info;
        // Copy only source-required coefficient slots, not computed expressions.
        // Extra tail values and all legal nonfinite doubles remain untouched.
        return new(true, firstUsed ? values[0] : 0, requiredLength == 2 ? values[1] : 0);
    }

    private static InvalidDataException SelectedStatusUnavailable(string detail) =>
        new("Selected combat-gear status values are unavailable: " + detail + ".");

    private readonly record struct SelectedStatusValue(bool Active, double First, double Second);

    private sealed record PreparedSelectedStatusValues(
        SelectedStatusValue HolyProtection,
        SelectedStatusValue PhoenixBlessing,
        SelectedStatusValue NobleSpirit,
        SelectedStatusValue FlameShield,
        SelectedStatusValue FlowKnack,
        SelectedStatusValue DivineEmptiness,
        SelectedStatusValue WackeSpirit,
        SelectedStatusValue CrackMantra1,
        SelectedStatusValue CrackMantra2,
        SelectedStatusValue Sense,
        SelectedStatusValue Diabolize,
        SelectedStatusValue Barrier,
        SelectedStatusValue Shell,
        SelectedStatusValue Storm,
        SelectedStatusValue Thrash,
        SelectedStatusValue Thunder,
        SelectedStatusValue Torrent,
        SelectedStatusValue Tide,
        SelectedStatusValue Splash,
        SelectedStatusValue Sailing,
        SelectedStatusValue Vast,
        SelectedStatusValue ColdBloodline);
}
