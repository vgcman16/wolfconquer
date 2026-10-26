using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("slot")]
    [InlineData("player")]
    [InlineData("uid")]
    [InlineData("class")]
    [InlineData("dictionary")]
    [InlineData("row-reference")]
    [InlineData("row-key")]
    [InlineData("row-item-id")]
    [InlineData("row-level")]
    [InlineData("row-position")]
    [InlineData("row-exp")]
    [InlineData("row-uk")]
    [InlineData("row-added")]
    [InlineData("flag")]
    [InlineData("Exp")]
    [InlineData("Fire")]
    [InlineData("Water")]
    [InlineData("Earth")]
    [InlineData("Wind")]
    [InlineData("Lightning")]
    [InlineData("tmp_Fire")]
    [InlineData("tmp_Water")]
    [InlineData("tmp_Earth")]
    [InlineData("tmp_Wind")]
    [InlineData("tmp_Lightning")]
    [InlineData("FireMastery")]
    [InlineData("WaterMastery")]
    [InlineData("EarthMastery")]
    [InlineData("WindMastery")]
    [InlineData("LightningMastery")]
    [InlineData("UnlockMastery")]
    public void MailboxPointsPreparedCaptureRefusesEveryRelevantOwnerOrValueDrift(string fault)
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        Assert.True(ninja.TryPrepareMailboxPoints(client, MailboxNinjaLine, 50, out var projection));
        DriftMailboxNinja(client, ninja, fault);
        var before = MailboxNinjaState(ninja);
        Assert.False(projection!.IsCurrent());
        Assert.False(projection.TryPublishCommitted());
        Assert.False(projection.IsPublishedCurrent());
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }

    [Theory]
    [InlineData("unowned")]
    [InlineData("foreign-user")]
    [InlineData("missing-slot")]
    [InlineData("foreign-player")]
    [InlineData("zero-uid")]
    [InlineData("wrong-class")]
    [InlineData("key-mismatch")]
    [InlineData("null-items")]
    [InlineData("null-row")]
    [InlineData("zero-key")]
    public void MailboxPointsRequiresGenuineConstructorAndCurrentReciprocalNativeOwners(string fault)
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        switch (fault)
        {
            case "unowned": ninja = new LegacyNativeNinja { user = client }; ninja.Load(MailboxNinjaLine); client.MyNinja = ninja; break;
            case "foreign-user": ninja.user = Client(); break;
            case "missing-slot": client.MyNinja = null; break;
            case "foreign-player": client.Player = Client().Player; break;
            case "zero-uid": client.Player.UID = 0; break;
            case "wrong-class": Class(client, 7051); break;
            case "key-mismatch": ninja.Items.TryRemove(100, out var row); ninja.Items[101] = row!; break;
            case "null-items": ninja.Items = null!; break;
            case "null-row": ninja.Items[300] = null!; break;
            case "zero-key": ninja.Items[0] = new() { ItemID = 0 }; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        var before = MailboxNinjaState(ninja);
        Assert.False(ninja.TryPrepareMailboxPoints(client, MailboxNinjaLine, 50, out var projection));
        Assert.Null(projection);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }

    private static void DriftMailboxNinja(LegacyNativeGameClientRuntime client, LegacyNativeNinja ninja, string fault)
    {
        var row = ninja.Items[100];
        switch (fault)
        {
            case "user": ninja.user = Client(); break;
            case "slot": client.MyNinja = new(client); break;
            case "player": client.Player = new(client); break;
            case "uid": client.Player.UID++; break;
            case "class": Class(client, 5050); break;
            case "dictionary": ninja.Items = new ConcurrentDictionary<uint, LegacyNativeNinja.Item>(ninja.Items); break;
            case "row-reference": ninja.Items[100] = new() { ItemID = row.ItemID, Level = row.Level, Position = row.Position, Exp = row.Exp, UK = row.UK }; break;
            case "row-key": ninja.Items.TryRemove(100, out _); ninja.Items[101] = row; break;
            case "row-item-id": row.ItemID++; break;
            case "row-level": row.Level++; break;
            case "row-position": row.Position++; break;
            case "row-exp": row.Exp++; break;
            case "row-uk": row.UK++; break;
            case "row-added": ninja.Items[300] = new() { ItemID = 300 }; break;
            case "flag": ninja.Flag++; break;
            default:
                var property = typeof(LegacyNativeNinja).GetProperty(fault, BindingFlags.Instance | BindingFlags.Public)!;
                property.SetValue(ninja, checked((uint)property.GetValue(ninja)! + 1));
                break;
        }
    }
}
