// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Core/StealingHandler.cs -> ours. Stealing from a creature's
// pack in a Felucca dungeon or a champion spawn region may also yield one of the thief's special items, once per creature
// (Stealing 100+; chance by skill and the creature's fame). Reached from pinned Skills/Stealing.cs through
// Stealing-monster-stealing.patch, exactly where ServUO's Stealing.cs:419-423 calls it.
//
// What differs from ServUO, each argued in shard-migration notes/cc-P127-port-treasure-maps-monster-stealing.md:
//   - HasBeenStolen: ServUO keeps a serialized flag on BaseCreature (BaseCreature.cs:326-332). Pinned's BaseCreature has
//     none and is not patched for it; the flag lives here, in a weak table, so it lasts until the creature is collected
//     or the server restarts (a restart lets a creature be stolen from once more).
//   - HandleSmugglersEdgeSteal and TryStealItem are not here: only the store-bought Smuggler's Edge weapon called them, and
//     the store is not ported.
//   - ExodusZealot's ritual items are not here: the Exodus Encounter is not ported (port list).
//   - The shard's gain rule (clusterf.stealing.monsterGain, default on): after the attempt the thief rolls a gain-only
//     Stealing check on (100, 100 + 2.5 x the creature's fame level), so a 30,000-fame creature trains to 200. ServUO's
//     HandleSteal rolls no skill check at all (the pack-item roll before it gains on weight, to 127.5 at most).

using System;
using System.Runtime.CompilerServices;
using Server.Engines.CannedEvil;
using Server.Items;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.CreatureStealing;

public static class StealingHandler
{
    // ServUO's order. A plain steal draws from the first nine (Random(Length - 2)): the lantern and the tool box came
    // only from the Smuggler's Edge.
    public static Type[] SpecialItemList { get; } =
    {
        typeof(SeedOfLife),
        typeof(BalmOfStrength),
        typeof(BalmOfWisdom),
        typeof(BalmOfSwiftness),
        typeof(ManaDraught),
        typeof(BalmOfProtection),
        typeof(StoneSkinLotion),
        typeof(GemOfSalvation),
        typeof(LifeShieldLotion),
        typeof(SmugglersLantern),
        typeof(SmugglersToolBox)
    };

    private static readonly object _marker = new();
    private static readonly ConditionalWeakTable<BaseCreature, object> _stolen = new();

    private static bool _monsterGain = true;

    /// <summary>The shard's rule: a gain-only Stealing roll against the creature's fame level after each attempt.</summary>
    public static bool MonsterGain
    {
        get => _monsterGain;
        set => _monsterGain = value;
    }

    public static void Configure()
    {
        _monsterGain = ServerConfiguration.GetSetting("clusterf.stealing.monsterGain", true);
    }

    public static bool HasBeenStolen(BaseCreature bc) => bc != null && _stolen.TryGetValue(bc, out _);

    public static void SetStolen(BaseCreature bc, bool value)
    {
        if (bc == null)
        {
            return;
        }

        if (value)
        {
            _stolen.AddOrUpdate(bc, _marker);
        }
        else
        {
            _stolen.Remove(bc);
        }
    }

    public static void HandleSteal(BaseCreature from, PlayerMobile thief)
    {
        if (from == null || thief == null)
        {
            return;
        }

        if (HasBeenStolen(from))
        {
            // That creature has already been stolen from.  There is nothing left to steal.
            thief.SendLocalizedMessage(1094948);
            return;
        }

        if (from.Controlled || from.Summoned)
        {
            thief.SendLocalizedMessage(502708); // You can't steal from this.
            return;
        }

        if (!CheckLocation(thief, from))
        {
            return;
        }

        var stealing = thief.Skills.Stealing.Value;

        if (stealing < 100)
        {
            return;
        }

        var chance = GetStealingChance(thief, from, stealing);

        if (Utility.Random(100) + 1 <= chance)
        {
            thief.SendLocalizedMessage(1094947); // You successfully steal a special item from the creature!

            var item = SpecialItemList[Utility.Random(SpecialItemList.Length - 2)].CreateInstance<Item>();

            if (item != null)
            {
                thief.AddToBackpack(item);
            }
        }

        SetStolen(from, true);

        if (_monsterGain)
        {
            RollShardGain(thief, from);
        }
    }

    /// <summary>ServUO: a Felucca dungeon, a champion spawn region (any facet), or an Exodus zealot (not ported).</summary>
    public static bool CheckLocation(Mobile thief, Mobile from)
    {
        var region = thief.Region;

        if (region == null)
        {
            return false;
        }

        return thief.Map == Map.Felucca && region.IsPartOf<DungeonRegion>() || region.IsPartOf<ChampionSpawnRegion>();
    }

    /// <summary>ServUO's chance in whole percent: 2 at 100, 5 from 100.1, 8 from 110.1, 10 at 120, plus 1 to 5 by fame.</summary>
    public static int GetStealingChance(Mobile thief, BaseCreature from, double stealing)
    {
        var chance = 0;

        if (stealing == 120)
        {
            chance += 10;
        }
        else if (stealing >= 110.1)
        {
            chance += 8;
        }
        else if (stealing >= 100.1)
        {
            chance += 5;
        }
        else if (stealing == 100)
        {
            chance += 2;
        }

        var level = FameLevel(from);

        if (level >= 40)
        {
            chance += 5;
        }
        else if (level >= 35)
        {
            chance += 3;
        }
        else if (level >= 30)
        {
            chance += 2;
        }
        else if (level >= 25)
        {
            chance += 1;
        }

        return chance;
    }

    /// <summary>ServUO's fame-to-level line, 0 at fame 1 to 40 at fame 30,000 (clamped).</summary>
    public static int FameLevel(BaseCreature from)
    {
        var fame = Math.Clamp(from.Fame, 1, 30000);
        return (int)(40.0 / 29999.0 * fame - 40.0 / 29999.0);
    }

    /// <summary>The shard's gain window for a monster steal: (100, 100 + 2.5 x fame level), so 200 at level 40.</summary>
    public static double GainCeiling(BaseCreature from) => 100.0 + FameLevel(from) * 2.5;

    private static void RollShardGain(PlayerMobile thief, BaseCreature from)
    {
        var max = GainCeiling(from);

        if (max <= 100.0)
        {
            return;
        }

        // Gain only: the steal's outcome was decided above. Pinned's skill check goes through ClusterFSkillGain's wrapped
        // handlers, so the shard's rate and worn-bonus rules apply as for any skill use.
        thief.CheckTargetSkill(SkillName.Stealing, from, 100.0, max);
    }
}
