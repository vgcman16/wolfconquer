using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShockRetaliationSenderReplacementOrSameRecordReissueStopsOldHpAndProgress(bool sameRecordReissue)
    {
        using var f = new ShockRetaliationOwnerFixture();
        var before = f.SpellBytes();
        WorldSession? replacement = null;
        var ownCount = 0;
        var targetCount = 0;
        f.OnSend = bytes =>
        {
            if (replacement is not null || !f.IsRetaliation(bytes)) return;
            var committed = f.Current;
            Assert.Equal(4_999L, committed.Character.MinHitPoints);
            Assert.Equal(160L, committed.Character.Experience);
            Assert.Equal((ushort)0, Assert.Single(committed.PlayerSpells.Spells).Level);
            if (sameRecordReissue)
            {
                Assert.True(f.Sessions.TryRemove(f.Connection, out _));
                replacement = committed; // Exact same object/UID/account, newly issued holder.
            }
            else replacement = committed with { AccountId = committed.AccountId + 1 };
            f.Sessions.Set(f.Connection, replacement);
            ownCount = f.Sent.Count;
            targetCount = f.TargetSent.Count;
        };

        await f.CastAsync();

        Assert.NotNull(replacement);
        Assert.Same(replacement, f.Current);
        Assert.Equal(ownCount, f.Sent.Count);
        Assert.Equal(targetCount, f.TargetSent.Count);
        Assert.Equal(before, f.SpellBytes());
        Assert.Empty(ParseLegacySpellPackets(f.Sent));
        Assert.DoesNotContain(f.Sent.Select(ReadFrame), f.IsCastAnimation);
    }

    [Fact]
    public async Task ShockRetaliationSameIssuancePublicationRetainsActualHpExperienceAndSavedProgress()
    {
        using var f = new ShockRetaliationOwnerFixture();
        var entered = false;
        f.OnSend = bytes =>
        {
            if (entered || !f.IsRetaliation(bytes)) return;
            entered = true;
            var committed = f.Current;
            Assert.Equal(4_999L, committed.Character.MinHitPoints);
            Assert.Equal(160L, committed.Character.Experience);
            f.Sessions.Set(f.Connection, committed with { Character = committed.Character with { Money = 123 } });
        };

        await f.CastAsync();

        Assert.True(entered, f.Output);
        Assert.Equal(4_999L, f.Current.Character.MinHitPoints);
        Assert.Equal(160L, f.Current.Character.Experience);
        Assert.Equal(123L, f.Current.Character.Money);
        Assert.Equal((ushort)1, Assert.Single(f.Current.PlayerSpells.Spells).Level);
        Assert.Equal(Assert.Single(f.Current.PlayerSpells.Spells), Assert.Single(f.LoadSaved().Spells));
        Assert.Single(ParseLegacySpellPackets(f.Sent));
        Assert.Single(f.Sent.Select(ReadFrame), f.IsCastAnimation);
    }

    [Fact]
    public async Task ShockProgressSenderReissueRetainsCommittedFileAndStopsFollowingAnimation()
    {
        using var f = new ShockRetaliationOwnerFixture();
        WorldSession? reissued = null;
        var ownCount = 0;
        var targetCount = 0;
        f.OnSend = bytes =>
        {
            if (reissued is not null || !LegacySpellPacket.TryParse(ReadFrame(bytes), out var spell) ||
                spell.Id != CrackingShockSpellId) return;
            reissued = f.Current;
            Assert.Equal((ushort)1, Assert.Single(reissued.PlayerSpells.Spells).Level);
            Assert.Equal(Assert.Single(reissued.PlayerSpells.Spells), Assert.Single(f.LoadSaved().Spells));
            Assert.True(f.Sessions.TryRemove(f.Connection, out _));
            f.Sessions.Set(f.Connection, reissued);
            ownCount = f.Sent.Count;
            targetCount = f.TargetSent.Count;
        };

        await f.CastAsync();

        Assert.NotNull(reissued);
        Assert.Same(reissued, f.Current);
        Assert.Equal(ownCount, f.Sent.Count);
        Assert.Equal(targetCount, f.TargetSent.Count);
        Assert.Equal(Assert.Single(reissued.PlayerSpells.Spells), Assert.Single(f.LoadSaved().Spells));
        Assert.Single(ParseLegacySpellPackets(f.Sent));
        Assert.DoesNotContain(f.Sent.Select(ReadFrame), f.IsCastAnimation);
    }

    [Fact]
    public void ShockCapturedFreeWriterRejectsActualSameRecordAbaBeforeOwnerCapture()
    {
        using var f = new ShockRetaliationOwnerFixture();
        var original = f.Current;
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Sessions, f.Connection, PlayerUid, out var identity));
        var before = f.SpellBytes();
        Assert.True(f.Sessions.TryRemove(f.Connection, out _));
        f.Sessions.Set(f.Connection, original);
        var proposed = new PlayerSpellSnapshot(new PlayerSpellSnapshotEntry[]
            { Assert.Single(original.PlayerSpells.Spells) with { Level = 1 } });
        object?[] args = [f.Context, original, proposed, identity, CancellationToken.None, null, null];
        var writer = typeof(WorldPacketHandlers).GetMethod("TryCommitLegacyFreeSpellsForCapturedIdentity",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.False((bool)writer.Invoke(null, args)!);

        Assert.Same(original, f.Current);
        Assert.Same(original, args[5]);
        Assert.Equal("spellMutationOriginalIssuanceChanged", args[6]);
        Assert.Equal(before, f.SpellBytes());
        Assert.Empty(f.Sent);
    }

    private sealed class ShockRetaliationOwnerFixture : IDisposable
    {
        private readonly StringWriter log = new();
        private readonly LegacyCombatSpellTestOwner owner;
        private readonly LegacyMonsterInstance monster;
        internal readonly WorldSessionStore Sessions = new();
        internal readonly List<byte[]> Sent = new(), TargetSent = new();
        internal readonly ConquerTcpConnection Connection;
        internal readonly WorldPacketHandlerContext Context;
        internal Action<byte[]>? OnSend;
        internal string Output => log.ToString();
        internal WorldSession Current => Assert.IsType<WorldSession>(Sessions.TryGet(Connection, out var current) ? current : null);

        internal ShockRetaliationOwnerFixture()
        {
            var family = CreateHighLifeMonsterFamily();
            family = family with { Attributes = new Dictionary<string, string>(family.Attributes) { ["Boss"] = "1" } };
            var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
            var world = CreateMagicCatalogCore(family, [spawn], [], magicRows:
            [CreateCrackingShockDebtRow(0, 1, 30_095), CreateCrackingShockDebtRow(1, 200, 30_095)]);
            monster = CreateRuntimeMonsterInstance(world, spawn, family);
            var caster = CreateSession(strength: 300, spirit: 0, minMana: 50, stamina: 100,
                classCode: 9_001, spellEntries: [new(CrackingShockSpellId, 0, 0, 0, 2, 2)]);
            var target = CreateSession(uid: TargetPlayerUid, name: "ShockOwnerTarget", x: 842, y: 584,
                minHitPoints: 100_000, spellIds: [], attributes: new Dictionary<string, string>
                { ["MaxHitpoints"] = "100000", ["LegacySpeffectLevel.Solid"] = "4", ["LegacySpeffectRoll.Solid"] = "0" });
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray(); Sent.Add(bytes); OnSend?.Invoke(bytes); return ValueTask.CompletedTask;
            });
            var targetConnection = CreateSendingConnection(TargetSent);
            Sessions.Set(Connection, caster);
            Sessions.Set(targetConnection, target);
            owner = new LegacyCombatSpellTestOwner(new WorldPacketHandlerContext(Connection, log, caster,
                WorldData: world, Sessions: Sessions, MonsterStates: new MonsterRuntimeStateStore(),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13), ref caster);
            Context = owner.Context;
        }

        internal bool IsRetaliation(byte[] bytes) => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.AttackerUid == monster.Uid && packet.SpellId == 9_999;
        internal bool IsCastAnimation(PacketFrame frame) => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.AttackerUid == PlayerUid && packet.SpellId == CrackingShockSpellId;
        internal Task CastAsync() => PacketHandlers.HandleAsync(Context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 836, Y: 584, AttackType: MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
            SpellId: CrackingShockSpellId)).ToFrameBytes())).AsTask();
        internal PlayerSpellSnapshot LoadSaved() => new PlayerSpellSnapshotLoader(Context.PlayerSpellsRoot!).LoadByCharacterUid(PlayerUid);
        internal byte[] SpellBytes() => File.ReadAllBytes(Path.Combine(Context.PlayerSpellsRoot!, PlayerUid + ".bin"));
        public void Dispose() { try { owner.Dispose(); } finally { log.Dispose(); } }
    }
}
