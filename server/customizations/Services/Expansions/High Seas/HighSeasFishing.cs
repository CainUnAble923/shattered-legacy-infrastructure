// cc-P269: the High Seas half of ServUO pub57 Scripts/Services/Harvest/Fishing.cs, the parts pinned's Fishing has no
// place for. Fishing-high-seas-bait-hooks-lava.patch (pinned Engines/Harvest/Fishing.cs) calls in here; nothing else does.
//
//   - Hooks: HasTypeHook, and the dredging hook's barnacle-covered shipwreck items (ServUO passes a "dredge" flag to
//     its ShipwreckedItem; pinned's ShipwreckedItem has no such field, so a dredged item is our BarnacleShipwreckedItem).
//   - Lava fishing (ServUO Fishing.cs:789-1060): a pole with a lava hook fishes lava tiles. Pinned's harvest system picks
//     the definition from the tile alone (HarvestSystem.GetDefinition(tileID, isLand), no tool), so lava has its own
//     definition here, LavaDefinition, which the patch starts (StartLavaHarvest) and finishes (FinishLavaHarvest) itself
//     when the pole carries a lava hook and the target is lava. ServUO's lava harvest has its own rules: a skill check,
//     then the lava catch table (stone footwear, cracked lava rock, stone pavers, a weapon) and, failing that, only a
//     rare fish of the region (the Abyss's lava fish); a failed cast may still wear the hook.
//   - The charybdis (ServUO Fishing.SpecialHarvest's High Seas region): with the Oracle of the Sea in the pack and a pole
//     baited for charybdis, at 100 Fishing, a cast into the spot the Oracle named raises Charybdis.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
//   - Lava's banks are their own (LavaDefinition's), where ServUO's lava casts drew on the water definition's banks; lava
//     and water never share a tile, so no spot fishes differently.
//   - ServUO keeps lava land and static tiles in one list (LavaLobsterTrap.LavaTiles); pinned's definitions keep land and
//     statics apart, so 0x1F4-0x1F7 are the land tiles and the rest the statics.
//   - The lava catch's weapon row gives a random weapon without ServUO's Searing socket (pinned and ours have no item
//     sockets or Searing effect); its "mysterious weapon" message is kept.

using System;
using Server.Engines.Harvest;
using Server.Mobiles;
using Server.Multis;

namespace Server.Items;

public static class HighSeasFishing
{
    private static HarvestDefinition _lavaDefinition;

    // ServUO Fishing.m_LavaMutateTable: (required skill, min skill, max skill, types). A row's chance is
    // (Fishing - min) / (max - min), half again in Felucca.
    private static readonly (double Req, double Min, double Max, Type[] Types)[] _lavaMutateTable =
    {
        (0.0, 80.0, 333.0, new[] { typeof(StoneFootwear) }),
        (80.0, 80.0, 333.0, new[] { typeof(CrackedLavaRockEast), typeof(CrackedLavaRockSouth) }),
        (85.0, 80.0, 333.0, new[] { typeof(StonePaver) }),
        (80.0, 80.0, 4080.0, new[] { typeof(BaseWeapon) })
    };

    private static readonly int[] _lavaLandTiles = { 0x1F4, 0x1F5, 0x1F6, 0x1F7 };

    public static HarvestDefinition LavaDefinition => _lavaDefinition ??= BuildLavaDefinition();

    private static HarvestDefinition BuildLavaDefinition()
    {
        var water = Fishing.System.Definitions[0];
        var statics = new int[LavaLobsterTrap.LavaTiles.Length - _lavaLandTiles.Length];
        Array.Copy(LavaLobsterTrap.LavaTiles, _lavaLandTiles.Length, statics, 0, statics.Length);

        return new HarvestDefinition
        {
            BankWidth = water.BankWidth,
            BankHeight = water.BankHeight,
            MinTotal = water.MinTotal,
            MaxTotal = water.MaxTotal,
            MinRespawn = water.MinRespawn,
            MaxRespawn = water.MaxRespawn,
            Skill = SkillName.Fishing,
            LandTiles = _lavaLandTiles,
            StaticTiles = statics,
            RangedTiles = false, // a list of single tiles, not the water definition's (low, high) pairs
            MaxRange = water.MaxRange,
            ConsumedPerHarvest = 1,
            ConsumedPerFeluccaHarvest = 1,
            EffectActions = water.EffectActions,
            EffectSounds = water.EffectSounds,
            EffectCounts = water.EffectCounts,
            EffectDelay = water.EffectDelay,
            EffectSoundDelay = water.EffectSoundDelay,
            NoResourcesMessage = water.NoResourcesMessage,
            FailMessage = water.FailMessage,
            TimedOutOfRangeMessage = water.TimedOutOfRangeMessage,
            OutOfRangeMessage = water.OutOfRangeMessage,
            PackFullMessage = water.PackFullMessage,
            ToolBrokeMessage = water.ToolBrokeMessage,
            Resources = water.Resources,
            Veins = water.Veins
        };
    }

    public static bool HasTypeHook(Item tool, HookType type) => tool is FishingPole pole && pole.HookType == type;

    /// <summary>A shipwreck item for an SOS haul: barnacle covered when the pole carries a dredging hook.</summary>
    public static ShipwreckedItem Shipwrecked(int itemID, Item tool) =>
        HasTypeHook(tool, HookType.Dredging) ? new BarnacleShipwreckedItem(itemID) : new ShipwreckedItem(itemID);

    // ServUO Fishing.Construct's High Seas case (Fishing.cs:437-456): fish heads, oars, unfinished drawers and legs.
    private static readonly int[] _sosOddments =
    {
        0x1E19, 0x1E1A, 0x1E1B, // fish heads
        0x1E2A, 0x1E2B,         // oars
        0x1E71, 0x1E7A,         // unfinished drawers
        0x1E75                  // unfinished legs
    };

    /// <summary>
    /// The High Seas number of an SOS haul's roll (1 in 17): the yellow polka dot bikini 5% of the time, an oar, drawer or
    /// table legs 20%, else a fish head.
    /// </summary>
    public static Item SosOddment(Item tool)
    {
        var ran = Utility.RandomDouble();

        if (ran < 0.05)
        {
            return new YellowPolkaDotBikini();
        }

        return Shipwrecked(ran < 0.25 ? _sosOddments[Utility.RandomMinMax(3, 7)] : _sosOddments[Utility.Random(3)], tool);
    }

    /// <summary>ServUO Fishing.IsLavaHarvest: a lava-hooked pole aimed at a lava land tile or lava static.</summary>
    public static bool IsLavaHarvest(HarvestSystem system, Mobile from, Item tool, object toHarvest) =>
        HasTypeHook(tool, HookType.Lava) &&
        system.GetHarvestDetails(from, tool, toHarvest, out var tileID, out _, out _, out var isLand) &&
        LavaDefinition.Validate(tileID, isLand);

    /// <summary>Pinned HarvestSystem.StartHarvesting with the lava definition in place of the tile's.</summary>
    public static void StartLavaHarvest(HarvestSystem system, Mobile from, Item tool, object toHarvest)
    {
        if (!system.CheckHarvest(from, tool))
        {
            return;
        }

        if (!system.GetHarvestDetails(from, tool, toHarvest, out _, out var map, out var loc, out _))
        {
            system.OnBadHarvestTarget(from, tool, toHarvest);
            return;
        }

        var def = LavaDefinition;

        if (!system.CheckRange(from, tool, def, map, loc, false) ||
            !system.CheckResources(from, tool, def, map, loc, false) ||
            !system.CheckHarvest(from, tool, def, toHarvest))
        {
            return;
        }

        var toLock = system.GetLock(from, tool, def, toHarvest);

        if (!from.BeginAction(toLock))
        {
            system.OnConcurrentHarvest(from, tool, def, toHarvest);
            return;
        }

        new HarvestTimer(from, tool, system, def, toHarvest, toLock).Start();
        system.OnHarvestStarted(from, tool, def, toHarvest);
    }

    /// <summary>ServUO Fishing.FinishHarvesting's lava branch (Fishing.cs:929-1026).</summary>
    public static void FinishLavaHarvest(
        HarvestSystem system, Mobile from, Item tool, HarvestDefinition def, object toHarvest, object locked
    )
    {
        from.EndAction(locked);

        if (!system.CheckHarvest(from, tool))
        {
            return;
        }

        if (!system.GetHarvestDetails(from, tool, toHarvest, out var tileID, out var map, out var loc, out var isLand) ||
            !def.Validate(tileID, isLand))
        {
            system.OnBadHarvestTarget(from, tool, toHarvest);
            return;
        }

        if (!system.CheckRange(from, tool, def, map, loc, true) ||
            !system.CheckResources(from, tool, def, map, loc, true) ||
            !system.CheckHarvest(from, tool, def, toHarvest))
        {
            return;
        }

        var bank = def.GetBank(map, loc.X, loc.Y);
        var vein = bank?.Vein;

        if (vein == null)
        {
            return;
        }

        Type type = null;
        var resource = system.MutateResource(from, tool, def, map, loc, vein, vein.PrimaryResource, vein.FallbackResource);

        if (from.CheckSkill(def.Skill, resource.MinSkill, resource.MaxSkill))
        {
            // Special eye candy item, else a rare fish of these parts (rareOnly: no common fish from lava).
            type = GetSpecialLavaItem(from, map) ?? FishInfo.GetSpecialItem(from, tool, loc, true);

            if (type != null)
            {
                var item = system.Construct(type, from);

                if (item == null)
                {
                    type = null;
                }
                else
                {
                    if (from.AccessLevel == AccessLevel.Player)
                    {
                        bank.Consume(map?.Rules == MapRules.FeluccaRules ? (int)Math.Ceiling(item.Amount / 2.0) : item.Amount, from);
                    }

                    if (system.Give(from, item, true))
                    {
                        system.SendSuccessTo(from, item, null);
                    }
                    else
                    {
                        system.SendPackFullTo(from, item, def, null);
                        item.Delete();
                    }
                }
            }
        }

        if (type == null)
        {
            def.SendMessageTo(from, def.FailMessage);

            var skill = from.Skills.Fishing.Value / 50;

            if (0.5 / skill > Utility.RandomDouble())
            {
                (tool as FishingPole)?.OnFishedHarvest(from, false);
            }
        }
        else
        {
            (tool as FishingPole)?.OnFishedHarvest(from, true);
        }

        system.OnHarvestFinished(from, tool, def, vein, bank, null, null);
    }

    /// <summary>
    /// ServUO Fishing.GetSpecialLavaItem: every row of the lava table rolls in turn and the last hit wins, except the
    /// weapon row, which puts a random weapon in the pack at once and ends the roll (the cast then reads as a miss, as
    /// in ServUO, whose Construct returns nothing for BaseWeapon).
    /// </summary>
    public static Type GetSpecialLavaItem(Mobile from, Map map)
    {
        Type newType = null;

        var skillBase = from.Skills.Fishing.Base;
        var skillValue = Math.Min(120.0, from.Skills.Fishing.Value);

        foreach (var (req, min, max, types) in _lavaMutateTable)
        {
            if (skillBase < req)
            {
                continue;
            }

            var chance = (skillValue - min) / (max - min);

            if (map?.Rules == MapRules.FeluccaRules)
            {
                chance *= 1.5;
            }

            if (chance <= Utility.RandomDouble())
            {
                continue;
            }

            newType = types.RandomElement();

            if (newType == typeof(BaseWeapon))
            {
                var wep = Loot.RandomWeapon();

                if (wep != null)
                {
                    from.AddToBackpack(wep);
                    from.SendMessage("You have pulled out an item : mysterious weapon");
                    return typeof(BaseWeapon);
                }
            }
        }

        return newType == typeof(BaseWeapon) ? null : newType;
    }

    /// <summary>
    /// ServUO Fishing.SpecialHarvest's High Seas region. True when the cast raised Charybdis (the harvest stops there).
    /// </summary>
    public static bool TrySummonCharybdis(Mobile from, Item tool, Point3D loc)
    {
        var sp = CharydbisSpawner.SpawnInstance;

        if (tool is not FishingPole pole || sp?.IsSummoned != true || from.Backpack == null)
        {
            return false;
        }

        if (from.Backpack.FindItemByType<OracleOfTheSea>() == null)
        {
            return false;
        }

        if (from.Map != sp.Map)
        {
            from.SendLocalizedMessage(1150861); // Charybdis have never been seen in these waters, try somewhere else.
        }
        else if (pole.BaitType == typeof(Charydbis) && from.Skills.Fishing.Value >= 100)
        {
            if (sp.Charydbis == null && !sp.HasSpawned && sp.CurrentLocation.Contains(loc))
            {
                var boat = BaseBoat.FindBoatAt(from.Location, from.Map);
                sp.SpawnCharydbis(from, loc, sp.Map, boat);
                sp.HasSpawned = true;
                pole.OnFishedHarvest(from, true);
                return true;
            }

            if (sp.LastLocation.Contains(loc))
            {
                from.SendLocalizedMessage(1150862); // The charybdis has moved on from this location, consult Oracle Of The Seas again.
            }
        }
        else
        {
            from.SendLocalizedMessage(1150858); // You see a few bubbles, but no charybdis.
        }

        return false;
    }
}

/// <summary>
/// cc-P269: a shipwreck item pulled up with a dredging hook, ServUO's ShipwreckedItem with its barnacle flag set: "barnacle
/// covered ~1_token~" and "recovered from a shipwreck". Pinned's ShipwreckedItem has no barnacle field, so the flag is
/// this type.
/// </summary>
[ModernUO.Serialization.SerializationGenerator(0, false)]
public partial class BarnacleShipwreckedItem : ShipwreckedItem
{
    public BarnacleShipwreckedItem(int itemID) : base(itemID)
    {
    }

    public override void AddNameProperties(IPropertyList list)
    {
        if (LabelNumber > 0)
        {
            list.Add(1151075, $"{LabelNumber:#}"); // barnacle covered ~1_token~
        }
        else
        {
            list.Add(1151075, ItemData.Name); // barnacle covered ~1_token~
        }

        list.Add(1041645); // recovered from a shipwreck
    }
}
