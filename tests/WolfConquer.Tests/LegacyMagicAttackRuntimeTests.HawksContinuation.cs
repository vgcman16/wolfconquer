using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static Task RunOwnedHawksAnimationEofAsync()
    {
        // The existing declared compatibility rows are power35/duration33,
        // Mana7. This tests the real caster, not retained catalog provenance.
        var catalog = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 0,
                experience: 50, damage: 35, duration: 33, coldTime: 0),
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 1,
                experience: 200, damage: 35, duration: 33, coldTime: 0));
        var actor = CreateSession(spellIds: [HawksAmbitionAttachStatusSpellId]);
        var peer = CreateSession(uid: TargetPlayerUid, name: "HawksViewer", x: actor.Character.X + 1,
            y: actor.Character.Y, spellIds: []);
        var clock = new HawksOwnedClock();
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(
            new(actor.Character, actor.PlayerItems, actor.PlayerSpells, actor.PlayerProficiencies), catalog,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                var before = f.Current(PlayerUid); var viewer = f.Current(TargetPlayerUid);
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
                var viewerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(viewer.NativeGameClient!.OrdinarySpellBinding);
                var spellMap = binding.Storage.ClientSpells; var viewerImage = viewerBinding.Items.Image;
                var files = f.Data.Files();
                var paid = files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath(PlayerUid));
                paid[path] = ReplaceOwnedHawksMana(paid[path]);
                f.ActorFrames.Clear(); f.Frames(TargetPlayerUid).Clear();
                Task? close = null; WorldSession? committed = null; int attempted = 0;
                var observed = new List<(uint Recipient, byte[] Bytes)>();
                f.BeforeSend = (uid, bytes) =>
                {
                    f.Data.AssertFiles(paid); AssertOwnedHawksCurrent(f, before, clock, progressed: false);
                    Assert.Same(viewer, f.Current(TargetPlayerUid)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(viewerBinding));
                    Assert.Same(viewerImage, viewerBinding.Items.Image);
                    Assert.Equal(checked((int)viewer.Character.MinHitPoints), viewerBinding.Player.HitPoints);
                    Assert.Equal(checked((uint)viewer.Character.MinMana), viewerBinding.Player.Mana);
                    Assert.Equal(LegacyStaminaState.RequireCurrent(viewer.Character), viewerBinding.Player.Stamina);
                    Assert.Equal(LegacyActionState.RequireCurrent(viewer.Character), (uint)viewerBinding.Player.Action);
                    observed.Add((uid, bytes.ToArray()));
                    if (uid != PlayerUid || !LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) ||
                        animation.SpellId != HawksAmbitionAttachStatusSpellId) return;
                    Assert.Null(close); attempted++; committed = f.Current(PlayerUid);
                    // Actual EOF is published synchronously. Await its cleanup
                    // only after this real Frame releases its shared lease.
                    close = f.Connection(PlayerUid).CompleteTransportCloseAsync();
                };
                var request = new MsgAttackPacket(new MsgAttackQuery(PlayerUid, PlayerUid, 0, 0,
                    MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0,
                    SpellId: HawksAmbitionAttachStatusSpellId)).ToFrameBytes();
                Assert.Equal(OwnedHawksInteractionGolden(), request);
                try { await f.SendAsync(PlayerUid, request); }
                finally { f.BeforeSend = null; }
                Assert.Equal(1, attempted); Assert.NotNull(committed); Assert.NotNull(close); await close!;
                var diagnostic = f.Log.ToString();
                Assert.Contains("WorldServer committed Hawks Ambition outcome; output interrupted", diagnostic);
                Assert.Contains(nameof(LegacyCommittedHawksOutputException), diagnostic);
                Assert.Contains("committedHawksAmbitionOutputOwnerLost", diagnostic);
                Assert.DoesNotContain("PARITY-BUG world runtime exception", diagnostic);
                f.Data.AssertFiles(paid); Assert.True(binding.IsRetired);
                Assert.False(f.Store.TryGet(f.Connection(PlayerUid), out _));
                Assert.Equal(493L, committed!.Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
                Assert.Equal("35", committed.Character.Attributes["HawksAmbitionPower"]);
                Assert.Equal((ushort)0, Assert.Single(committed.PlayerSpells.Spells).Level);
                Assert.Same(spellMap, binding.Storage.ClientSpells);
                Assert.Equal((ushort)0, Assert.Single(spellMap).Value.Level);
                Assert.Same(viewer, f.Current(TargetPlayerUid)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(viewerBinding));

                var flags = new List<(int Field, ulong Value)> { (1, 25) };
                for (int word = 0; word < 18; word++) flags.Add((3, word == 8 ? 1UL : 0UL));
                var vector = OwnedHawksUpdateGolden(flags.ToArray());
                var ownerFrames = f.ActorFrames.ToArray();
                Assert.Equal(5, ownerFrames.Length);
                Assert.Equal(OwnedHawksUpdateGolden((1, 2UL), (2, 493UL)), ownerFrames[0]);
                Assert.Equal(OwnedHawksInteractionGolden(), ownerFrames[1]); Assert.Equal(vector, ownerFrames[2]);
                Assert.Equal(OwnedHawksUpdateGolden((1, 120UL), (4, 256UL), (5, 33UL)), ownerFrames[3]);
                Assert.Equal(HawksContinuationAnimationGolden(), ownerFrames[4]);
                Assert.Equal(vector, Assert.Single(f.Frames(TargetPlayerUid)));
                Assert.Equal(new[] { PlayerUid, PlayerUid, PlayerUid, TargetPlayerUid, PlayerUid, PlayerUid },
                    observed.Select(row => row.Recipient));
                Assert.DoesNotContain(observed, row => LegacySpellPacket.TryParse(ReadFrame(row.Bytes), out _));
                Assert.DoesNotContain(observed, row => LegacyShowEquipmentPacket.TryParse(ReadFrame(row.Bytes), out _));
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
                int actorCount = f.ActorFrames.Count, viewerCount = f.Frames(TargetPlayerUid).Count;
                await f.SendAsync(PlayerUid, request); // Same closed connection cannot replay the committed cast.
                f.Data.AssertFiles(paid); Assert.Equal(actorCount, f.ActorFrames.Count);
                Assert.Equal(viewerCount, f.Frames(TargetPlayerUid).Count); Assert.Equal(493U, binding.Player.Mana);
                Assert.Same(viewer, f.Current(TargetPlayerUid));
            }, new OwnedEquipmentBehaviorInput(peer.Character, peer.PlayerItems, peer.PlayerSpells, peer.PlayerProficiencies));
    }

    private static byte[] HawksContinuationAnimationGolden()
    {
        var animation = new List<byte>();
        foreach (var pair in new (int, ulong)[] { (1, PlayerUid), (2, PlayerUid), (5, 15920), (6, 0), (7, 0), (8, 0), (9, 0) })
            OwnedHawksField(animation, pair.Item1, pair.Item2);
        var hit = new List<byte>();
        foreach (var pair in new (int, ulong)[] { (1, PlayerUid), (2, 0), (3, 1), (4, 0), (6, 0), (7, 0), (8, 0), (9, 0) })
            OwnedHawksField(hit, pair.Item1, pair.Item2);
        OwnedHawksBytes(animation, 10, hit);
        return OwnedHawksFrame(2239, animation.ToArray());
    }
}
