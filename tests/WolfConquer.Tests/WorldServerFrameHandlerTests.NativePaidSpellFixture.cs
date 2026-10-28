using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public enum NativePaidSpellCase { Mount, Taoist, Trojan }

public sealed partial class WorldServerFrameHandlerTests
{
    internal static async Task WithNativePaidSpellCallerAsync(NativePaidSpellCase kind, bool funded,
        Func<NativeSpellJoinFixture, Task> body)
    {
        var fixture = new NativeSpellJoinFixture(); Exception? primary = null;
        try { fixture.InitializePaidSpellCaller(kind, funded); await body(fixture); }
        catch (Exception error) { fixture.RetainRoot = true; primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"Paid caller fixture retained at {fixture.Root}.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) throw new AggregateException($"Paid caller fixture retained at {fixture.Root}.", primary);
    }

    internal sealed partial class NativeSpellJoinFixture
    {
        internal WorldDataCatalog PaidCallerWorldData => admission!.WorldData;

        internal void InitializePaidSpellCaller(NativePaidSpellCase kind, bool funded)
        {
            ValidateRoot(); Assert.False(Directory.Exists(Root) || File.Exists(Root));
            Directory.CreateDirectory(Users); ownsRoot = true; Directory.CreateDirectory(Spells);
            WriteSnapshot(Users, Uid, uid: Uid, classCode: kind == NativePaidSpellCase.Taoist ? 14052 : 1052,
                level: 110, reborn: 0, secondClass: 0, money: funded ? 150000 : 10,
                conquerPoints: funded ? 5000 : 1000, boundConquerPoints: 300, minHitPoints: 1000, name: "OwnedPaidSpellOwner");
            File.AppendAllText(CharacterPath, "\n[OwnedOpaque]\nKeep=paid-handler-fixture\n");
            SavedSpellsBefore = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(6001, 4, 1111, 2, 3, 2)]));
            SavedSpellsBefore[15] = 0xA5; File.WriteAllBytes(SpellPath, SavedSpellsBefore);
            var baseline = OrdinaryLoginFixture.Catalog();
            var metadata = baseline.Items.ToList();
            if (kind == NativePaidSpellCase.Trojan)
            {
                const uint itemId = 614000, itemUid = 900001;
                var item = new PlayerItemSnapshotItem { Uid = itemUid, ItemId = itemId, Position = 4,
                    Durability = 321, MaximumDurability = 321, StackSize = 1 };
                var itemsRoot = Path.Combine(Root, "PlayersItems"); Directory.CreateDirectory(itemsRoot);
                File.WriteAllBytes(Path.Combine(itemsRoot, Uid + ".bin"),
                    PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([], [], [], [item], [])));
                var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = "614000"; fields[1] = "OwnedTrojanEpicWeapon";
                fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1";
                metadata.Add(new ItemType((int)itemId, fields[1], fields));
            }
            var catalog = NativeCallerWorldDataFixture.Create(metadata, [], classLevelStats: baseline.ClassLevelStats,
                magicTypeOperations: baseline.MagicTypeOperations);
            beasts = new NativeLoginBeastMetadataScope(1000);
            // One genuine registry/bootstrap captures these exact files. No
            // saved owner, grant, UID receipt or Store acceptance is injected.
            admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(Users), catalog);
            Handler = admission.CreateHandler(Log); Native = new LegacyNativeGameClientRuntime();
            Assert.Same(Native, Native.Player.Owner); Assert.Null(Native.Socket);
            Connection = NewConnection(Native);
        }
    }
}
