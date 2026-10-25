// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/FishQuestHelper.cs -> ours, High
// Seas, second half. The profession fish quests' tables and rules: the 54 fish an order can ask for (shore fish to 80
// Fishing; lobsters, crabs and ocean fish to 106; dungeon fish beyond), their names, the reward table, the fish
// mongers of the world and the choice of the port an order is delivered to.
//
// Rewards (GiveRewards), by tier and a roll: half the time bait (tier 1 for one of the 15 rare fish, tier 2 and 3 for
// any rare or legendary fish, tier 4 a legendary fish or charybdis; rarely enhanced), a quarter of the time a lava
// lobster trap or a magic hook, 15% a rare fishing book, 10% a Fishing power scroll (105 to 120 by tier) or an Oracle
// of the Sea or a dredging hook.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO's tier test checks 100 points before 150 and 210 (if/else if in rising order), so tiers 3 and 4 never come
//   and an order pays tier 1 or 2. A bug with a missing result is copied (docs/bug-list.md D-4 rule).
// - The reward fishing pole carries no magic attributes: pinned's pole has none for ServUO's runic roll.
// - Tier 4's permanent boat paint is not a reward: the shard has no boat paint (High Seas ship content).
// - GetRandomMonger leaves out mongers with no port name instead of drawing again until it finds one (ServUO's loop
//   never ends when none has one). A monger's port is its own Port, set where it is placed, else its region's name.
// - GetBoat is the player's nearest boat that they own (pinned's boats; there are no rowboats).

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Multis;

namespace Server.Engines.Quests;

public static class FishQuestHelper
{
    public static readonly Type[] Fish =
    {
        // to 80.0 Fishing (shore fish): indexes 0 to 11
        typeof(BluegillSunfish), typeof(BrookTrout), typeof(GreenCatfish),
        typeof(KokaneeSalmon), typeof(PikeFish), typeof(PumpkinSeedSunfish),
        typeof(RainbowTrout), typeof(RedbellyBream), typeof(SmallmouthBass),
        typeof(UncommonShiner), typeof(Walleye), typeof(YellowPerch),

        // to 106.0 (crustaceans 12 to 23, deep water fish 24 to 41)
        typeof(CrustyLobster), typeof(FredLobster), typeof(HummerLobster),
        typeof(RockLobster), typeof(ShovelNoseLobster), typeof(SpineyLobster),
        typeof(AppleCrab), typeof(BlueCrab), typeof(DungeonessCrab),
        typeof(KingCrab), typeof(RockCrab), typeof(SnowCrab),

        typeof(Amberjack), typeof(BlackSeabass), typeof(BlueGrouper),
        typeof(BlueFish), typeof(Bonefish), typeof(Bonito),
        typeof(CapeCod), typeof(CaptainSnook), typeof(Cobia),
        typeof(GraySnapper), typeof(Haddock), typeof(MahiMahi),
        typeof(RedDrum), typeof(RedGrouper), typeof(RedSnook),
        typeof(Shad), typeof(Tarpon), typeof(YellowfinTuna),

        // to 120.0 (dungeon fish): 42 to 53
        typeof(CragSnapper), typeof(CutThroatTrout), typeof(DarkFish),
        typeof(DemonTrout), typeof(DrakeFish), typeof(DungeonChub),
        typeof(GrimCisco), typeof(InfernalTuna), typeof(LurkerFish),
        typeof(OrcBass), typeof(SnaggletoothBass), typeof(TormentedPike)
    };

    public static readonly int[] Labels =
    {
        1116417, 1116415, 1116421,
        1116423, 1116414, 1116412,
        1116416, 1116418, 1116419,
        1116420, 1116422, 1116413,

        1116383, 1116382, 1116381,
        1116380, 1116384, 1116379,
        1116378, 1116374, 1116373,
        1116375, 1116376, 1116377,

        1116402, 1116396, 1116411,
        1116406, 1116409, 1116405,
        1116395, 1116408, 1116400,
        1116399, 1116394, 1116401,
        1116410, 1116407, 1116398,
        1116403, 1116397, 1116404,

        1116432, 1116427, 1116431,
        1116425, 1116429, 1116424,
        1116428, 1116433, 1116435,
        1116430, 1116426, 1116434
    };

    // [tier][category] -> the types one is drawn from. ServUO's table, less the boat paint (see the header).
    private static readonly Type[][][] _rewardTable =
    {
        new[]
        {
            new[] { typeof(Bait) },
            new[] { typeof(LavaLobsterTrap) },
            new[] { typeof(FishingGuideBook1), typeof(FishingGuideBook2) },
            new[] { typeof(PowerScroll), typeof(FishingPole) }
        },
        new[]
        {
            new[] { typeof(Bait) },
            new[] { typeof(LavaHook), typeof(LavaLobsterTrap), typeof(JunkProofHook) },
            new[] { typeof(FishingGuideBook1), typeof(FishingGuideBook2), typeof(FishingGuideBook3), typeof(FishingPole) },
            new[] { typeof(PowerScroll), typeof(OracleOfTheSea), typeof(DredgingHook) }
        },
        new[]
        {
            new[] { typeof(Bait) },
            new[] { typeof(LavaHook), typeof(DredgingHook), typeof(JunkProofHook), typeof(FishingPole) },
            new[] { typeof(FishingGuideBook3), typeof(FishingGuideBook4), typeof(FishingGuideBook5) },
            new[] { typeof(PowerScroll), typeof(OracleOfTheSea) }
        },
        new[]
        {
            new[] { typeof(Bait), typeof(JunkProofHook) },
            new[] { typeof(OracleOfTheSea), typeof(LavaHook), typeof(FishingPole) },
            new[] { typeof(FishingGuideBook4), typeof(FishingGuideBook5), typeof(FishingGuideBook6) },
            new[] { typeof(PowerScroll) }
        }
    };

    private static readonly List<FishMonger> _mongers = new();

    public static List<FishMonger> Mongers => _mongers;

    /// <summary>ServUO's tier from an order's points (its order of tests kept: see the header).</summary>
    public static int GetTier(double points)
    {
        var tier = 1;

        if (points >= 100)
        {
            tier = 2;
        }
        else if (points >= 150)
        {
            tier = 3;
        }
        else if (points >= 210)
        {
            tier = 4;
        }

        return tier;
    }

    /// <summary>ServUO's category: 10% the fourth, 15% the third, 25% the second, else the first.</summary>
    public static int GetCategory(double roll) =>
        roll switch
        {
            < .10 => 4,
            < .25 => 3,
            < .50 => 2,
            _     => 1
        };

    public static Type[] GetRewardTypes(int tier, int category) => _rewardTable[tier - 1][category - 1];

    public static void GiveRewards(Mobile from, PlayerFishingEntry entry, double points)
    {
        var pack = from.Backpack ?? from.BankBox;

        var tier = GetTier(points);
        var category = GetCategory(Utility.RandomDouble());
        var types = GetRewardTypes(tier, category);
        var item = Loot.Construct(types.RandomElement());

        if (item == null)
        {
            return;
        }

        if (item is PowerScroll scroll)
        {
            var chance = Utility.RandomDouble();

            var value = tier switch
            {
                2 => chance < 0.05 ? 120 : chance < 0.25 ? 115 : 110,
                3 => chance < 0.10 ? 120 : 115,
                4 => 120,
                // ServUO's tier 1: its "chance > 0.25" test makes 110 the common one and 105 the rarer.
                _ => chance < 0.01 ? 120 : chance < 0.05 ? 115 : chance > 0.25 ? 110 : 105
            };

            scroll.Skill = SkillName.Fishing;
            scroll.Value = value;
            from.SendLocalizedMessage(1149591); // You receive a power scroll.
        }
        else if (item is BaseBook)
        {
            from.SendLocalizedMessage(1149590); // You receive a rare book.
        }
        else if (item is Bait bait)
        {
            switch (tier)
            {
                case 1:
                    {
                        bait.Index = Utility.Random(15);

                        if (Utility.RandomDouble() <= 0.001)
                        {
                            bait.Enhanced = true;
                        }

                        break;
                    }
                case 2:
                case 3:
                    {
                        bait.Index = Utility.Random(34);

                        if (Utility.RandomDouble() <= (tier == 2 ? 0.005 : 0.01))
                        {
                            bait.Enhanced = true;
                        }

                        break;
                    }
                case 4:
                    {
                        bait.Index = Utility.RandomBool() ? 35 : Utility.RandomMinMax(16, 34);

                        if (Utility.RandomDouble() <= 0.05)
                        {
                            bait.Enhanced = true;
                        }

                        break;
                    }
            }

            if (FishInfo.GetTypeFromIndex(bait.Index) == typeof(Charydbis))
            {
                bait.UsesRemaining = 5;
                from.SendLocalizedMessage(1150871); // You receive charybdis bait

                if (Utility.RandomDouble() <= 0.08)
                {
                    bait.Enhanced = true;
                }
            }
            else
            {
                var label = FishInfo.GetFishLabel(bait.Index);

                if (label is int number)
                {
                    from.SendLocalizedMessage(1149588, $"{number:#}\t"); // You receive bait to catch ~1_val~
                }
                else
                {
                    from.SendLocalizedMessage(1149588, label as string); // You receive bait to catch ~1_val~
                }

                bait.UsesRemaining = (int)Math.Min(200, points / 2);
            }
        }
        else
        {
            from.SendLocalizedMessage(1149589); // You receive some rare fishing equipment.
        }

        pack?.DropItem(item);
        entry?.OnAfterReward(points);
    }

    public static void AddMonger(FishMonger monger)
    {
        if (monger != null && !_mongers.Contains(monger))
        {
            _mongers.Add(monger);
        }
    }

    public static void RemoveMonger(FishMonger monger) => _mongers.Remove(monger);

    /// <summary>
    /// ServUO's choice of the port to deliver to: any other monger, not one in the quest giver's port (on either facet),
    /// not one another of the player's orders goes to, and only one with a port name.
    /// </summary>
    public static FishMonger GetRandomMonger(PlayerMobile player, FishMonger quester)
    {
        var mongers = new List<FishMonger>();
        var orders = ShippingCrate.OrdersOf(player);

        foreach (var m in _mongers)
        {
            if (m == quester || m.Deleted || m.PortName == null || m.PortName == quester.PortName)
            {
                continue;
            }

            if (orders.Exists(o => o.TurnIn == m))
            {
                continue;
            }

            mongers.Add(m);
        }

        return mongers.Count == 0 ? null : mongers.RandomElement();
    }

    /// <summary>The player's own boat nearest to them, or null.</summary>
    public static BaseBoat GetBoat(Mobile from)
    {
        BaseBoat closest = null;
        var range = int.MaxValue;

        foreach (var boat in BaseBoat.Boats)
        {
            if (boat.Owner != from || boat.Deleted)
            {
                continue;
            }

            var dist = (int)from.GetDistanceToSqrt(boat.Location);

            if (closest == null || dist < range)
            {
                closest = boat;
                range = dist;
            }
        }

        return closest;
    }

    /// <summary>How far into the fish table a player's orders may reach: shore fish under 80 Fishing, then 106.</summary>
    public static int GetIndexForSkillLevel(Mobile from)
    {
        if (from == null)
        {
            return 0;
        }

        var skill = from.Skills.Fishing.Base;

        return skill < 80.0 ? 11 : skill < 106.0 ? 41 : Fish.Length;
    }

    public static int GetIndexForType(Type type)
    {
        for (var i = 0; i < Fish.Length; i++)
        {
            if (Fish[i] == type)
            {
                return i;
            }
        }

        return 0;
    }

    public static Type GetTypeFromIndex(int index) => index < 0 || index >= Fish.Length ? null : Fish[index];

    public static bool IsShallowWaterFish(Type type) => GetIndexForType(type) <= 11;

    public static bool IsCrustacean(Type type)
    {
        var index = GetIndexForType(type);
        return index is > 11 and <= 23;
    }

    public static bool IsDeepWaterFish(Type type)
    {
        var index = GetIndexForType(type);
        return index is > 11 and <= 41;
    }

    public static bool IsDungeonFish(Type type) => GetIndexForType(type) > 41;
}
