using System.Reflection;
using ProtoBuf.Meta;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using CollectionTable = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

// Explicit once-per-native-serial-collection TEST prerequisite, never a production bootstrap.
// Registering actual contracts does not set m_Load or imply file loading/runtime readiness.
public sealed class NativeCombatGearRegisteredTestContext
{
    public NativeCombatGearRegisteredTestContext()
    {
        bool gate = Table.m_Load;
        string location = Table.Location;
        var info = Table.Info; var jade = Jade.Info; var lottery = Lottery.Info;
        var collection = CollectionTable.Info; var ranks = Rank.Ranks;
        // Passive enumeration cannot infer/freeze a type; serialization must not precede the registrar.
        Assert.Empty(RuntimeTypeModel.Default.GetTypes().Cast<MetaType>());
        var registrar = typeof(Table).GetMethod("RegisterMessages", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(registrar);
        registrar.Invoke(null, null); // The genuine production registrar, once; no mirrored/fake model.
        var registered = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToArray();
        var contracts = registered.Where(contract => !contract.Type.IsEnum).ToArray();
        Assert.Equal(12, contracts.Length);
        Assert.Equal(67, contracts.Sum(contract => contract.GetFields().Length));
        Assert.All(contracts.SelectMany(contract => contract.GetFields()), field => Assert.True(field.IsRequired));
        Assert.False(Table.TryCaptureMailboxLoadProof(out _)); // Serialization/registration is not a completed data load.
        Assert.Equal(gate, Table.m_Load);
        Assert.Equal(location, Table.Location);
        Assert.Same(info, Table.Info); Assert.Same(jade, Jade.Info); Assert.Same(lottery, Lottery.Info);
        Assert.Same(collection, CollectionTable.Info); Assert.Same(ranks, Rank.Ranks);
        // Default deliberately remains registered for this parent process; it is never reset.
        // All cold/failure lifecycle controls run in fresh measured child processes instead.
    }
}
