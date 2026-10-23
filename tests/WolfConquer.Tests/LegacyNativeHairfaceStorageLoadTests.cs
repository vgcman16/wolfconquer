using System.Collections.Generic;
using WolfConquer.Core.World;
using Xunit;
using Table = WolfConquer.Core.World.LegacyNativeHairfaceStorageTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeHairfaceStorageLoadTests
{
    [Fact]
    public void KnownIdCopiesCatalogMetadataAndAliasesClassesButKeepsLoadedColors()
    {
        WithTable(table =>
        {
            var catalog = Row(100, LegacyHairFaceStorageType.Avatar);
            table.Add(catalog);
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("1/100/6/3/9/8/7/");

            var loaded = Assert.Single(storage.Objects);
            Assert.NotSame(catalog, loaded);
            Assert.Equal(100u, loaded.ID);
            Assert.Equal(catalog.Type, loaded.Type);
            Assert.Equal(catalog.Name, loaded.Name);
            Assert.Equal(catalog.RareLevel, loaded.RareLevel);
            Assert.Equal(catalog.RequiredVIPLevel, loaded.RequiredVIPLevel);
            Assert.Equal(catalog.Cost, loaded.Cost);
            Assert.Equal(catalog.Sex, loaded.Sex);
            Assert.Equal((byte)6, loaded.EquippedColor);
            Assert.Equal(new byte[] { 9, 8, 7 }, loaded.Colors);
            Assert.NotSame(catalog.Colors, loaded.Colors);
            Assert.Same(catalog.Classes, loaded.Classes);
            catalog.Classes!.Add(77);
            Assert.Equal(new byte[] { 1, 2, 77 }, loaded.Classes);
            catalog.Name = "Changed catalog";
            catalog.Cost = 0;
            catalog.Colors![0] = 0;
            Assert.Equal("Catalog 100", loaded.Name);
            Assert.Equal(123u, loaded.Cost);
            Assert.Equal((byte)9, loaded.Colors![0]);
        });
    }

    [Fact]
    public void UnknownIdConsumesItsCompleteColorPayloadBeforeBeingIgnored()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("2/999/5/4/10/20/30/40/100/6/2/7/8/");

            var loaded = Assert.Single(storage.Objects);
            Assert.Equal(100u, loaded.ID);
            Assert.Equal((byte)6, loaded.EquippedColor);
            Assert.Equal(new byte[] { 7, 8 }, loaded.Colors);
            Assert.False(storage.Exists(999, LegacyHairFaceStorageType.Hairstyle));
        });
    }

    [Fact]
    public void FirstCatalogRowWithIdWinsEvenWhenLaterRowHasAnotherType()
    {
        WithTable(table =>
        {
            var first = Row(100, LegacyHairFaceStorageType.Avatar);
            var second = Row(100, LegacyHairFaceStorageType.Hairstyle);
            second.Name = "Later duplicate";
            second.Classes = [99];
            table.Add(first);
            table.Add(second);
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("1/100/3/0/");

            var loaded = Assert.Single(storage.Objects);
            Assert.Equal(LegacyHairFaceStorageType.Avatar, loaded.Type);
            Assert.Equal(first.Name, loaded.Name);
            Assert.Same(first.Classes, loaded.Classes);
            Assert.True(storage.Exists(100, LegacyHairFaceStorageType.Avatar));
            Assert.False(storage.Exists(100, LegacyHairFaceStorageType.Hairstyle));
        });
    }

    [Fact]
    public void DuplicateIdAndTypeDoesNotReplaceFirstLoadedRecord()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("2/100/2/2/1/2/100/5/1/9/");

            var loaded = Assert.Single(storage.Objects);
            Assert.Equal((byte)2, loaded.EquippedColor);
            Assert.Equal(new byte[] { 1, 2 }, loaded.Colors);
        });
    }

    [Fact]
    public void SameIdWithChangedCatalogTypeAppendsBecauseDedupIncludesType()
    {
        WithTable(table =>
        {
            var catalog = Row(100);
            table.Add(catalog);
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            storage.Load("1/100/1/1/7/");
            var first = Assert.Single(storage.Objects);
            catalog.Type = LegacyHairFaceStorageType.Avatar;

            storage.Load("1/100/2/1/8/");

            Assert.Equal(2, storage.Objects.Length);
            Assert.Same(first, storage.Objects[0]);
            Assert.Equal(LegacyHairFaceStorageType.Hairstyle, first.Type);
            Assert.Equal(LegacyHairFaceStorageType.Avatar, storage.Objects[1].Type);
            Assert.True(storage.Exists(100, LegacyHairFaceStorageType.Hairstyle));
            Assert.True(storage.Exists(100, LegacyHairFaceStorageType.Avatar));
        });
    }

    [Fact]
    public void RepeatedLoadAppendsNewIdsWithoutResettingOrReplacingExistingRecords()
    {
        WithTable(table =>
        {
            var catalog = Row(100);
            table.Add(catalog);
            table.Add(Row(200));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            storage.Load("1/100/1/1/7/");
            var oldObjects = storage.Objects;
            var first = Assert.Single(oldObjects);
            catalog.Name = "New metadata";

            storage.Load("2/100/9/2/3/4/200/2/1/8/");

            Assert.Equal(2, storage.Objects.Length);
            Assert.NotSame(oldObjects, storage.Objects);
            Assert.Same(first, storage.Objects[0]);
            Assert.Same(first, Assert.Single(oldObjects));
            Assert.Equal("Catalog 100", first.Name);
            Assert.Equal((byte)1, first.EquippedColor);
            Assert.Equal(new byte[] { 7 }, first.Colors);
            Assert.Equal(200u, storage.Objects[1].ID);
            Assert.Equal(new byte[] { 8 }, storage.Objects[1].Colors);
        });
    }

    [Fact]
    public void MalformedNumbersUseNativeDefaultsWithoutRejectingKnownRows()
    {
        WithTable(table =>
        {
            table.Add(Row(0));
            table.Add(Row(100));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("1/not-a-uint/256/not-an-int/");
            storage.Load("1/100/not-a-byte/4/255/256/bad/-1/");

            Assert.Equal(2, storage.Objects.Length);
            Assert.Equal(0u, storage.Objects[0].ID);
            Assert.Equal((byte)0, storage.Objects[0].EquippedColor);
            Assert.Empty(storage.Objects[0].Colors!);
            Assert.Equal((byte)0, storage.Objects[1].EquippedColor);
            Assert.Equal(new byte[] { 255, 0, 0, 0 }, storage.Objects[1].Colors);
        });
    }

    [Fact]
    public void MissingTokensDefaultAndCanPublishCatalogIdZero()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            table.Add(Row(0));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("2/100/");

            Assert.Equal(new uint[] { 100, 0 }, storage.Objects.Select(row => row.ID));
            Assert.All(storage.Objects, row =>
            {
                Assert.Equal((byte)0, row.EquippedColor);
                Assert.Empty(row.Colors!);
            });
        });
    }

    [Fact]
    public void NegativeRecordCountDoesNotClearOrChangeExistingCache()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            storage.Load("1/100/1/1/7/");
            var before = storage.Objects;

            storage.Load("-1/100/9/0/");

            Assert.Same(before, storage.Objects);
            Assert.Equal("1/100/1/1/7/", storage.ToString());
        });
    }

    [Fact]
    public void NegativeColorLengthThrowsAfterEarlierRecordsArePublished()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            table.Add(Row(200));
            table.Add(Row(300));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            Assert.Throws<OverflowException>(() => storage.Load("3/100/1/1/7/200/2/-1/300/3/0/"));

            var first = Assert.Single(storage.Objects);
            Assert.Equal(100u, first.ID);
            Assert.Equal(new byte[] { 7 }, first.Colors);
            Assert.False(storage.Exists(200, LegacyHairFaceStorageType.Hairstyle));
            Assert.False(storage.Exists(300, LegacyHairFaceStorageType.Hairstyle));
            Assert.Equal("1/100/1/1/7/", storage.ToString());
        });
    }

    [Fact]
    public void MutableObjectsCacheDrivesExistsAndCanAdmitDuplicateUnderlyingItem()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            table.Add(Row(200));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            storage.Load("2/100/1/1/7/200/2/1/8/");
            var original = storage.Objects[0];
            var exposed = storage.Objects;
            exposed[0] = new Table.Hairface { ID = 999, Type = (LegacyHairFaceStorageType)255, Colors = [] };

            Assert.Same(exposed, storage.Objects);
            Assert.False(storage.Exists(100, LegacyHairFaceStorageType.Hairstyle));
            Assert.True(storage.Exists(999, (LegacyHairFaceStorageType)255));
            storage.Load("1/100/3/1/9/");

            Assert.Equal(3, storage.Objects.Length);
            Assert.NotSame(exposed, storage.Objects);
            Assert.Same(original, storage.Objects[0]);
            Assert.Equal(new uint[] { 100, 200, 100 }, storage.Objects.Select(row => row.ID));
            Assert.NotSame(original, storage.Objects[2]);
            Assert.Equal(new byte[] { 7 }, original.Colors);
            Assert.Equal(new byte[] { 9 }, storage.Objects[2].Colors);
            Assert.False(storage.Exists(999, (LegacyHairFaceStorageType)255));
        });
    }

    [Fact]
    public void ToStringWritesExactOrderColorLengthsAndTrailingSlashWithoutTypeOrMetadata()
    {
        WithTable(table =>
        {
            table.Add(Row(100, LegacyHairFaceStorageType.Avatar));
            table.Add(Row(200));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());

            storage.Load("2/100/255/3/9/8/7/200/4/0/");

            Assert.Equal("2/100/255/3/9/8/7/200/4/0/", storage.ToString());
        });
    }

    [Fact]
    public void ToStringUsesCurrentVariableColorArrayAndDoesNotRepairNull()
    {
        WithTable(table =>
        {
            table.Add(Row(100));
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            storage.Load("1/100/1/1/7/");
            var row = Assert.Single(storage.Objects);

            row.Colors = [];
            Assert.Equal("1/100/1/0/", storage.ToString());
            row.Colors = [255, 2];
            Assert.Equal("1/100/1/2/255/2/", storage.ToString());
            row.Colors = null;
            Assert.Throws<NullReferenceException>(() => storage.ToString());
            Assert.Null(row.Colors);
        });
    }

    [Fact]
    public void NullInvalidOrZeroCountInputLeavesAValidOwnersEmptyState()
    {
        WithTable(_ =>
        {
            var storage = new LegacyNativeHairfaceStorageRuntime(new LegacyNativeGameClientRuntime());
            var original = storage.Objects;

            storage.Load(null);
            storage.Load("");
            storage.Load("3"); // No separator means the native reader is invalid.
            storage.Load("invalid/");
            storage.Load("0/100/1/0/");

            Assert.Same(original, storage.Objects);
            Assert.Empty(storage.Objects);
            Assert.False(storage.Exists(0, LegacyHairFaceStorageType.Hairstyle));
            Assert.Equal("0/", storage.ToString());
        });
    }

    private static Table.Hairface Row(uint id, LegacyHairFaceStorageType type = LegacyHairFaceStorageType.Hairstyle) => new()
    {
        ID = id,
        Type = type,
        Name = "Catalog " + id,
        RareLevel = 4,
        RequiredVIPLevel = 6,
        Cost = 123,
        Classes = [1, 2],
        Sex = 2,
        EquippedColor = 99,
        Colors = [1, 0, 0, 0, 0, 0, 0],
    };

    private static void WithTable(Action<List<Table.Hairface>> body)
    {
        var previous = Table.Hairfaces;
        try
        {
            var owned = new List<Table.Hairface>();
            Table.Hairfaces = owned;
            body(owned);
        }
        finally
        {
            Table.Hairfaces = previous;
        }
    }
}
