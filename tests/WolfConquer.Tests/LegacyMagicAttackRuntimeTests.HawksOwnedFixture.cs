using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort HawksAmbitionAttachStatusSpellId = 15_920;
    private const int HawksAmbitionStatusFlag = 0x100;

    private sealed class HawksOwnedClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    }

    internal static Task RunOwnedHawksAmbitionAttachStatusAsync()
    {
        // Original explicitly configured test rows, not retained catalog proof:
        // power35/duration33, level0 threshold50, level1 threshold200, mana7.
        var catalog = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 0,
                experience: 50, damage: 35, duration: 33, coldTime: 0),
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 1,
                experience: 200, damage: 35, duration: 33, coldTime: 0));
        var input = CreateSession(spellIds: [HawksAmbitionAttachStatusSpellId]);
        var clock = new HawksOwnedClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock);
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(
            new(input.Character, input.PlayerItems, input.PlayerSpells, input.PlayerProficiencies),
            catalog, options, async f =>
            {
                // Real login/query normalization precedes this baseline; no
                // native scalar or post-admission saved input is fabricated.
                var before = f.Current(PlayerUid);
                Assert.Equal(500L, before.Character.MinMana);
                Assert.False(HasLegacyStatusFlag(before.Character.Attributes, HawksAmbitionStatusFlag));
                Assert.False(before.Character.Attributes.ContainsKey("HawksAmbitionPower"));
                Assert.Equal(new PlayerSpellSnapshotEntry(15920, 0, 0, 0, 0, 0), Assert.Single(before.PlayerSpells.Spells));
                var spellsPath = Path.Combine(f.Data.Spells, PlayerUid.ToString(CultureInfo.InvariantCulture) + ".bin");
                var paid = f.Data.Files();
                var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath(PlayerUid));
                paid[characterKey] = ReplaceOwnedHawksMana(paid[characterKey]);
                var progressed = paid.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var spellKey = Path.GetRelativePath(f.Data.Root, spellsPath);
                var expectedSpells = (byte[])paid[spellKey].Clone();
                Assert.Equal(16, expectedSpells.Length);
                BinaryPrimitives.WriteUInt16LittleEndian(expectedSpells.AsSpan(6), 1);
                progressed[spellKey] = expectedSpells; // preserve all other12-byte fields, including byte11
                f.ActorFrames.Clear();
                bool sawProgress = false;
                int observed = 0;
                f.BeforeSend = (uid, bytes) =>
                {
                    Assert.Equal(PlayerUid, uid);
                    if (LegacySpellPacket.TryParse(ReadFrame(bytes), out _)) sawProgress = true;
                    f.Data.AssertFiles(sawProgress ? progressed : paid);
                    AssertOwnedHawksCurrent(f, before, clock, sawProgress);
                    observed++;
                };
                var request = new MsgAttackPacket(new MsgAttackQuery(PlayerUid, PlayerUid, 0, 0,
                    MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0,
                    SpellId: HawksAmbitionAttachStatusSpellId)).ToFrameBytes();
                Assert.Equal(OwnedHawksInteractionGolden(), request);
                try { await f.SendAsync(PlayerUid, request); }
                finally { f.BeforeSend = null; }
                Assert.True(sawProgress);
                Assert.Equal(f.ActorFrames.Count, observed);
                f.Data.AssertFiles(progressed);
                AssertOwnedHawksCurrent(f, before, clock, progressed: true);
                AssertOwnedHawksOriginalResponses(f.Current(PlayerUid), f.ActorFrames.ToArray(), f.Log.ToString());
            });
    }

    private static byte[] ReplaceOwnedHawksMana(byte[] before)
    {
        var text = Encoding.UTF8.GetString(before);
        const string from = "MinMana=500\r\n";
        int start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.Equal(-1, text.IndexOf(from, start + from.Length, StringComparison.Ordinal));
        return Encoding.UTF8.GetBytes(text[..start] + "MinMana=493\r\n" + text[(start + from.Length)..]);
    }

    private static void AssertOwnedHawksCurrent(OwnedEquipmentBehaviorFixture f, WorldSession before,
        HawksOwnedClock clock, bool progressed)
    {
        var current = f.Current(PlayerUid);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(f.Connection(PlayerUid).NativeGameClient, binding.Client);
        Assert.Equal(493L, current.Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
        Assert.Equal(before.Character.MinHitPoints, current.Character.MinHitPoints);
        Assert.Equal(checked((int)current.Character.MinHitPoints), binding.Player.HitPoints);
        Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), binding.Player.Stamina);
        Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)binding.Player.Action);
        var expectedScalars = CharacterSnapshotScalarPersistence.BuildPersistedValues(before.Character with { MinMana = 493 });
        var actualScalars = CharacterSnapshotScalarPersistence.BuildPersistedValues(current.Character);
        Assert.Equal(expectedScalars.OrderBy(pair => pair.Key), actualScalars.OrderBy(pair => pair.Key));
        Assert.Equal(before.Character.PaidNobility.OrderBy(pair => pair.Key), current.Character.PaidNobility.OrderBy(pair => pair.Key));
        Assert.Equal("35", current.Character.Attributes["HawksAmbitionPower"]);
        Assert.Equal("true", current.Character.Attributes["LegacyHawksAmbitionRemoveOnDead"]);
        Assert.Equal(clock.GetUtcNow().AddSeconds(33).ToString("O", CultureInfo.InvariantCulture),
            current.Character.Attributes["LegacyHawksAmbitionExpiresAtUtc"]);
        var expected = new PlayerSpellSnapshotEntry(15920, progressed ? (ushort)1 : (ushort)0, 0, 0, 0, 0);
        Assert.Equal(expected, Assert.Single(current.PlayerSpells.Spells));
        var native = Assert.Single(binding.Client.MySpells!.ClientSpells);
        Assert.Equal((ushort)15920, native.Key);
        Assert.Equal(expected.Level, native.Value.Level); Assert.Equal(0, native.Value.Experience);
        Assert.Equal((byte)0, native.Value.PreviousLevel); Assert.Equal((byte)0, native.Value.SoulLevel);
        Assert.Equal((byte)0, native.Value.UseSpellSoul);
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems), items);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemPath(PlayerUid)))), items);
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.Equal(PlayerProficiencySnapshotLoader.ToLegacyBytes(before.PlayerProficiencies),
            PlayerProficiencySnapshotLoader.ToLegacyBytes(current.PlayerProficiencies));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        // Observe only the lease issued by this accepted cast; no test grant.
        using var mutation = binding.EnterMutation();
        lock (binding.Client.SpellAdmissionSyncRoot)
        lock (binding.Player.SpellAdmissionSyncRoot)
        {
            Assert.True(WorldSessionStore.TryCaptureOwnedHawksStatus(binding, current, out var status));
            Assert.NotNull(status); Assert.True(status.IsCurrentPlain()); Assert.Equal(35U, status.Power);
            Assert.True(WorldSessionStore.TryCaptureOwnedHawksPower(binding, current, out var power));
            Assert.NotNull(power); Assert.True(power.IsCurrentPlain()); Assert.Equal(35U, power.Power);
        }
    }
}
