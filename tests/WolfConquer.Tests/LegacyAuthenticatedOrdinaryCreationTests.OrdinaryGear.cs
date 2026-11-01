using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal static Task RunOrdinaryGearLoadedScenarioAsync(string scenario, LegacyNativeCombatGearRuntime? runtime,
        IReadOnlyList<CombatGear> catalogGear, IReadOnlyList<DaoqiDictType> catalogJade) => Run(async f =>
    {
        var uid = CreationFixture.RuneActor;
        var path = Path.Combine(f.Data.CombatGear, uid + ".ini");
        var image = File.ReadAllBytes(path); var uidState = File.ReadAllBytes(f.Data.State);
        var allBefore = f.Data.Files();
        var connection = f.NewConnection(uid);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
        if (scenario == "OrdinaryGearUnknown")
        {
            Assert.False(f.Store.TryGet(connection, out _));
            Assert.Null(client.CombatGear); Assert.Null(client.OrdinarySpellBinding);
            f.Data.AssertFiles(allBefore);
            return;
        }
        var session = f.CurrentFor(connection);
        Assert.Equal(image, Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(session.CombatGear!)));
        Assert.Equal(image, File.ReadAllBytes(path)); Assert.Equal(uidState, File.ReadAllBytes(f.Data.State));
        if (scenario is "OrdinaryGearMissingTables" or "OrdinaryGearSchema22Refused")
        {
            Assert.Null(client.CombatGear);
            var heldBinding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(heldBinding));
            Assert.Equal(1L, session.Character.Online);
            Assert.Contains("native CombatGear held", f.Log.ToString());
            // Successful ordinary admission persists the independently known
            // Online transition even though native populated gear stays held.
            var expected = allBefore.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var characterKey = Path.GetRelativePath(f.Data.Root, f.Data.UserPath);
            var text = CharacterSnapshotFileEncoding.Decode(expected[characterKey], out var encoding);
            const string beforeOnline = "\r\nOnline=0\r\n";
            Assert.Contains(beforeOnline, text, StringComparison.Ordinal);
            Assert.Equal(text.IndexOf(beforeOnline, StringComparison.Ordinal), text.LastIndexOf(beforeOnline, StringComparison.Ordinal));
            text = text.Replace(beforeOnline, "\r\nOnline=1\r\n", StringComparison.Ordinal);
            expected[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            f.Data.AssertFiles(expected);
            Assert.False(f.Runtime.Recovery.IsBlocked);
            return;
        }
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
        Assert.NotNull(runtime);
        Assert.True(runtime.IsCurrent);
        Assert.True(gear.IsCombatOwnedBy(client)); Assert.True(gear.IsAdmissionOriginCurrent(binding, session));
        Assert.Equal(23U, gear.MasteryPoints); Assert.Equal(11U, gear.TaoistReiki);
        Assert.Equal(17U, gear.PirateExp); Assert.Equal(19U, gear.AirPowers.Points);
        var nativeRow = Assert.Single(gear.Items).Value;
        var savedRow = Assert.Single(session.CombatGear!.Items);
        Assert.Equal(savedRow.ItemID, (uint)nativeRow.ItemID); Assert.Equal(savedRow.Level, nativeRow.Level);
        Assert.Equal(savedRow.Exp, nativeRow.Exp); Assert.Equal(savedRow.Equip, nativeRow.Equip);
        Assert.Equal(savedRow.Transform, nativeRow.Transform); Assert.Equal(savedRow.MasteryScore, nativeRow.MasteryScore);
        for (int index = 0; index < 2; index++) Assert.Equal(savedRow.Animas[index], nativeRow.TypeAnima[index]);
        Assert.Equal(savedRow.BlackJades, nativeRow.BlackJades.Select(row => row.Type));
        Assert.Equal(session.CombatGear.BlackJadeBag.BlackJades.Order(), gear.BlackJadeBag.Keys.Order());
        nativeRow.TypeAnima[1][6]++;
        Assert.False(gear.IsAdmissionOriginCurrent(binding, session));
        nativeRow.TypeAnima[1][6]--;
        Assert.True(gear.IsAdmissionOriginCurrent(binding, session));
        var fields = Assert.IsType<string[]>(catalogGear[0].Fields);
        var originalField = fields[4];
        try
        {
            fields[4] = (int.Parse(originalField, System.Globalization.CultureInfo.InvariantCulture) + 1)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.False(gear.IsAdmissionOriginCurrent(binding, session));
        }
        finally { fields[4] = originalField; }
        Assert.True(gear.IsAdmissionOriginCurrent(binding, session));
        Assert.Equal(image, File.ReadAllBytes(path)); Assert.Equal(uidState, File.ReadAllBytes(f.Data.State));
    }, beforeBootstrap: f =>
    {
        f.ConfigureNativeGear(runtime, catalogGear, catalogJade);
        var row = new CombatGearSnapshotItem(scenario == "OrdinaryGearUnknown" ? uint.MaxValue : 701U,
            2, 7, 1, 3, 9, [new uint[] { 1, 2, 3, 4, 5, 6, 7 }, new uint[] { 8, 9, 10, 11, 12, 13, 14 }],
            new uint[] { 9001, 0, 0, 0, 0, 0 });
        var saved = CombatGearSnapshot.Empty with { MasteryPoints = 23, TaoistReiki = 11, PirateExp = 17,
            Count = 1, AirPowers = CombatGearAirPowersSnapshot.Empty with { Points = 19 }, Items = [row],
            BlackJadeBag = new(1, new uint[] { 9002 }) };
        new CombatGearSnapshotLoader(f.Data.CombatGear).SaveByCharacterUid(CreationFixture.RuneActor, saved);
    }, enter: false);

    [Fact]
    public Task ExistingSavedEmptyGearAdoptsActualConstructorOwnerWithoutAllocatingItemUid() => Run(async f =>
    {
        var uid = CreationFixture.RuneActor;
        var path = Path.Combine(f.Data.CombatGear, uid + ".ini");
        var image = File.ReadAllBytes(path);
        var uidState = File.ReadAllBytes(f.Data.State);
        var connection = f.NewConnection(uid);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
        var session = f.CurrentFor(connection);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
        Assert.True(gear.IsCombatOwnedBy(client));
        Assert.True(gear.IsAdmissionOriginCurrent(binding, session));
        Assert.Equal((uint)session.Character.Class, gear.GetClass());
        Assert.Equal(23U, gear.MasteryPoints); Assert.Equal(11U, gear.TaoistReiki);
        Assert.Equal(17U, gear.PirateExp); Assert.Equal(19U, gear.AirPowers.Points);
        Assert.Empty(gear.Items); Assert.Empty(gear.BlackJadeBag);
        Assert.Equal(image, Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(session.CombatGear!)));
        Assert.Equal(image, File.ReadAllBytes(path)); Assert.Equal(uidState, File.ReadAllBytes(f.Data.State));
        gear.AirPowers.Points++;
        Assert.False(gear.IsAdmissionOriginCurrent(binding, session));
        gear.AirPowers.Points--;
        Assert.True(gear.IsAdmissionOriginCurrent(binding, session));
        var original = client.Player;
        try
        {
            client.Player = new LegacyNativePlayerRuntime(client);
            Assert.False(gear.IsAdmissionOriginCurrent(binding, session));
        }
        finally { client.Player = original; }
        Assert.Equal(image, File.ReadAllBytes(path)); Assert.Equal(uidState, File.ReadAllBytes(f.Data.State));
    }, beforeBootstrap: f =>
    {
        var gear = CombatGearSnapshot.Empty with { MasteryPoints = 23, TaoistReiki = 11, PirateExp = 17,
            AirPowers = CombatGearAirPowersSnapshot.Empty with { Points = 19 } };
        new CombatGearSnapshotLoader(f.Data.CombatGear).SaveByCharacterUid(CreationFixture.RuneActor, gear);
    }, enter: false);

    [Fact]
    public Task AbsentGearImageDoesNotMintAPersistedNativeGearOwner() => Run(async f =>
    {
        var uid = CreationFixture.RuneActor;
        var uidState = File.ReadAllBytes(f.Data.State);
        var connection = f.NewConnection(uid);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
        var session = f.CurrentFor(connection);
        Assert.NotNull(session.NativeGameClient!.OrdinarySpellBinding);
        Assert.Null(session.NativeGameClient.CombatGear);
        Assert.False(File.Exists(Path.Combine(f.Data.CombatGear, uid + ".ini")));
        Assert.Equal(uidState, File.ReadAllBytes(f.Data.State));
    }, beforeBootstrap: f => File.Delete(Path.Combine(f.Data.CombatGear, CreationFixture.RuneActor + ".ini")), enter: false);

    [Fact]
    public Task MalformedSavedGearRefusesBeforeAdmissionWithoutAllocation() => Run(async f =>
    {
        var uid = CreationFixture.RuneActor;
        var before = f.Data.Files();
        var connection = f.NewConnection(uid);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
        Assert.False(f.Store.TryGet(connection, out _));
        Assert.Null(connection.NativeGameClient!.CombatGear);
        Assert.Null(connection.NativeGameClient.OrdinarySpellBinding);
        Assert.DoesNotContain(f.Frames(uid), bytes => LegacyMsgMessagePacket.TryParse(CreationFixture.ReadFrame(bytes), out var packet) &&
            packet.Content.Any(value => value.Contains("ANSWER_OK", StringComparison.OrdinalIgnoreCase)));
        f.Data.AssertFiles(before);
        Assert.False(f.Runtime.Recovery.IsBlocked);
    }, beforeBootstrap: f =>
    {
        var path = Path.Combine(f.Data.CombatGear, CreationFixture.RuneActor + ".ini");
        File.WriteAllText(path, "[CombatGear]\r\nCount=1\r\n[BlackJadeBag]\r\nCount=0\r\n");
    }, enter: false);
}
