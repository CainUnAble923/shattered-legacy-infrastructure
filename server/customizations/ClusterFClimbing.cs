// ClusterFClimbing.cs
//
// cc-P238 (F-36, ours: OSI has no climbing). Climbing boots hop a miner onto impassable mountain tiles, to mine the 8 x 8
// ore banks no walkable tile within Mining's reach of 2 can touch (notes: 11,748 of 47,071 ore banks on Felucca and
// 13,124 of 48,799 on Trammel, counted from the map by notes/cc-P238-tools/unreachable_banks.py).
//
//   The hop     wear the boots, use them, target a tile within the tier's range (4 / 8 / 12, Chebyshev). The landing
//               tile must be mountain (impassable land on Mining's own mountain list) or walkable, its z is read from
//               the map (never from the target), and nothing may stand in the body's 16 z there (Map.CanFit: a hop
//               never lands inside a static). Refused into a town, a dungeon, a house or its region, and anywhere a
//               teleport may not go (SpellHelper.CheckTravel TeleportTo). Costs stamina, more for a higher climb (half
//               on Delver's and Deepwarden's), with a cooldown. No charges and no durability.
//   Ledge memory  Deepwarden's boots remember the last climbed tile; from the ground within LedgeRange of it, targeting
//               yourself hops straight back up (one hop, the usual cost and checks).
//   On a climbed tile  (standing on mountain land, ClusterFClimbing.IsOnClimbedTile) a player may mine, prospect and
//               hop only: no attacks or harmful spells (PlayerMobile.CanBeHarmful), no taming (AllowSkillUse), no Mark
//               (CheckSpellCast), no house placement (HousePlacement.Check); those four are one-line patches
//               (server/patches/PlayerMobile-climbed-tile-rules.patch, HousePlacement-climbed-tile.patch). Harm done TO a
//               climber is the attacker's own check, so hostile spells still reach them.
//   Pets        the hop up orders followers in range to stay; the hop down orders followers in range to follow. A pet is
//               never released, and none is moved.
//
// Pinned facts this rests on (notes Part 1): MoveToWorld places a mobile anywhere and keeps it there; logout keeps
// LogoutLocation and login restores it unchecked (Mobile.cs:1320-1323); from impassable land the start z is the mobile's
// own (Movement.GetStartZ), so the next step can only be onto walkable ground within a step's height; Map.CanFit with
// requireSurface false accepts impassable land at its own average z and refuses any static overlapping the body.
//
// Every number is a setting (clusterf.climbing.*), read at Configure; the defaults are F-36's placeholders.

using System;
using System.Collections.Generic;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells;
using Server.Spells.Sixth;

namespace Server;

public enum ClimbTileKind
{
    Blocked,
    Walkable,
    Mountain
}

/// <summary>What a mobile would stand on at a tile, and at what z.</summary>
public readonly record struct ClimbTile(ClimbTileKind Kind, int Z);

/// <summary>Reads the terrain for the hop. The shard reads the map; the facts give a grid (the test host has no map).</summary>
public interface IClimbTerrain
{
    ClimbTile Probe(Map map, int x, int y, int nearZ);
}

/// <summary>The map as pinned reads it: land tile and flags, average z, statics, Map.CanFit.</summary>
public sealed class MapClimbTerrain : IClimbTerrain
{
    public const int BodyHeight = 16;

    public ClimbTile Probe(Map map, int x, int y, int nearZ)
    {
        if (map == null || map == Map.Internal || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return new ClimbTile(ClimbTileKind.Blocked, 0);
        }

        var land = map.Tiles.GetLandTile(x, y);
        var flags = TileData.LandTable[land.ID & TileData.MaxLandValue].Flags;
        map.GetAverageZ(x, y, out _, out var avg, out _);

        // Walkable: passable land at its average z, or the top of a surface static; the one nearest the climber's z.
        var best = int.MinValue;
        var found = false;

        void Consider(int z)
        {
            if (map.CanFit(x, y, z, BodyHeight, false, true) && (!found || Math.Abs(z - nearZ) < Math.Abs(best - nearZ)))
            {
                best = z;
                found = true;
            }
        }

        if (!land.Ignored && (flags & (TileFlag.Impassable | TileFlag.Wet)) == 0)
        {
            Consider(avg);
        }

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            var id = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            if (id.Surface && !id.Impassable && !id.Wet)
            {
                Consider(tile.Z + id.CalcHeight);
            }
        }

        if (found)
        {
            return new ClimbTile(ClimbTileKind.Walkable, best);
        }

        // Mountain: impassable land on Mining's mountain list, with room for a body at its average z and no static there.
        if ((flags & TileFlag.Impassable) != 0 && (flags & TileFlag.Wet) == 0 &&
            Mining.System.OreAndStone.Validate(land.ID, true) &&
            map.CanFit(x, y, avg, BodyHeight, false, true, false))
        {
            return new ClimbTile(ClimbTileKind.Mountain, avg);
        }

        return new ClimbTile(ClimbTileKind.Blocked, 0);
    }
}

public static class ClusterFClimbing
{
    // ---------------------------------------------------------------- settings (F-36 placeholders)

    public sealed record TierSettings(
        string Name, int HopRange, int RankStanding, double Mining, int BlocksMined, int Vouchers, int Gold, Type IngotType,
        int Ingots);

    public static TierSettings[] Tiers { get; private set; } = DefaultTiers();

    public static int CooldownMs { get; private set; } = 2500;
    public static int StaminaBase { get; private set; } = 5;
    public static int StaminaZPerPoint { get; private set; } = 4;   // one more stamina per this many z climbed
    public static int MaxClimbZ { get; private set; } = 60;         // highest climb in one hop
    public static int LedgeRange { get; private set; } = 30;
    public static int PetRange { get; private set; } = 15;
    public static int RestoreVouchers { get; private set; } = 5;

    // Ingots per tier from a metal that tier's Mining can work (ClusterFMetalTiers: Platinum 112.5, Frost 150, Mythril 175).
    private static TierSettings[] DefaultTiers() =>
    [
        new("Surveyor's Climbing Boots", 4, 15000, 120.0, 100, 75, 20000, typeof(PlatinumIngot), 100),
        new("Delver's Climbing Boots", 8, 40000, 150.0, 300, 150, 50000, typeof(FrostIngot), 150),
        new("Deepwarden's Climbing Boots", 12, 80000, 180.0, 600, 250, 100000, typeof(MythrilIngot), 200)
    ];

    public static void Configure()
    {
        var t = DefaultTiers();
        for (var i = 0; i < t.Length; i++)
        {
            var p = $"clusterf.climbing.t{i + 1}.";
            t[i] = t[i] with
            {
                HopRange = ServerConfiguration.GetOrUpdateSetting(p + "hopRange", t[i].HopRange),
                RankStanding = ServerConfiguration.GetOrUpdateSetting(p + "rankStanding", t[i].RankStanding),
                Mining = ServerConfiguration.GetOrUpdateSetting(p + "mining", t[i].Mining),
                BlocksMined = ServerConfiguration.GetOrUpdateSetting(p + "blocksMined", t[i].BlocksMined),
                Vouchers = ServerConfiguration.GetOrUpdateSetting(p + "vouchers", t[i].Vouchers),
                Gold = ServerConfiguration.GetOrUpdateSetting(p + "gold", t[i].Gold),
                Ingots = ServerConfiguration.GetOrUpdateSetting(p + "ingots", t[i].Ingots)
            };
        }

        Tiers = t;
        CooldownMs = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.cooldownMs", CooldownMs);
        StaminaBase = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.staminaBase", StaminaBase);
        StaminaZPerPoint = Math.Max(1, ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.staminaZPerPoint", StaminaZPerPoint));
        MaxClimbZ = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.maxClimbZ", MaxClimbZ);
        LedgeRange = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.ledgeRange", LedgeRange);
        PetRange = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.petRange", PetRange);
        RestoreVouchers = ServerConfiguration.GetOrUpdateSetting("clusterf.climbing.restoreVouchers", RestoreVouchers);
    }

    public static TierSettings Tier(int tier) => Tiers[Math.Clamp(tier, 1, Tiers.Length) - 1];

    /// <summary>True at a blocks-mined count that opens a boots tier (the logbook says so when it is reached).</summary>
    public static bool IsBlocksGate(int count) => Array.Exists(Tiers, t => t.BlocksMined == count);

    // ---------------------------------------------------------------- terrain

    /// <summary>The terrain the hop reads. The shard's is the map; a fact may set a grid for its own duration.</summary>
    public static IClimbTerrain Terrain { get; set; } = new MapClimbTerrain();

    /// <summary>
    /// True while a player stands on a climbed tile: mountain land, with no walkable surface under them. Staff are never
    /// held to the climbed-tile rules.
    /// </summary>
    public static bool IsOnClimbedTile(Mobile m) =>
        m is PlayerMobile { Deleted: false } && m.AccessLevel == AccessLevel.Player && m.Map != null && m.Map != Map.Internal &&
        Terrain.Probe(m.Map, m.X, m.Y, m.Z).Kind == ClimbTileKind.Mountain;

    // ---------------------------------------------------------------- the climbed-tile rules (the patches call these)

    public const string NoCombatMessage = "You cannot fight while clinging to the mountainside.";

    /// <summary>PlayerMobile.CanBeHarmful: a climber's attacks and harmful spells are refused.</summary>
    public static bool RefusesHarm(Mobile from, bool message)
    {
        if (!IsOnClimbedTile(from))
        {
            return false;
        }

        if (message)
        {
            from.SendMessage(0x22, NoCombatMessage);
        }

        return true;
    }

    /// <summary>PlayerMobile.AllowSkillUse: no taming from a climbed tile.</summary>
    public static bool RefusesSkill(Mobile from, SkillName skill)
    {
        if (skill != SkillName.AnimalTaming || !IsOnClimbedTile(from))
        {
            return false;
        }

        from.SendMessage(0x22, "You cannot tame anything while clinging to the mountainside.");
        return true;
    }

    /// <summary>PlayerMobile.CheckSpellCast: no Mark from a climbed tile (a rune to a perch no monster can reach).</summary>
    public static bool RefusesSpell(Mobile from, ISpell spell)
    {
        if (spell is not MarkSpell || !IsOnClimbedTile(from))
        {
            return false;
        }

        from.SendMessage(0x22, "The mountainside is no place to mark a rune.");
        return true;
    }

    // ---------------------------------------------------------------- the hop

    public enum HopResult
    {
        Hopped,
        NotWorn,
        Dead,
        Cooldown,
        TooFar,
        NoFooting,
        TooHigh,
        Forbidden,
        TooTired,
        NoLedge
    }

    // When each climber may hop again (Core.TickCount).
    private static readonly Dictionary<Mobile, long> _nextHop = new();

    /// <summary>Stamina for a hop that climbs <paramref name="up"/> z (0 for level or down), halved from Delver's up.</summary>
    public static int StaminaCost(int tier, int up)
    {
        var cost = StaminaBase + Math.Max(0, up) / StaminaZPerPoint;
        return tier >= 2 ? (cost + 1) / 2 : cost;
    }

    public static string Message(HopResult r, int tier) => r switch
    {
        HopResult.NotWorn   => "You must be wearing the boots to climb with them.",
        HopResult.Dead      => "The dead do not climb.",
        HopResult.Cooldown  => "You need a moment to catch your breath.",
        HopResult.TooFar    => $"That is beyond your boots' reach ({Tier(tier).HopRange} tiles).",
        HopResult.NoFooting => "You can find no footing there.",
        HopResult.TooHigh   => "That ledge is too high to reach in one hop.",
        HopResult.Forbidden => "Your boots will not take you there.",
        HopResult.TooTired  => "You are too tired to climb.",
        HopResult.NoLedge   => "Your boots remember no ledge within reach.",
        _                   => ""
    };

    /// <summary>Whether a hop may land in this region: never into a town, a dungeon, a house, or a no-teleport place.</summary>
    public static bool MayLandAt(Map map, Point3D p)
    {
        var region = Region.Find(p, map);
        if (region.IsPartOf<TownRegion>() || region.IsPartOf<DungeonRegion>() || region.IsPartOf<HouseRegion>())
        {
            return false;
        }

        if (BaseHouse.FindHouseAt(p, map, 16) != null)
        {
            return false;
        }

        return SpellHelper.CheckTravel(map, p, TravelCheckType.TeleportTo, out _);
    }

    /// <summary>A hop to the tile at <paramref name="x"/>, <paramref name="y"/>. Everything is checked; nothing changes unless it lands.</summary>
    public static HopResult TryHop(PlayerMobile pm, ClimbingBoots boots, int x, int y)
    {
        var refused = CheckCommon(pm, boots);
        if (refused != HopResult.Hopped)
        {
            return refused;
        }

        var range = Tier(boots.Tier).HopRange;
        var distance = Math.Max(Math.Abs(x - pm.X), Math.Abs(y - pm.Y));
        if (distance == 0 || distance > range)
        {
            return HopResult.TooFar;
        }

        return Land(pm, boots, pm.Map, x, y);
    }

    /// <summary>Deepwarden's ledge memory: from the ground within LedgeRange of the last climbed tile, one hop back to it.</summary>
    public static HopResult TryLedgeReturn(PlayerMobile pm, ClimbingBoots boots)
    {
        var refused = CheckCommon(pm, boots);
        if (refused != HopResult.Hopped)
        {
            return refused;
        }

        if (boots.Tier < 3 || boots.LedgeMap == null || boots.LedgeMap != pm.Map || IsOnClimbedTile(pm) ||
            Math.Max(Math.Abs(boots.LedgeLocation.X - pm.X), Math.Abs(boots.LedgeLocation.Y - pm.Y)) > LedgeRange)
        {
            return HopResult.NoLedge;
        }

        return Land(pm, boots, pm.Map, boots.LedgeLocation.X, boots.LedgeLocation.Y);
    }

    private static HopResult CheckCommon(PlayerMobile pm, ClimbingBoots boots)
    {
        if (boots == null || boots.Deleted || boots.Parent != pm)
        {
            return HopResult.NotWorn;
        }

        if (!pm.Alive)
        {
            return HopResult.Dead;
        }

        if (_nextHop.TryGetValue(pm, out var next) && Core.TickCount < next)
        {
            return HopResult.Cooldown;
        }

        if (pm.Map == null || pm.Map == Map.Internal ||
            !SpellHelper.CheckTravel(pm, TravelCheckType.TeleportFrom, out _))
        {
            return HopResult.Forbidden;
        }

        return HopResult.Hopped;
    }

    private static HopResult Land(PlayerMobile pm, ClimbingBoots boots, Map map, int x, int y)
    {
        var tile = Terrain.Probe(map, x, y, pm.Z);
        if (tile.Kind == ClimbTileKind.Blocked)
        {
            return HopResult.NoFooting;
        }

        var up = tile.Z - pm.Z;
        if (up > MaxClimbZ)
        {
            return HopResult.TooHigh;
        }

        var dest = new Point3D(x, y, tile.Z);
        if (!MayLandAt(map, dest))
        {
            return HopResult.Forbidden;
        }

        var cost = StaminaCost(boots.Tier, up);
        if (pm.Stam < cost)
        {
            return HopResult.TooTired;
        }

        var wasClimbing = IsOnClimbedTile(pm);
        var from = pm.Location;

        if (tile.Kind == ClimbTileKind.Mountain && !wasClimbing)
        {
            OrderPets(pm, map, from, OrderType.Stay);
        }

        pm.Stam -= cost;
        Effects.SendLocationParticles(EffectItem.Create(from, map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        pm.MoveToWorld(dest, map);
        pm.PlaySound(0x2E6);
        _nextHop[pm] = Core.TickCount + CooldownMs;

        if (tile.Kind == ClimbTileKind.Mountain)
        {
            if (boots.Tier >= 3)
            {
                boots.LedgeMap = map;
                boots.LedgeLocation = dest;
            }
        }
        else if (wasClimbing)
        {
            OrderPets(pm, map, dest, OrderType.Follow);
        }

        return HopResult.Hopped;
    }

    /// <summary>Orders the climber's followers near <paramref name="around"/> to stay or follow. Nothing is released or moved.</summary>
    public static int OrderPets(PlayerMobile pm, Map map, Point3D around, OrderType order)
    {
        var pets = new List<BaseCreature>();
        foreach (var bc in map.GetMobilesInRange<BaseCreature>(around, PetRange))
        {
            if (bc.Controlled && bc.ControlMaster == pm && !bc.Deleted)
            {
                pets.Add(bc);
            }
        }

        foreach (var bc in pets)
        {
            bc.ControlTarget = order == OrderType.Follow ? pm : null;
            bc.ControlOrder = order;
        }

        if (pets.Count > 0)
        {
            pm.SendMessage(order == OrderType.Stay
                ? "Your pets will wait for you at the foot of the climb."
                : "Your pets fall in behind you again.");
        }

        return pets.Count;
    }

    /// <summary>For the facts: forgets every climber's cooldown.</summary>
    public static void ResetCooldowns() => _nextHop.Clear();

    // ---------------------------------------------------------------- the sale (MinersCompactLiaisonGump, Climbing Boots)

    /// <summary>The boots the player wears, or else carries in the backpack (null for none).</summary>
    public static ClimbingBoots BootsOn(PlayerMobile pm) =>
        pm.FindItemOnLayer(Layer.Shoes) as ClimbingBoots ?? pm.Backpack?.FindItemByType<ClimbingBoots>();

    /// <summary>The highest tier the account has bought (0 for none), from the restoration registry.</summary>
    public static int RegisteredTier(Server.Accounting.IAccount acct)
    {
        for (var t = Tiers.Length; t >= 1; t--)
        {
            if (acct != null && ClusterFRestorationRegistry.HasUnlocked(acct, ClimbingBoots.RegistryKey(t)))
            {
                return t;
            }
        }

        return 0;
    }

    public static (int Vouchers, GuildCost[] Materials, int Gold) Cost(int tier)
    {
        var t = Tier(tier);
        return (t.Vouchers, [GuildCost.Of(t.IngotType, t.Ingots)], t.Gold);
    }

    /// <summary>The next tier's gates and price, met or not each: rank, Mining, blocks mined, vouchers, ingots, gold.</summary>
    public static RequirementList Requirements(PlayerMobile pm, CharacterGuildData guild, int tier)
    {
        var t = Tier(tier);
        guild.GuildReputation.TryGetValue("mining", out var standing);
        var blocks = ClusterFMinedBlocks.BlocksMined(pm.Account as Server.Accounting.IAccount);
        var cost = Cost(tier);

        return new RequirementList()
            .Rank(MinersCompactLiaisonGump.GetRankName(t.RankStanding), standing, t.RankStanding)
            .Skill("Mining", pm.Skills.Mining.Base, t.Mining)
            .Amount("Blocks mined", blocks, t.BlocksMined)
            .Price(pm, guild, "mining", "Mining Vouchers", cost.Vouchers, cost.Gold, cost.Materials);
    }

    /// <summary>
    /// Buys the next tier (tier 1 with no boots; tier n+1 for the tier-n boots worn or carried, which it consumes). Every
    /// gate is checked again and the price taken before anything changes. Returns a line for the player either way.
    /// </summary>
    public static bool BuyNext(PlayerMobile pm, CharacterGuildData guild, out string message)
    {
        var acct = pm.Account as Server.Accounting.IAccount;
        if (acct == null || guild == null || !guild.JoinedGuilds.Contains("mining"))
        {
            message = "Climbing boots are for Miners' Compact members.";
            return false;
        }

        var current = BootsOn(pm);
        var curTier = current?.Tier ?? 0;
        if (RegisteredTier(acct) > curTier)
        {
            message = "Your account owns better boots than these: recover them first.";
            return false;
        }

        if (curTier >= Tiers.Length)
        {
            message = "Your boots are already Deepwarden's.";
            return false;
        }

        var next = curTier + 1;
        var reqs = Requirements(pm, guild, next);
        var bypass = DevTestingCrystal.IsActive(pm);

        if (!bypass)
        {
            if (!reqs.AllMet)
            {
                message = $"{Tier(next).Name} are not yet yours to buy: the Liaison's page shows what is still needed.";
                return false;
            }

            var cost = Cost(next);
            if (!GuildResources.TryPay(pm, guild, "mining", cost.Vouchers, "Mining Vouchers", cost.Gold, cost.Materials))
            {
                message = "You cannot pay for them.";
                return false;
            }
        }

        current?.Delete();
        var boots = ClimbingBoots.Create(next, acct.Username);
        pm.AddToBackpack(boots);
        ClusterFRestorationRegistry.Unlock(acct, ClimbingBoots.RegistryKey(next), "purchase");

        message = curTier == 0 ? $"You buy {Tier(next).Name}." : $"Your boots are now {Tier(next).Name}.";
        return true;
    }

    /// <summary>Replaces the account's registered boots when none are worn or carried, for RestoreVouchers vouchers.</summary>
    public static bool Recover(PlayerMobile pm, CharacterGuildData guild, out string message)
    {
        var acct = pm.Account as Server.Accounting.IAccount;
        var registered = RegisteredTier(acct);
        var current = BootsOn(pm);

        if (acct == null || guild == null || registered == 0 || current?.Tier >= registered)
        {
            message = "Your boots are accounted for: nothing to recover.";
            return false;
        }

        if (!DevTestingCrystal.IsActive(pm) &&
            !GuildResources.TryPay(pm, guild, "mining", RestoreVouchers, "Mining Vouchers", 0))
        {
            message = "You cannot pay for the replacement.";
            return false;
        }

        current?.Delete();
        pm.AddToBackpack(ClimbingBoots.Create(registered, acct.Username));
        message = $"The Compact replaces your {Tier(registered).Name}.";
        return true;
    }
}
