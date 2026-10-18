// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Core/StealingHandler.cs -> ours. Stealing from a creature's
// pack may also yield a special prize. Reached from pinned Skills/Stealing.cs through Stealing-monster-stealing.patch,
// exactly where ServUO's Stealing.cs:419-423 calls it.
//
// cc-P142 layers the shard's rules on the port (Chase's decisions of 2026-10-16, shard-migration
// notes/cc-P142-monster-stealing-shard-rules.md; each is a registered deviation, OSI's line kept beside it):
//   - Where and who: ThievesDenStealRules (Den membership, the rank ladder, every facet, the tutorial dungeon at
//     Initiate, the shard's later dungeons and bosses at Master, the never list). OSI: Felucca dungeons and champion
//     spawns, anyone with Stealing 100.
//   - The chance (GetStealingChance): a line from 5% at 100 to 35% at 200, with OSI's fame bonus (1 to 5) on top, and a
//     minor tier from 2% at 60 to 5% at 100. OSI (GetServUOStealingChance, kept for the register and the facts): 2 / 5 /
//     8 / 10 at 100 / 100.1 / 110.1 / 120, nothing past 120, nothing below 100.
//   - One SUCCESS per creature (decision 3a), not one attempt: a miss leaves the creature open after RetryCooldown, and
//     costs the thief: the creature turns on them. OSI marks the creature on any attempt.
//   - Two loot tiers (StealLootTables): the minor table below 100, OSI's specials and the new loot from 100.
//   - Saved state (MonsterStealingStore): the stolen flag and the gem timer outlive a restart (decision 6).
//   - The shard's gain rule (cc-P127, clusterf.stealing.monsterGain, default on): after every legal attempt the thief
//     rolls a gain-only Stealing check on (60, 100 + 2.5 x the creature's fame level), so a 30,000-fame creature
//     trains to 200 and a weak one to 100. ServUO's HandleSteal rolls no skill check at all.
//   - HandleSmugglersEdgeSteal, TryStealItem and the ExodusZealot branch are not here (cc-P127: the store and the Exodus
//     Encounter are not ported).

using System;
using System.Runtime.CompilerServices;
using Server.Items;
using Server.Mobiles;

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

    // -- The shard's numbers (cc-P142) ----------------------------------------------------------------------------

    /// <summary>Stealing below this never steals from a monster (decision 3: the lower tier starts at 60).</summary>
    public const double Floor = 60.0;

    /// <summary>OSI's specials from here (decision 3); the minor table below.</summary>
    public const double SpecialFrom = 100.0;

    public const double SkillCeiling = 200.0;

    // The curve: minor tier 2% at 60 rising 0.075 a point to 5% at 100; special tier 5% at 100 rising 0.30 a point to
    // 35% at 200. OSI's fame bonus (FameBonus, 1 to 5) adds on top of both.
    public const double MinorBase = 2.0;
    public const double MinorSlope = 0.075;
    public const double SpecialBase = 5.0;
    public const double SpecialSlope = 0.30;

    /// <summary>After a miss, this creature cannot be tried again for this long (decision 3a: a few seconds).</summary>
    public static TimeSpan RetryCooldown { get; set; } = TimeSpan.FromSeconds(5);

    public const string WaryLine = "The creature is wary of your hands. Give it a moment.";
    public const string CaughtLine = "The creature catches your hand in its hoard and turns on you!";
    public const string MinorSuccessLine = "You slip something small from the creature.";

    private static readonly ConditionalWeakTable<BaseCreature, StrongBox<DateTime>> _nextRetry = new();

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

    public static bool HasBeenStolen(BaseCreature bc) => MonsterStealingStore.HasBeenStolen(bc);

    public static void SetStolen(BaseCreature bc, bool value) => MonsterStealingStore.SetStolen(bc, value);

    public static DateTime NextRetry(BaseCreature bc) =>
        bc != null && _nextRetry.TryGetValue(bc, out var box) ? box.Value : DateTime.MinValue;

    public static void SetNextRetry(BaseCreature bc, DateTime when)
    {
        if (bc != null)
        {
            _nextRetry.GetOrCreateValue(bc).Value = when;
        }
    }

    /// <summary>What the last HandleSteal did, for the facts and the log.</summary>
    public enum Outcome
    {
        Nothing,
        AlreadyStolen,
        Never,
        NotHere,
        Refused,
        TooLittleSkill,
        Wary,
        Missed,
        StoleMinor,
        StoleSpecial
    }

    public static Outcome LastOutcome { get; private set; }

    public static void HandleSteal(BaseCreature from, PlayerMobile thief)
    {
        LastOutcome = Outcome.Nothing;

        if (from == null || thief == null)
        {
            return;
        }

        if (HasBeenStolen(from))
        {
            // That creature has already been stolen from.  There is nothing left to steal.
            thief.SendLocalizedMessage(1094948);
            LastOutcome = Outcome.AlreadyStolen;
            return;
        }

        if (ThievesDenStealRules.IsNeverStolen(from))
        {
            thief.SendLocalizedMessage(502708); // You can't steal from this.
            LastOutcome = Outcome.Never;
            return;
        }

        if (!ThievesDenStealRules.CheckPlaceAndRank(thief, from, out _, out var refusal))
        {
            if (refusal != null)
            {
                thief.SendMessage(0x22, refusal);
                LastOutcome = Outcome.Refused;
            }
            else
            {
                LastOutcome = Outcome.NotHere;
            }

            return;
        }

        var stealing = thief.Skills.Stealing.Value;

        if (stealing < Floor)
        {
            LastOutcome = Outcome.TooLittleSkill;
            return;
        }

        if (Core.Now < NextRetry(from))
        {
            thief.SendMessage(0x22, WaryLine);
            LastOutcome = Outcome.Wary;
            return;
        }

        var chance = GetStealingChance(thief, from, stealing);

        if (Utility.RandomDouble() * 100.0 < chance)
        {
            if (stealing < SpecialFrom)
            {
                thief.SendMessage(54, MinorSuccessLine);
                StealLootTables.GiveMinor(thief, from, StealLootTables.MinorPick(Utility.Random(100)));
                LastOutcome = Outcome.StoleMinor;
            }
            else
            {
                thief.SendLocalizedMessage(1094947); // You successfully steal a special item from the creature!

                var item = StealLootTables.MakeSpecial(from, StealLootTables.SpecialPick(Utility.RandomDouble()));

                if (item != null)
                {
                    thief.AddToBackpack(item);
                }

                LastOutcome = Outcome.StoleSpecial;
            }

            StealLootTables.TryFamilyPart(thief, from);
            SetStolen(from, true);
        }
        else
        {
            Miss(from, thief);
            LastOutcome = Outcome.Missed;
        }

        if (_monsterGain)
        {
            RollShardGain(thief, from);
        }
    }

    /// <summary>A miss: the creature stays open after the cooldown, the thief is revealed and the creature turns on them.</summary>
    public static void Miss(BaseCreature from, PlayerMobile thief)
    {
        SetNextRetry(from, Core.Now + RetryCooldown);
        thief.SendMessage(0x22, CaughtLine);
        thief.RevealingAction();

        if (!from.Deleted && from.Alive)
        {
            from.AggressiveAction(thief, false);
            from.Combatant = thief;
        }
    }

    /// <summary>
    /// The shard's chance in percent (cc-P142, decisions 2 and 3): nothing below 60; 2 + 0.075 a point to 100; 5 + 0.30 a
    /// point from 100 to 200 (5 / 11 / 23 / 35 at 100 / 120 / 160 / 200); plus OSI's fame bonus (1 to 5).
    /// </summary>
    public static double GetStealingChance(Mobile thief, BaseCreature from, double stealing)
    {
        if (stealing < Floor)
        {
            return 0.0;
        }

        var chance = stealing < SpecialFrom
            ? MinorBase + (stealing - Floor) * MinorSlope
            : SpecialBase + (Math.Min(stealing, SkillCeiling) - SpecialFrom) * SpecialSlope;

        return chance + FameBonus(from);
    }

    /// <summary>OSI's fame bonus: 1 / 2 / 3 / 5 at fame level 25 / 30 / 35 / 40.</summary>
    public static int FameBonus(BaseCreature from)
    {
        var level = FameLevel(from);

        return level >= 40 ? 5 :
            level >= 35 ? 3 :
            level >= 30 ? 2 :
            level >= 25 ? 1 : 0;
    }

    /// <summary>ServUO's chance in whole percent, kept for the register: 2 at 100, 5 from 100.1, 8 from 110.1, 10 at 120, plus the fame bonus.</summary>
    public static int GetServUOStealingChance(Mobile thief, BaseCreature from, double stealing)
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

        return chance + FameBonus(from);
    }

    /// <summary>ServUO's fame-to-level line, 0 at fame 1 to 40 at fame 30,000 (clamped).</summary>
    public static int FameLevel(BaseCreature from)
    {
        var fame = Math.Clamp(from.Fame, 1, 30000);
        return (int)(40.0 / 29999.0 * fame - 40.0 / 29999.0);
    }

    /// <summary>The shard's gain window for a monster steal: (Floor, 100 + 2.5 x fame level), so 200 at level 40.</summary>
    public static double GainCeiling(BaseCreature from) => 100.0 + FameLevel(from) * 2.5;

    private static void RollShardGain(PlayerMobile thief, BaseCreature from)
    {
        // Gain only: the steal's outcome was decided above. Pinned's skill check goes through ClusterFSkillGain's wrapped
        // handlers, so the shard's rate and worn-bonus rules apply as for any skill use; a thief whose Stealing is at or
        // above the window's top gains nothing (pinned's no-challenge rule), so the creature's fame sets the ceiling.
        thief.CheckTargetSkill(SkillName.Stealing, from, Floor, GainCeiling(from));
    }
}
