using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPromotionEpicAtomicCommitTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "wolf-epic-atomic-" + Guid.NewGuid().ToString("N"));
        internal readonly WorldSessionStore Sessions = new();
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [];
        internal readonly uint Npc;
        internal readonly ushort Spell;
        internal readonly ConquerTcpConnection Connection;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Session;
        internal Action? BeforeFirstSend;
        internal string Spells => Context.PlayerSpellsRoot!;
        internal string CharacterPath => Path.Combine(Session.UsersRoot!, Session.AccountId + ".ini");
        internal string SpellPath => Path.Combine(Spells, Session.Character.Uid + ".bin");
        internal WorldSession Current => Assert.IsType<WorldSession>(Sessions.TryGet(Connection, out var s) ? s : null);
        internal Fixture(bool ninja, bool learned = false, long cp = 3_000)
        {
            Npc = ninja ? 4_972U : 8_314U; Spell = ninja ? (ushort)12_090 : (ushort)12_550;
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
            {
                if (Sent.Count == 0) BeforeFirstSend?.Invoke();
                Sent.Add(bytes.ToArray()); return ValueTask.CompletedTask;
            });
            var character = CharacterSnapshotParser.ParseText(FormattableString.Invariant($$"""
                [Character]
                UID=1293600
                Name=EpicOwner
                Body=1003
                Face=0
                Haire=0
                Class={{(ninja ? 5005 : 6004)}}
                FirstClass={{(ninja ? 5005 : 6004)}}
                SecoundeClass=0
                Reborn={{(ninja ? 2 : 0)}}
                Level=140
                Experience=0
                Map=1002
                X=430
                Y=380
                PMap=1002
                PMapX=430
                PMapY=380
                Strength=0
                Agility=0
                Vitaliti=0
                Spirit=0
                Atributes=0
                MinHitPoints=1
                MinMana=0
                MaxMana=0
                Money=7654
                ConquerPoints={{cp}}
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=0
                AtomicEpicSentinel=preserved
                """));
            var session = new WorldSession(301, "epic-owner", character, PlayerItemSnapshot.Empty,
                learned ? new([new PlayerSpellSnapshotEntry(Spell, 0, 0, 0, 0, 0)]) : PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty);
            Sessions.Set(Connection, session);
            Context = LegacySpellProducerTestOwner.PrepareContext(new(Connection, Log, session, Sessions: Sessions,
                PlayerSpellsRoot: Path.Combine(Root, "Spells"), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13), ref session);
            Session = session;
        }
        internal async Task InvokeAsync()
        {
            Assert.True(await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(Context, Session, Npc, 83, CancellationToken.None));
        }
        public void Dispose()
        {
            Log.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
