// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/PlayerFishingEntry.cs -> ours,
// High Seas, second half. A player's standing with the fish mongers: reputation (points per delivered fish: one for a
// shore fish, two for a crustacean or ocean fish, three for a dungeon fish), and per fish how many an order asks for
// next (10 to start; up by 5 after each delivery, back to 10 past 20; down after a forfeit) and how often it was
// delivered (on a galleon, more deliveries open orders of up to six fish; every pinned boat is a small one, one fish).
// [FishMongerStatus tells a player their standing.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - Saved in HighSeasPersistence (ServUO's Highseas.bin carried the same list).
// - ServUO's GetPointsAwarded added its running total to the reputation once per line (so a three-line order counted
//   the first line three times). Kept: the reputation only names a standing, and the order's points are its sum.

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Items;

namespace Server.Engines.Quests;

public class PlayerFishingEntry
{
    private static readonly Dictionary<Mobile, PlayerFishingEntry> _fishingEntries = new();

    private static readonly string[] _status =
    {
        "a small",
        "a fair",
        "a moderate",
        "a tremendous",
        "an outstanding",
        "an enormous"
    };

    public PlayerFishingEntry(Mobile from)
    {
        Player = from;
        _fishingEntries[from] = this;
    }

    public Mobile Player { get; private set; }
    public double Reputation { get; set; }
    public bool HasRecievedBritGal { get; set; }
    public Dictionary<int, int> HaveFished { get; } = new();
    public Dictionary<int, int> TimesFished { get; } = new();

    public static Dictionary<Mobile, PlayerFishingEntry> FishingEntries => _fishingEntries;

    public static void Initialize()
    {
        CommandSystem.Register("FishMongerStatus", AccessLevel.Player, FishMongerStatus_OnCommand);
    }

    public static PlayerFishingEntry GetEntry(Mobile from, bool create = false)
    {
        if (from == null)
        {
            return null;
        }

        if (_fishingEntries.TryGetValue(from, out var entry))
        {
            return entry;
        }

        return create ? new PlayerFishingEntry(from) : null;
    }

    public double GetPointsAwarded(ShippingCrate order)
    {
        var toAward = 0.0;

        for (var i = 0; i < order.LineCount; i++)
        {
            var type = order.LineType(i);
            var value = order.LineNeeded(i);

            if (FishQuestHelper.IsShallowWaterFish(type))
            {
                toAward += value;
            }
            else if (FishQuestHelper.IsCrustacean(type) || FishQuestHelper.IsDeepWaterFish(type))
            {
                toAward += value * 2;
            }
            else if (FishQuestHelper.IsDungeonFish(type))
            {
                toAward += value * 3;
            }

            Reputation += toAward;
        }

        return toAward;
    }

    public void OnAfterReward(double points)
    {
        if (Player?.NetState == null)
        {
            return;
        }

        if (points <= 100)
        {
            Player.SendMessage("You have gained a fair amount of reputation with the Fish Mongers.");
        }
        else if (points <= 150)
        {
            Player.SendMessage("You have gained a good amount of reputation with the Fish Mongers.");
        }
        else
        {
            Player.SendMessage("You have gained a large amount of reputation with the Fish Mongers.");
        }
    }

    /// <summary>ServUO's line count for a galleon: more deliveries of more kinds open more lines (up to six).</summary>
    public int CalculateLines()
    {
        var eligibleIndex = FishQuestHelper.GetIndexForSkillLevel(Player);
        double line2 = 0, line3 = 0, line4 = 0, line5 = 0, line6 = 0;

        for (var i = 0; i < eligibleIndex; i++)
        {
            if (!TimesFished.TryGetValue(i, out var times))
            {
                continue;
            }

            var timesDone = Math.Min(6, times);
            var toAdd = 100 / eligibleIndex;

            if (timesDone >= 5) line6 += toAdd;
            if (timesDone >= 4) line5 += toAdd;
            if (timesDone >= 3) line4 += toAdd;
            if (timesDone >= 2) line3 += toAdd;
            if (timesDone >= 1) line2 += toAdd;
        }

        if (Math.Min(85, line6) >= Utility.Random(100)) return 6;
        if (Math.Min(85, line5) >= Utility.Random(100)) return 5;
        if (Math.Min(85, line4) >= Utility.Random(100)) return 4;
        if (Math.Min(85, line3) >= Utility.Random(100)) return 3;
        if (Math.Min(85, line2) >= Utility.Random(100)) return 2;
        return 1;
    }

    public void GetRandomFish(ref int index, ref int amount, List<int> chosen)
    {
        var eligibleIndex = FishQuestHelper.GetIndexForSkillLevel(Player);

        do
        {
            index = Utility.Random(eligibleIndex);
        } while (chosen.Contains(index) && chosen.Count < eligibleIndex);

        amount = HaveFished.TryGetValue(index, out var have) ? have : 10;
    }

    public void OnQuestResign(Type type)
    {
        var index = FishQuestHelper.GetIndexForType(type);

        Reputation -= 20;

        if (HaveFished.TryGetValue(index, out var have))
        {
            HaveFished[index] = have > 10 ? have - 5 : 5;
        }
        else
        {
            HaveFished[index] = 10;
        }
    }

    public void OnQuestComplete(ShippingCrate order)
    {
        for (var i = 0; i < order.LineCount; i++)
        {
            var index = FishQuestHelper.GetIndexForType(order.LineType(i));

            if (HaveFished.TryGetValue(index, out var have))
            {
                HaveFished[index] = have >= 20 ? 10 : have + 5;
            }
            else
            {
                HaveFished[index] = 15;
            }

            TimesFished[index] = TimesFished.GetValueOrDefault(index) + 1;
        }
    }

    public string GetStatus() =>
        Reputation switch
        {
            < 500   => _status[0],
            < 1000  => _status[1],
            < 5000  => _status[2],
            < 15000 => _status[3],
            < 50000 => _status[4],
            _       => _status[5]
        };

    [Usage("FishMongerStatus")]
    [Description("Tells you your standing with the fish mongers (the profession fish quests).")]
    [ShardCommand(CommandCategory.Player)]
    public static void FishMongerStatus_OnCommand(CommandEventArgs e)
    {
        var entry = GetEntry(e.Mobile);

        if (entry != null)
        {
            e.Mobile.SendMessage($"You have earned {entry.GetStatus()} amount of reputation with the Fish Mongers.");
        }
        else
        {
            e.Mobile.SendMessage("You have no reputation with the Fish Mongers.");
        }
    }

    public static void SaveAll(IGenericWriter writer)
    {
        var entries = new List<PlayerFishingEntry>();

        foreach (var entry in _fishingEntries.Values)
        {
            if (entry.Player?.Deleted == false)
            {
                entries.Add(entry);
            }
        }

        writer.WriteEncodedInt(entries.Count);

        foreach (var entry in entries)
        {
            entry.Serialize(writer);
        }
    }

    public static void LoadAll(IGenericReader reader)
    {
        _fishingEntries.Clear();
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var entry = Deserialize(reader);

            if (entry.Player != null)
            {
                _fishingEntries[entry.Player] = entry;
            }
        }
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.Write(HasRecievedBritGal);
        writer.Write(Player);
        writer.Write(Reputation);

        writer.WriteEncodedInt(HaveFished.Count);
        foreach (var (index, amount) in HaveFished)
        {
            writer.WriteEncodedInt(index);
            writer.WriteEncodedInt(amount);
        }

        writer.WriteEncodedInt(TimesFished.Count);
        foreach (var (index, times) in TimesFished)
        {
            writer.WriteEncodedInt(index);
            writer.WriteEncodedInt(times);
        }
    }

    private PlayerFishingEntry()
    {
    }

    private static PlayerFishingEntry Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var entry = new PlayerFishingEntry
        {
            HasRecievedBritGal = reader.ReadBool(),
            Player = reader.ReadEntity<Mobile>(),
            Reputation = reader.ReadDouble()
        };

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var index = reader.ReadEncodedInt();
            var amount = reader.ReadEncodedInt();

            if (index >= 0 && index < FishQuestHelper.Fish.Length)
            {
                entry.HaveFished[index] = amount;
            }
        }

        count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var index = reader.ReadEncodedInt();
            var times = reader.ReadEncodedInt();

            if (index >= 0 && index < FishQuestHelper.Fish.Length)
            {
                entry.TimesFished[index] = times;
            }
        }

        return entry;
    }
}
