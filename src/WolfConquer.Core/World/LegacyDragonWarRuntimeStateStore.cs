using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum LegacyDragonWarProcess
{
    Dead,
    Idle,
    Alive,
}

public sealed class LegacyDragonWarRuntimeStateStore
{
    public static LegacyDragonWarEndRewardEconomyPolicy EndRewardEconomyPolicy { get; } =
        LegacyDragonWarEndRewardEconomyPolicy.SourceObservedTuned;

    public const long DragonWarMapId = 700;
    public const int DragonSwingStatusFlag = 0xA0;
    public const string RuntimeDragonSwingExpiresAtUtcAttribute = "LegacyDragonSwingExpiresAtUtc";

    private const int LegacyInventorySlots = 40;
    private const uint FirstRightCornerChatType = 2_108;
    private const uint ContinueRightCornerChatType = 2_109;
    private const uint LegacyReturnedItemsWarehouseId = 65_535;
    private const string DragonWarInvitationText = "DragonWar[SS/FB]";
    private const ushort DragonWarInvitationMapId = 1002;
    private const ushort DragonWarInvitationX = 319;
    private const ushort DragonWarInvitationY = 256;
    private const ushort DragonWarInvitationDynamicId = 0;
    private const int DragonWarInvitationSeconds = 60;
    private static readonly TimeSpan InvitationWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TournamentDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CountdownInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScoreboardInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DragonSwingDuration = TimeSpan.FromSeconds(2_592_000);
    private static readonly TimeSpan DragonWarReviveDelay = TimeSpan.FromSeconds(4);

    private readonly object _gate = new();
    private LegacyDragonWarProcess _process;
    private uint _dynamicId;
    private DateTimeOffset _startTimer;
    private DateTimeOffset _nextInfoTimer;
    private DateTimeOffset _scoreStamp;
    private uint _seconds;

    public LegacyDragonWarProcess Process
    {
        get
        {
            lock (_gate)
            {
                return _process;
            }
        }
    }

    public LegacyRuntimeUpdate? Open(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_process != LegacyDragonWarProcess.Dead)
            {
                return null;
            }

            _process = LegacyDragonWarProcess.Idle;
            _dynamicId = CreateDynamicId(now);
            _startTimer = now;
            _nextInfoTimer = now + CountdownInterval;
            _scoreStamp = DateTimeOffset.MinValue;
            _seconds = 60;
        }

        return CreateOpenInvitationUpdate();
    }

    public void SetProcess(LegacyDragonWarProcess process)
    {
        lock (_gate)
        {
            _process = process;
            _scoreStamp = DateTimeOffset.MinValue;
        }
    }

    public bool TryJoin(WorldSession session, out WorldSession updatedSession)
    {
        ArgumentNullException.ThrowIfNull(session);

        uint dynamicId;
        lock (_gate)
        {
            if (_process != LegacyDragonWarProcess.Idle)
            {
                updatedSession = default!;
                return false;
            }

            dynamicId = _dynamicId;
        }

        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarScore"] = "0",
            ["DragonWarHits"] = "0",
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
        };
        RemoveLegacyStatusFlag(attributes, DragonSwingStatusFlag);

        var (x, y) = CreateJoinCoordinates(session.Character.Uid, dynamicId);
        var character = session.Character with
        {
            MapId = DragonWarMapId,
            X = x,
            Y = y,
            PreviousMapId = session.Character.MapId,
            PreviousMapX = session.Character.X,
            PreviousMapY = session.Character.Y,
            Attributes = attributes,
        };

        updatedSession = session with { Character = character };
        return true;
    }

    public LegacyDragonWarRuntimeTickResult CreateDueRuntimeUpdates(
        DateTimeOffset now,
        IEnumerable<WorldSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var sessionSnapshot = sessions.ToList();
        var startTournament = false;
        var endTournament = false;
        var dynamicId = 0u;
        var process = LegacyDragonWarProcess.Dead;
        string? countdownMessage = null;

        lock (_gate)
        {
            process = _process;
            dynamicId = _dynamicId;
            if (_process == LegacyDragonWarProcess.Idle)
            {
                if (now > _startTimer + InvitationWindow)
                {
                    _process = LegacyDragonWarProcess.Alive;
                    process = _process;
                    _startTimer = now;
                    _scoreStamp = DateTimeOffset.MinValue;
                    startTournament = true;
                }
                else if (now > _nextInfoTimer)
                {
                    _seconds = _seconds >= 10 ? _seconds - 10 : 0;
                    _nextInfoTimer = now + CountdownInterval;
                    countdownMessage = $"Fight starts in {_seconds.ToString(CultureInfo.InvariantCulture)} Secounds.";
                }
            }
            else if (_process == LegacyDragonWarProcess.Alive &&
                now > _startTimer + TournamentDuration)
            {
                _process = LegacyDragonWarProcess.Dead;
                process = _process;
                _scoreStamp = DateTimeOffset.MinValue;
                endTournament = true;
            }
        }

        if (startTournament)
        {
            var updates = new List<LegacyDragonWarRuntimeUpdate>
            {
                CreateBroadcastUpdate(
                    LegacyMsgMessagePacket.CenterChatType,
                    "DragonWar has started! signup are now closed."),
            };
            var mutations = CreateDragonSwingStartMutations(now, dynamicId, sessionSnapshot);
            return new LegacyDragonWarRuntimeTickResult(updates, mutations);
        }

        if (endTournament)
        {
            return CreateDragonWarEndTickResult(dynamicId, sessionSnapshot);
        }

        var reviveMutations = CreateDragonWarReviveMutations(now, dynamicId, process, sessionSnapshot);
        if (countdownMessage is not null)
        {
            return new LegacyDragonWarRuntimeTickResult(
                [CreateBroadcastUpdate(LegacyMsgMessagePacket.CenterChatType, countdownMessage)],
                reviveMutations);
        }

        return new LegacyDragonWarRuntimeTickResult(CreateDueUpdates(now, sessionSnapshot), reviveMutations);
    }

    public IReadOnlyList<LegacyDragonWarRuntimeUpdate> CreateDueUpdates(
        DateTimeOffset now,
        IEnumerable<WorldSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        uint dynamicId;
        lock (_gate)
        {
            if (_process != LegacyDragonWarProcess.Alive || now < _scoreStamp)
            {
                return [];
            }

            dynamicId = _dynamicId;
            _scoreStamp = now + ScoreboardInterval;
        }

        var players = sessions
            .Where(session => IsInTournament(session, dynamicId))
            .ToList();
        if (players.Count == 0)
        {
            return [];
        }

        var ranking = players
            .Select(session => new LegacyDragonWarRankEntry(
                session.Character.Name,
                ReadUInt64Attribute(session.Character.Attributes, "DragonWarScore", "LegacyDragonWarScore")))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Take(4)
            .ToList();

        var updates = new List<LegacyDragonWarRuntimeUpdate>(players.Count * (ranking.Count + 2));
        foreach (var player in players)
        {
            var recipientUid = CheckedUInt32(player.Character.Uid);
            updates.Add(CreateUpdate(recipientUid, FirstRightCornerChatType, "DragonWar Score: "));

            for (var index = 0; index < ranking.Count; index++)
            {
                var rank = ranking[index];
                updates.Add(CreateUpdate(
                    recipientUid,
                    ContinueRightCornerChatType,
                    $"No {index + 1}. {rank.Name} ({rank.Score.ToString(CultureInfo.InvariantCulture)})"));
            }

            var tournamentKills = ReadUInt64Attribute(
                player.Character.Attributes,
                "TournamentKills",
                "LegacyTournamentKills");
            updates.Add(CreateUpdate(
                recipientUid,
                ContinueRightCornerChatType,
                $"My tournament Kills: {tournamentKills.ToString(CultureInfo.InvariantCulture)}"));
        }

        return updates;
    }

    private static IReadOnlyList<LegacyDragonWarRuntimeSessionUpdate> CreateDragonSwingStartMutations(
        DateTimeOffset now,
        uint dynamicId,
        IReadOnlyList<WorldSession> sessions)
    {
        var firstPlayer = sessions.FirstOrDefault(session => IsInTournament(session, dynamicId));
        if (firstPlayer is null)
        {
            return [];
        }

        var attributes = new Dictionary<string, string>(
            firstPlayer.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarHits"] = "5",
        };
        if (!LegacyPlayerDeathState.HasStatusFlag(firstPlayer.Character.Attributes, DragonSwingStatusFlag))
            attributes[RuntimeDragonSwingExpiresAtUtcAttribute] =
                (now + DragonSwingDuration).ToString("O", CultureInfo.InvariantCulture);
        AddLegacyStatusFlag(attributes, DragonSwingStatusFlag);

        var updatedSession = firstPlayer with
        {
            Character = LegacyStatusFlagDeathRegistration.CaptureAddFlag(firstPlayer.Character,
                firstPlayer.Character with { Attributes = attributes },
                DragonSwingStatusFlag, removeOnDead: true),
        };
        var statusFrame = LegacyMsgUpdatePacket
            .CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(updatedSession.Character.Uid),
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    updatedSession.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();
        return [new LegacyDragonWarRuntimeSessionUpdate(
            CheckedUInt32(updatedSession.Character.Uid),
            updatedSession,
            statusFrame)];
    }

    private static IReadOnlyList<LegacyDragonWarRuntimeSessionUpdate> CreateDragonWarReviveMutations(
        DateTimeOffset now,
        uint dynamicId,
        LegacyDragonWarProcess process,
        IReadOnlyList<WorldSession> sessions)
    {
        if (process == LegacyDragonWarProcess.Dead)
        {
            return [];
        }

        var mutations = new List<LegacyDragonWarRuntimeSessionUpdate>();
        foreach (var session in sessions)
        {
            if (!IsInTournament(session, dynamicId))
            {
                continue;
            }

            var revive = LegacyPlayerDeathState.TryRevive(
                session.Character,
                now,
                new LegacyPlayerReviveOptions { ReviveDelay = DragonWarReviveDelay });
            if (!revive.Allowed)
            {
                continue;
            }

            var attributes = new Dictionary<string, string>(
                revive.Character.Attributes,
                StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            };
            var (x, y) = CreateJoinCoordinates(session.Character.Uid + now.UtcTicks, dynamicId);
            var updatedCharacter = revive.Character with
            {
                MapId = DragonWarMapId,
                X = x,
                Y = y,
                PreviousMapId = session.Character.MapId,
                PreviousMapX = session.Character.X,
                PreviousMapY = session.Character.Y,
                Attributes = attributes,
            };
            var updatedSession = session with { Character = updatedCharacter };
            var statusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    CheckedUInt32(updatedCharacter.Uid),
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        updatedCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();
            mutations.Add(new LegacyDragonWarRuntimeSessionUpdate(
                CheckedUInt32(updatedCharacter.Uid),
                updatedSession,
                statusFrame));
        }

        return mutations;
    }

    private static LegacyDragonWarRuntimeTickResult CreateDragonWarEndTickResult(
        uint dynamicId,
        IReadOnlyList<WorldSession> sessions)
    {
        var players = sessions
            .Where(session => IsInTournament(session, dynamicId))
            .Select(session => new
            {
                Session = session,
                Score = ReadUInt64Attribute(
                    session.Character.Attributes,
                    "DragonWarScore",
                    "LegacyDragonWarScore"),
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Session.Character.Name, StringComparer.Ordinal)
            .ToList();

        var updates = new List<LegacyDragonWarRuntimeUpdate>
        {
            CreateBroadcastUpdate(
                LegacyMsgMessagePacket.CenterChatType,
                "DragonWar has ended. All Players of DragonWar has teleported to TwinCity."),
        };
        var mutations = new List<LegacyDragonWarRuntimeSessionUpdate>(players.Count);

        if (players.Count == 0)
        {
            return new LegacyDragonWarRuntimeTickResult(updates, mutations);
        }

        updates.Add(CreateBroadcastUpdate(
            LegacyMsgMessagePacket.BroadcastChatType,
            $"{players[0].Session.Character.Name} has Won  DragonWar. "));

        for (var index = 0; index < players.Count; index++)
        {
            var rank = index + 1;
            var session = players[index].Session;
            var uid = CheckedUInt32(session.Character.Uid);
            var isWinner = index == 0;
            var conquerPointReward = isWinner
                ? EndRewardEconomyPolicy.TunedWinnerConquerPointsReward
                : EndRewardEconomyPolicy.TunedWinnerConquerPointsReward / rank;

            updates.Add(CreateUpdate(
                uid,
                LegacyMsgMessagePacket.TalkChatType,
                isWinner
                    ? EndRewardEconomyPolicy.TunedWinnerMessage
                    : $"You received {conquerPointReward.ToString(CultureInfo.InvariantCulture)} ConquerPoints. "));

            var attributes = new Dictionary<string, string>(
                session.Character.Attributes,
                StringComparer.OrdinalIgnoreCase);
            attributes.Remove("DynamicID");
            attributes.Remove("DynamicId");
            attributes.Remove(RuntimeDragonSwingExpiresAtUtcAttribute);
            RemoveLegacyStatusFlag(attributes, DragonSwingStatusFlag);

            if (isWinner)
            {
                attributes["ClassExperience"] = checked(
                    ReadUInt64Attribute(attributes, "ClassExperience") +
                    EndRewardEconomyPolicy.TunedWinnerClassExperienceReward)
                    .ToString(CultureInfo.InvariantCulture);
            }

            var updatedCharacter = session.Character with
            {
                MapId = 1002,
                X = 410,
                Y = 355,
                PreviousMapId = session.Character.MapId,
                PreviousMapX = session.Character.X,
                PreviousMapY = session.Character.Y,
                ConquerPoints = checked(session.Character.ConquerPoints + conquerPointReward),
                Attributes = attributes,
            };
            var updatedItems = session.PlayerItems;
            var statusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        updatedCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();

            var ownerFrames = new List<byte[]>
            {
                new LegacyMsgUpdatePacket(
                    uid,
                    [new LegacyMsgUpdateEntry(
                        LegacyMsgUpdateDataType.ConquerPoints,
                        checked((ulong)updatedCharacter.ConquerPoints))])
                    .ToFrameBytes(),
            };
            var playerItemsDirty = false;
            if (isWinner)
            {
                ownerFrames.Add(new LegacyMsgUpdatePacket(
                        uid,
                        [new LegacyMsgUpdateEntry(
                            LegacyMsgUpdateDataType.ClassExperience,
                            ReadUInt64Attribute(updatedCharacter.Attributes, "ClassExperience"))])
                    .ToFrameBytes());

                var rewardItem = CreateDragonWarPowerExpBallItem(updatedItems);
                if (HasDragonWarInventorySpace(
                    updatedItems,
                    EndRewardEconomyPolicy.TunedWinnerItemRequiredFreeSlots))
                {
                    updatedItems = updatedItems with
                    {
                        InventoryItems = updatedItems.InventoryItems
                            .Concat([rewardItem])
                            .OrderBy(item => item.Uid)
                            .ToArray(),
                    };
                    playerItemsDirty = true;
                    ownerFrames.Add(
                        LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes());
                }
                else
                {
                    var returnedRewardItem = rewardItem.WithWarehouseId(LegacyReturnedItemsWarehouseId, position: 0);
                    updatedItems = updatedItems with
                    {
                        WarehouseItems = updatedItems.WarehouseItems
                            .Concat([returnedRewardItem])
                            .OrderBy(item => item.WarehouseId)
                            .ThenBy(item => item.Uid)
                            .ToArray(),
                    };
                    playerItemsDirty = true;
                    ownerFrames.Add(
                        LegacyGameItemPacket
                            .FromSnapshotItem(returnedRewardItem, mode: LegacyGameItemPacket.AddItemReturnedMode)
                            .ToFrameBytes());
                }
            }

            var updatedSession = session with
            {
                Character = updatedCharacter,
                PlayerItems = updatedItems,
            };
            mutations.Add(new LegacyDragonWarRuntimeSessionUpdate(
                uid,
                updatedSession,
                statusFrame)
            {
                OwnerFrameBytes = ownerFrames,
                PlayerItemsDirty = playerItemsDirty,
                RewardSourceMethodName = EndRewardEconomyPolicy.SourceRewardOwnerName,
            });
        }

        return new LegacyDragonWarRuntimeTickResult(updates, mutations);
    }

    private static PlayerItemSnapshotItem CreateDragonWarPowerExpBallItem(PlayerItemSnapshot items)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = EndRewardEconomyPolicy.TunedWinnerItemId,
            Position = 0,
            StackSize = EndRewardEconomyPolicy.TunedWinnerItemStackSize,
        };
    }

    private static bool HasDragonWarInventorySpace(PlayerItemSnapshot items, int count)
    {
        return items.InventoryItems.Count + count <= LegacyInventorySlots;
    }

    private static uint GetNextSessionItemUid(PlayerItemSnapshot items)
    {
        var maxUid = items.InventoryItems
            .Concat(items.RelicFuseItems)
            .Concat(items.MythsoulBagItems)
            .Concat(items.EquipmentItems)
            .Concat(items.RuneItems)
            .Concat(items.WardrobeItems)
            .Concat(items.WarehouseItems)
            .Select(item => item.Uid)
            .DefaultIfEmpty(0U)
            .Max();

        return checked(maxUid + 1);
    }

    private static bool IsInTournament(WorldSession session, uint dynamicId)
    {
        return session.Character.MapId == DragonWarMapId &&
            TryReadUInt32Attribute(session.Character.Attributes, out var sessionDynamicId, "DynamicID", "DynamicId") &&
            sessionDynamicId == dynamicId;
    }

    private static LegacyDragonWarRuntimeUpdate CreateUpdate(uint recipientUid, uint chatType, string message)
    {
        var packet = new LegacyMsgMessagePacket(
            LegacyMsgMessagePacket.YellowColor,
            chatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty]);
        return new LegacyDragonWarRuntimeUpdate(recipientUid, chatType, message, packet.ToFrameBytes());
    }

    private static LegacyDragonWarRuntimeUpdate CreateBroadcastUpdate(uint chatType, string message)
    {
        var update = CreateUpdate(0, chatType, message);
        return update with { Broadcast = true };
    }

    private static LegacyRuntimeUpdate CreateOpenInvitationUpdate()
    {
        var prompt = new LegacyRuntimeMessageBoxPrompt(
            LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
            DragonWarInvitationText,
            DragonWarInvitationMapId,
            DragonWarInvitationX,
            DragonWarInvitationY,
            DragonWarInvitationDynamicId,
            DragonWarInvitationSeconds);
        return new LegacyRuntimeUpdate(
            LegacyMsgMessagePacket.SystemCenter(" " + DragonWarInvitationText).ToFrameBytes(),
            prompt);
    }

    private static uint CreateDynamicId(DateTimeOffset now)
    {
        var seed = unchecked((uint)now.UtcTicks);
        seed ^= unchecked((uint)(now.UtcTicks >> 32));
        seed ^= (uint)Environment.TickCount;
        return seed == 0 ? 1 : seed;
    }

    private static (long X, long Y) CreateJoinCoordinates(long uid, uint dynamicId)
    {
        var seed = unchecked((int)(uid ^ dynamicId));
        var random = new Random(seed);
        return (random.Next(35, 70), random.Next(35, 70));
    }

    private static bool TryReadUInt32Attribute(
        IReadOnlyDictionary<string, string> attributes,
        out uint value,
        params string[] keys)
    {
        value = 0;
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static ulong ReadUInt64Attribute(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static uint CheckedUInt32(long value)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Legacy DragonWar recipient UID must fit UInt32.");
        }

        return (uint)value;
    }

    private static void RemoveLegacyStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        if (flag < 0)
        {
            return;
        }

        if (attributes.TryGetValue("StatusFlags", out var statusFlags) &&
            TryRemoveStatusFlag(statusFlags, flag, out var updatedStatusFlags))
        {
            attributes["StatusFlags"] = updatedStatusFlags;
            return;
        }

        if (attributes.TryGetValue("BitVector", out var bitVector) &&
            TryRemoveStatusFlag(bitVector, flag, out var updatedBitVector))
        {
            attributes["BitVector"] = updatedBitVector;
        }
    }

    private static void AddLegacyStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, flag);
        if (flag < 0)
        {
            return;
        }

        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        var words = ReadLegacyStatusWords32(attributes);
        var wordIndex = flag / 32;
        var wordCount = Math.Max(Math.Max(10, words.Count), wordIndex + 1);
        while (words.Count < wordCount)
        {
            words.Add(0);
        }

        words[wordIndex] |= 1UL << (flag % 32);
        attributes[key] = string.Join('/', words.Select(word => word.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private static List<ulong> ReadLegacyStatusWords32(IReadOnlyDictionary<string, string> attributes)
    {
        if ((!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw)) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        if (raw.Contains('/', StringComparison.Ordinal))
        {
            return raw
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : 0)
                .ToList();
        }

        var words = new List<ulong>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                value = 0;
            }

            words.Add(value & uint.MaxValue);
            words.Add(value >> 32);
        }

        return words;
    }

    private static bool TryRemoveStatusFlag(string raw, int flag, out string updated)
    {
        updated = raw;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var usesSlash = raw.Contains('/', StringComparison.Ordinal);
        var separator = usesSlash ? '/' : ',';
        var parts = raw.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = usesSlash ? flag / 32 : flag / 64;
        var bitIndex = usesSlash ? flag % 32 : flag % 64;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        var mask = 1UL << bitIndex;
        if ((word & mask) == 0)
        {
            return false;
        }

        parts[wordIndex] = (word & ~mask).ToString(CultureInfo.InvariantCulture);
        updated = string.Join(separator.ToString(), parts) +
            (usesSlash && raw.EndsWith('/') ? "/" : "");
        return true;
    }

    private readonly record struct LegacyDragonWarRankEntry(string Name, ulong Score);
}

public sealed record LegacyDragonWarEndRewardEconomyPolicy(
    string SourceRewardClassName,
    string SourceRewardMethodName,
    long SourceObservedWinnerConquerPointsReward,
    long TunedWinnerConquerPointsReward,
    ulong SourceObservedWinnerClassExperienceReward,
    ulong TunedWinnerClassExperienceReward,
    uint SourceObservedWinnerItemId,
    uint TunedWinnerItemId,
    ushort SourceObservedWinnerItemStackSize,
    ushort TunedWinnerItemStackSize,
    int SourceObservedWinnerItemRequiredFreeSlots,
    int TunedWinnerItemRequiredFreeSlots,
    string TunedWinnerMessage)
{
    public string SourceRewardOwnerName => string.Concat(SourceRewardClassName, ".", SourceRewardMethodName);

    public static LegacyDragonWarEndRewardEconomyPolicy SourceObservedTuned { get; } = new(
        SourceRewardClassName: "MrRayzo.Game.MsgTournaments.MsgDragonWar",
        SourceRewardMethodName: "CheckUp",
        SourceObservedWinnerConquerPointsReward: 1_000_000,
        TunedWinnerConquerPointsReward: 75_000,
        SourceObservedWinnerClassExperienceReward: 500,
        TunedWinnerClassExperienceReward: 500,
        SourceObservedWinnerItemId: 722_057,
        TunedWinnerItemId: 722_057,
        SourceObservedWinnerItemStackSize: 4,
        TunedWinnerItemStackSize: 1,
        SourceObservedWinnerItemRequiredFreeSlots: 2,
        TunedWinnerItemRequiredFreeSlots: 1,
        TunedWinnerMessage: "You received 75000 ConquerPoints, 500 ClassExperience and 1 PowerExpBall. ");
}

public sealed record LegacyDragonWarRuntimeUpdate(
    uint RecipientUid,
    uint ChatType,
    string Message,
    byte[] FrameBytes)
{
    public bool Broadcast { get; init; }
}

public sealed record LegacyDragonWarRuntimeSessionUpdate(
    uint RecipientUid,
    WorldSession Session,
    byte[] StatusFrameBytes)
{
    public IReadOnlyList<byte[]> OwnerFrameBytes { get; init; } = [];
    public bool PlayerItemsDirty { get; init; }
    public string? RewardSourceMethodName { get; init; }
}

public sealed record LegacyDragonWarRuntimeTickResult(
    IReadOnlyList<LegacyDragonWarRuntimeUpdate> Updates,
    IReadOnlyList<LegacyDragonWarRuntimeSessionUpdate> SessionUpdates);
