using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDaoQiFree10DrawsFlag = 2;
    private const int DaoqiDictTypePointsIndex = 4;
    private const int DaoqiDictTypeRateIndex = 5;
    private const int DaoqiDictTypeRuneIndex = 6;
    private const int DaoqiDictTypeExpIndex = 8;

    [PacketHandler(WorldPacketOpcodes.LegacyDaoQi)]
    private static async ValueTask HandleLegacyDaoQiAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyDaoQiPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyDaoQi packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyDaoQi " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        if (packet.Type == LegacyDaoQiType.TaoistReiki)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count} responses=0.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyDaoQiType.Buy)
        {
            var updatedReiki = unchecked(combatGear.TaoistReiki + packet.Position * 15U);
            var updatedSession = session with { CombatGear = combatGear with { TaoistReiki = updatedReiki } };
            context.Sessions?.Set(context.Connection, updatedSession);

            var responses = new[]
            {
                LegacyDaoQiPacket.TaoistReikiUpdate(updatedReiki),
                packet with { Done = 1 },
            };

            foreach (var daoQiResponse in responses)
            {
                await context.Connection.SendAsync(daoQiResponse.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count} responses={responses.Length}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyDaoQiType.Draws)
        {
            if (!TryApplyLegacyDaoQiDraws(
                    session,
                    packet,
                    context.WorldData,
                    out var updatedSession,
                    out var responses,
                    out var reason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyDaoQi action " +
                        $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                        $"itemId={packet.ItemId} items={packet.Items.Count}; {reason}.")
                    .ConfigureAwait(false);
                return;
            }

            context.Sessions?.Set(context.Connection, updatedSession);
            foreach (var daoQiResponse in responses)
            {
                await context.Connection.SendAsync(daoQiResponse.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count} responses={responses.Count}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyDaoQiType.PirateUpgrade)
        {
            if (!TryApplyLegacyDaoQiPirateUpgrade(
                    session,
                    packet,
                    context.WorldData,
                    out var updatedSession,
                    out var responses,
                    out var reason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyDaoQi action " +
                        $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                        $"itemId={packet.ItemId} items={packet.Items.Count}; {reason}.")
                    .ConfigureAwait(false);
                return;
            }

            context.Sessions?.Set(context.Connection, updatedSession);
            foreach (var daoQiResponse in responses)
            {
                await context.Connection.SendAsync(daoQiResponse.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count} responses={responses.Count}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type is LegacyDaoQiType.Equip or LegacyDaoQiType.UnEquip or LegacyDaoQiType.Replace)
        {
            if (!TryApplyLegacyDaoQiBlackJadeMutation(session, packet, out var updatedSession, out var responses, out var reason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyDaoQi action " +
                        $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                        $"itemId={packet.ItemId} items={packet.Items.Count}; {reason}.")
                    .ConfigureAwait(false);
                return;
            }

            context.Sessions?.Set(context.Connection, updatedSession);
            foreach (var daoQiResponse in responses)
            {
                await context.Connection.SendAsync(daoQiResponse.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count} responses={responses.Count}.")
                .ConfigureAwait(false);
            return;
        }

        LegacyDaoQiPacket? response = packet.Type switch
        {
            LegacyDaoQiType.Info => LegacyDaoQiPacket.Info(combatGear),
            LegacyDaoQiType.LoadItem => LegacyDaoQiPacket.LoadItem(combatGear),
            _ => null,
        };

        if (response is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyDaoQi action " +
                    $"{FormatSession(context)} type={packet.Type} position={packet.Position} " +
                    $"itemId={packet.ItemId} items={packet.Items.Count}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyDaoQi " +
                $"{FormatSession(context)} type={packet.Type} responseType={response.Type} " +
                $"items={response.Items.Count}.")
            .ConfigureAwait(false);
    }

    private static bool TryApplyLegacyDaoQiBlackJadeMutation(
        WorldSession session,
        LegacyDaoQiPacket packet,
        out WorldSession updatedSession,
        out IReadOnlyList<LegacyDaoQiPacket> responses,
        out string reason)
    {
        updatedSession = session;
        responses = [];
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        if (!TryFindCombatGearItem(combatGear, packet.ItemId, out var itemIndex, out var item))
        {
            reason = "combat gear item was not found";
            return false;
        }

        if (packet.Type == LegacyDaoQiType.Replace)
        {
            return TryApplyLegacyDaoQiReplace(session, combatGear, itemIndex, item, packet, out updatedSession, out responses, out reason);
        }

        if (packet.Position == 0 || packet.Position > item.BlackJades.Count)
        {
            reason = "black jade position is outside the combat gear slot range";
            return false;
        }

        var blackJades = item.BlackJades.ToArray();
        var slot = checked((int)packet.Position - 1);
        if (packet.Type == LegacyDaoQiType.UnEquip)
        {
            blackJades[slot] = 0;
            var updatedItem = item with { BlackJades = blackJades };
            updatedSession = session with { CombatGear = WithCombatGearItem(combatGear, itemIndex, updatedItem) };
            responses = [packet with { Done = 1 }];
            reason = "";
            return true;
        }

        if (packet.Items.Count == 0)
        {
            reason = "black jade item was not supplied";
            return false;
        }

        var blackJadeId = packet.Items[0].Id;
        if (!combatGear.BlackJadeBag.BlackJades.Contains(blackJadeId))
        {
            reason = "black jade item was not found in the bag";
            return false;
        }

        blackJades[slot] = blackJadeId;
        updatedSession = session with
        {
            CombatGear = WithCombatGearItem(combatGear, itemIndex, item with { BlackJades = blackJades }),
        };
        responses = [packet with { Done = 1 }];
        reason = "";
        return true;
    }

    private static bool TryApplyLegacyDaoQiDraws(
        WorldSession session,
        LegacyDaoQiPacket packet,
        WorldDataCatalog? worldData,
        out WorldSession updatedSession,
        out IReadOnlyList<LegacyDaoQiPacket> responses,
        out string reason)
    {
        updatedSession = session;
        responses = [];
        var drawCount = packet.Position switch
        {
            0 => 1,
            1 => 10,
            _ => 0,
        };

        if (drawCount == 0)
        {
            reason = "draw position is not supported";
            return false;
        }

        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var responseList = new List<LegacyDaoQiPacket>();
        var cost = checked((uint)drawCount * 1_500U);
        if (packet.Position == 1)
        {
            cost -= cost * 10U / 100U;
        }

        var lottery = combatGear.Lottery;
        if (!HasLegacyDaoQiFlag(lottery, LegacyDaoQiFree10DrawsFlag))
        {
            cost = 0;
            lottery |= LegacyDaoQiFree10DrawsFlag;
            responseList.Add(LegacyDaoQiPacket.LotteryUpdate(lottery));
        }

        if (combatGear.TaoistReiki < cost)
        {
            reason = "not enough Taoist Reiki";
            return false;
        }

        var updatedReiki = combatGear.TaoistReiki - cost;
        var updatedCombatGear = combatGear with
        {
            Lottery = lottery,
            TaoistReiki = updatedReiki,
        };
        responseList.Add(LegacyDaoQiPacket.TaoistReikiUpdate(updatedReiki));

        var drawnItems = new List<LegacyDaoQiItem>(drawCount);
        var masteryPoints = 0U;
        for (var i = 0; i < drawCount; i++)
        {
            if (!updatedCombatGear.BlackJadeBag.BlackJades.Contains(113U))
            {
                updatedCombatGear = WithCombatGearBlackJade(updatedCombatGear, 113U);
                drawnItems.Add(new LegacyDaoQiItem(113U));
                continue;
            }

            if (worldData is null || worldData.DaoqiDictTypes.Count == 0)
            {
                reason = "DaoQi dictionary is not loaded";
                return false;
            }

            if (!TrySelectLegacyDaoQiDraw(worldData.DaoqiDictTypes, out var draw))
            {
                reason = "DaoQi dictionary did not contain a selectable draw";
                return false;
            }

            var blackJadeId = checked((uint)draw.Id);
            if (!TryAddLegacyDaoQiBlackJade(
                    updatedCombatGear,
                    worldData,
                    blackJadeId,
                    out updatedCombatGear,
                    out var addObject,
                    out var duplicatePoints))
            {
                reason = $"DaoQi dictionary entry {draw.Id} is incomplete";
                return false;
            }

            if (addObject is not null)
            {
                responseList.Add(addObject);
            }

            masteryPoints = unchecked(masteryPoints + duplicatePoints);
            drawnItems.Add(new LegacyDaoQiItem(blackJadeId));
        }

        updatedCombatGear = updatedCombatGear with
        {
            MasteryPoints = unchecked(updatedCombatGear.MasteryPoints + masteryPoints),
        };
        responseList.Add(packet with
        {
            Position = masteryPoints,
            Items = drawnItems.ToArray(),
        });
        updatedSession = session with { CombatGear = updatedCombatGear };
        responses = responseList;
        reason = "";
        return true;
    }

    private static bool TrySelectLegacyDaoQiDraw(
        IReadOnlyList<DaoqiDictType> entries,
        out DaoqiDictType entry)
    {
        entry = default!;
        var candidates = entries
            .Where(candidate =>
                candidate.Id >= 0 &&
                candidate.Values.Count > DaoqiDictTypeRuneIndex &&
                GetLegacyDaoQiDrawRate(candidate) > 0)
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidate = candidates[Random.Shared.Next(candidates.Length)];
            if (Random.Shared.Next(10_000) < GetLegacyDaoQiDrawRate(candidate))
            {
                entry = candidate;
                return true;
            }
        }

        entry = candidates
            .OrderByDescending(GetLegacyDaoQiDrawRate)
            .ThenBy(candidate => candidate.Id)
            .First();
        return true;
    }

    private static int GetLegacyDaoQiDrawRate(DaoqiDictType entry)
    {
        var rate = entry.Values.Count > DaoqiDictTypeRateIndex ? Math.Max(0, entry.Values[DaoqiDictTypeRateIndex]) : 0;
        return entry.Type == 3 ? rate / 4 : rate;
    }

    private static bool TryAddLegacyDaoQiBlackJade(
        CombatGearSnapshot combatGear,
        WorldDataCatalog worldData,
        uint blackJadeId,
        out CombatGearSnapshot updatedCombatGear,
        out LegacyDaoQiPacket? addObject,
        out uint duplicatePoints)
    {
        updatedCombatGear = combatGear;
        addObject = null;
        duplicatePoints = 0;
        if (blackJadeId > int.MaxValue ||
            !worldData.DaoqiDictTypesById.TryGetValue((int)blackJadeId, out var entry) ||
            !TryGetDaoQiDictValue(entry, DaoqiDictTypeRuneIndex, out var typeRune))
        {
            return false;
        }

        foreach (var existingBlackJadeId in combatGear.BlackJadeBag.BlackJades)
        {
            if (existingBlackJadeId <= int.MaxValue &&
                worldData.DaoqiDictTypesById.TryGetValue((int)existingBlackJadeId, out var existingEntry) &&
                TryGetDaoQiDictValue(existingEntry, DaoqiDictTypeRuneIndex, out var existingTypeRune) &&
                existingTypeRune == typeRune)
            {
                duplicatePoints = TryGetDaoQiDictValue(entry, DaoqiDictTypePointsIndex, out var points)
                    ? checked((uint)Math.Max(0, points))
                    : 0;
                return true;
            }
        }

        updatedCombatGear = WithCombatGearBlackJade(combatGear, blackJadeId);
        addObject = LegacyDaoQiPacket.AddObject(blackJadeId);
        return true;
    }

    private static bool TryGetDaoQiDictValue(DaoqiDictType entry, int index, out int value)
    {
        if (index < 0 || index >= entry.Values.Count)
        {
            value = 0;
            return false;
        }

        value = entry.Values[index];
        return true;
    }

    private static bool HasLegacyDaoQiFlag(uint value, uint flag)
    {
        return (value & flag) != 0;
    }

    private static bool TryApplyLegacyDaoQiPirateUpgrade(
        WorldSession session,
        LegacyDaoQiPacket packet,
        WorldDataCatalog? worldData,
        out WorldSession updatedSession,
        out IReadOnlyList<LegacyDaoQiPacket> responses,
        out string reason)
    {
        updatedSession = session;
        responses = [];
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var blackJadeIndex = -1;
        for (var i = 0; i < combatGear.BlackJadeBag.BlackJades.Count; i++)
        {
            if (combatGear.BlackJadeBag.BlackJades[i] == packet.ItemId)
            {
                blackJadeIndex = i;
                break;
            }
        }

        if (blackJadeIndex < 0)
        {
            reason = "black jade item was not found in the bag";
            return false;
        }

        if (packet.ItemId % 1_000U >= 900U)
        {
            reason = "black jade is already at the source upgrade cap";
            return false;
        }

        var cost = GetLegacyDaoQiPirateUpgradeCost(worldData, packet.ItemId);
        if (combatGear.PirateExp < cost)
        {
            reason = "not enough PirateExp";
            return false;
        }

        var upgradedBlackJadeId = checked(packet.ItemId + 100U);
        var blackJades = combatGear.BlackJadeBag.BlackJades.ToArray();
        blackJades[blackJadeIndex] = upgradedBlackJadeId;
        var updatedPirateExp = combatGear.PirateExp - cost;
        var updatedCombatGear = combatGear with
        {
            PirateExp = updatedPirateExp,
            BlackJadeBag = new CombatGearBlackJadeBagSnapshot(blackJades.Length, blackJades),
        };
        updatedSession = session with { CombatGear = updatedCombatGear };
        responses =
        [
            LegacyDaoQiPacket.PiratePointsUpdate(updatedPirateExp),
            packet with
            {
                PirateExp = upgradedBlackJadeId,
                Position = 1,
            },
        ];
        reason = "";
        return true;
    }

    private static uint GetLegacyDaoQiPirateUpgradeCost(WorldDataCatalog? worldData, uint blackJadeId)
    {
        if (worldData is null ||
            blackJadeId > int.MaxValue ||
            !worldData.DaoqiDictTypesById.TryGetValue((int)blackJadeId, out var entry) ||
            !TryGetDaoQiDictValue(entry, DaoqiDictTypeExpIndex, out var exp))
        {
            return 0;
        }

        return checked((uint)Math.Max(0, exp));
    }

    private static bool TryApplyLegacyDaoQiReplace(
        WorldSession session,
        CombatGearSnapshot combatGear,
        int itemIndex,
        CombatGearSnapshotItem item,
        LegacyDaoQiPacket packet,
        out WorldSession updatedSession,
        out IReadOnlyList<LegacyDaoQiPacket> responses,
        out string reason)
    {
        updatedSession = session;
        responses = [];
        if (packet.Items.Count == 0)
        {
            reason = "black jade items were not supplied";
            return false;
        }

        var blackJades = item.BlackJades.ToArray();
        var responseCount = 0;
        for (var i = 0; i < packet.Items.Count && i < blackJades.Length; i++)
        {
            var blackJadeId = packet.Items[i].Id;
            if (!combatGear.BlackJadeBag.BlackJades.Contains(blackJadeId))
            {
                continue;
            }

            blackJades[i] = blackJadeId;
            responseCount++;
        }

        if (responseCount == 0)
        {
            reason = "no supplied black jade item was found in the bag";
            return false;
        }

        updatedSession = session with
        {
            CombatGear = WithCombatGearItem(combatGear, itemIndex, item with { BlackJades = blackJades }),
        };
        responses = Enumerable
            .Repeat(packet with { Done = 1 }, responseCount)
            .ToArray();
        reason = "";
        return true;
    }

}
