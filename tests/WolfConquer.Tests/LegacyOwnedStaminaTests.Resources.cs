using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData(50, 45, 57)] [InlineData(93, 88, 100)]
    public Task ActualOwnedBreathFocusCostsAndRefillPublishNativeAndStoreBeforeEachResourceReply(int initial, int debited, int restored) =>
        LegacyMagicAttackRuntimeTests.RestorationStaminaRun(Breath, initial.ToString(CultureInfo.InvariantCulture),
            blessed: false, omnipotence: false, staminaCost: 5, manaCost: 0, async f =>
        {
            const uint actor = OwnedEquipmentBehaviorFixture.Actor;
            var original = f.Current(actor); var native = original.NativeGameClient!; var files = f.Data.Files();
            var character = File.ReadAllBytes(f.CharacterPath(actor)); var items = File.ReadAllBytes(f.ItemPath(actor));
            var spellsPath = Path.Combine(f.Data.Spells, actor.ToString(CultureInfo.InvariantCulture) + ".bin");
            var expectedSpells = File.ReadAllBytes(spellsPath); BinaryPrimitives.WriteInt32LittleEndian(expectedSpells.AsSpan(8), 1000);
            var observed = new List<ulong>(); int first = f.ActorFrames.Count;
            Assert.Equal((ushort)initial, native.Player!.Stamina);
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != actor || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update)) return;
                var stamina = update.Updates.SingleOrDefault(row => row.Type == LegacyMsgUpdateDataType.Stamina);
                if (stamina.Type != LegacyMsgUpdateDataType.Stamina) return;
                observed.Add(stamina.Value); Assert.Equal(stamina.Value, (ulong)native.Player.Stamina);
                Assert.Equal(stamina.Value.ToString(CultureInfo.InvariantCulture), f.Current(actor).Character.Attributes["Stamina"]);
                Assert.Equal(character, File.ReadAllBytes(f.CharacterPath(actor))); Assert.Equal(items, File.ReadAllBytes(f.ItemPath(actor)));
            };
            try { await f.SendAsync(actor, BreathRequest(actor)); } finally { f.BeforeSend = null; }
            Assert.Equal(new ulong[] { checked((ulong)debited), checked((ulong)restored) }, observed);
            Assert.Equal((ushort)restored, native.Player.Stamina); Assert.Equal(restored.ToString(CultureInfo.InvariantCulture), f.Current(actor).Character.Attributes["Stamina"]);
            Assert.Equal(expectedSpells, File.ReadAllBytes(spellsPath)); files[Path.GetRelativePath(f.Data.Root, spellsPath)] = expectedSpells; f.Data.AssertFiles(files);
            var animation = f.ActorFrames.Skip(first).Select((bytes, index) => (bytes, index)).Single(item =>
                LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(item.bytes), out _));
            var resources = f.ActorFrames.Skip(first).Select((bytes, index) => (bytes, index)).Where(item =>
                LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(item.bytes), out var packet) &&
                packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina)).ToArray();
            Assert.All(resources, item => Assert.True(item.index < animation.index));
            Assert.Equal(1000, Assert.Single(f.Current(actor).PlayerSpells.Spells).Experience);
            if (restored < 100)
            {
                int count = f.ActorFrames.Count; await f.Handler.SendDueLegacyStaminaUpdatesAsync(CancellationToken.None);
                Assert.Equal(count + 1, f.ActorFrames.Count); Assert.Equal((ushort)(restored + 5), native.Player.Stamina);
                Assert.Equal((restored + 5).ToString(CultureInfo.InvariantCulture), f.Current(actor).Character.Attributes["Stamina"]); f.Data.AssertFiles(files);
            }
        });

    [Fact]
    public Task ActualOwnedCastWithInsufficientStaminaLeavesNativeStoreCooldownsAndFilesUntouched() =>
        LegacyMagicAttackRuntimeTests.RestorationStaminaRun(Breath, "4", false, false, 5, 0, async f =>
        {
            const uint actor = OwnedEquipmentBehaviorFixture.Actor;
            var before = f.Current(actor); var files = f.Data.Files(); int count = f.ActorFrames.Count;
            Assert.Equal((ushort)4, before.NativeGameClient!.Player!.Stamina);
            await f.SendAsync(actor, BreathRequest(actor));
            Assert.Same(before, f.Current(actor)); Assert.Equal((ushort)4, before.NativeGameClient.Player.Stamina);
            Assert.Null(f.Current(actor).MagicCooldowns); Assert.Equal(count, f.ActorFrames.Count); f.Data.AssertFiles(files);
        });

    private static byte[] BreathRequest(uint actor) => new MsgAttackPacket(new MsgAttackQuery(
        Uid: actor, OpponentUid: 0, X: 830, Y: 584, AttackType: MsgAttackType.Magic,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: Breath)).ToFrameBytes();
}
