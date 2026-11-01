using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private sealed class FirstUseHitClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance() => Interlocked.Add(ref ticks, TimeSpan.FromSeconds(5).Ticks);
    }

    private sealed record FirstUseHitFixture(CreationFixture Owner, ConquerTcpConnection Target,
        FirstUseHitClock Clock, bool Lethal, bool MissingProfs, bool MissingSpells)
    {
        internal string ProfsPath => Path.Combine(Owner.Data.Proficiencies,
            CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => DeathFile(Owner, LegacyTradeJournalFileKind.Spells);
        internal string CharacterPath => DeathFile(Owner, LegacyTradeJournalFileKind.Character);
        internal WorldSession Actor => Owner.CurrentFor(Owner.RuneConnection!);
        internal WorldSession Victim => Owner.CurrentFor(Target);
        internal Task AttackAsync() => Owner.SendOnAsync(Owner.RuneConnection!, DeathAttackRequest());
    }

    private static Task RunFirstUseHitAsync(bool lethal, bool missingProfs, bool missingSpells,
        Func<FirstUseHitFixture, Task> body, bool noWeapon = false)
    {
        var clock = new FirstUseHitClock();
        return Run(async f =>
        {
            await CreateAndAssert(f); await DepositNewbornMoney(f); await f.ConnectRuneActorAsync();
            var target = f.NewConnection(DeathTargetAccount);
            await f.SendOnAsync(target, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
            var fixture = new FirstUseHitFixture(f, target, clock, lethal, missingProfs, missingSpells);
            Assert.Equal(!missingProfs, File.Exists(fixture.ProfsPath));
            Assert.Equal(!missingSpells, File.Exists(fixture.SpellsPath));
            Assert.Empty(fixture.Actor.PlayerProficiencies.Proficiencies);
            var binding = fixture.Actor.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Empty(binding.Proficiencies.ClientProf);
            Assert.True(binding.SavedProficiencies.Matches(missingProfs ? null : new byte[4]));
            Assert.True(binding.SavedProficiencies.MatchesSnapshot(fixture.Actor.PlayerProficiencies));
            if (missingSpells)
            {
                Assert.Empty(fixture.Victim.PlayerSpells.Spells);
                Assert.Empty(fixture.Victim.NativeGameClient!.MySpells!.ClientSpells);
                Assert.True(fixture.Victim.NativeGameClient.OrdinarySpellBinding!.SavedSpells.Matches(null));
            }
            Assert.Equal(lethal ? 200L : 2000L, fixture.Victim.Character.MinHitPoints);
            f.Sent.Clear(); f.Frames(CreationFixture.RuneActor).Clear(); f.Frames(DeathTargetAccount).Clear();
            await body(fixture);
        }, beforeBootstrap: f =>
        {
            SeedHitRecovery(f, lethal, changedDeath: false);
            if (noWeapon)
            {
                var loader = new PlayerItemSnapshotLoader(f.Data.Items);
                var items = loader.LoadByCharacterUid(CreationFixture.RuneActor);
                loader.SaveByCharacterUid(CreationFixture.RuneActor, items with { EquipmentItems = [] });
            }
            var profs = Path.Combine(f.Data.Proficiencies, CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin");
            // Declared saved input BEFORE Bootstrap, read, admission or native birth.
            // No bound file is deleted to manufacture a missing-origin positive.
            if (missingProfs) File.Delete(profs);
            else File.WriteAllBytes(profs, new byte[4]);
            if (missingSpells) File.Delete(DeathFile(f, LegacyTradeJournalFileKind.Spells));
            f.RuntimeOptions = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
                LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        });
    }
}
