// =============================================================================
// ClusterFLeagueBestiary.cs
// cc-P364 (F-24): the League hunt bestiary, batch 2. Fourteen creatures on the player package's own bodies
// (2072-2085, cc-P139's route), each with a new name of ours, a monster part, a home and a use for the League's work:
// a hunt target (a named or elite creature whose part a hunt asks for), a culling pack (kill N of a species in a
// region), or a mini-boss. F-24's job board is not built yet (feature-queue F-24; RequisitionSystem.HuntFamily is its
// seam), so this file is the data it will read: the hunt table (HuntTargets), the culling table (CullingPacks), the
// bestiary lines, and the spawn data that gives each creature a home.
//
// Bestiary lines. Each entry has the System's snarky line and the plain line in parentheses, as achievements do
// (AchievementDef.FlavorText and EarnedLine). A player sees them once, on the first kill of each creature:
// OnCreatureDeath records the type in the account's EncounteredCreatures (the bestiary set ClusterFAccountData keeps
// for hunting work orders, "creature type names on first kill") and sends both lines. Only this table's creatures are
// recorded here; the set's other writers are F-24's.
//
// Spawn data, not hand placement. Spawners (stock Spawner) are rows in Spawners; [ClusterFSeedBestiary places the
// missing ones on Trammel (Rerun = Skips: a row whose spawner stands is counted present, nothing is moved or deleted).
// Homes are existing places: Haven Island's hills and woods for the low-level targets (clear of New Haven, Old Haven,
// the tutorial farm and the Rootdeep hole), the great swamp south of Britain and the marsh west of Cove for the
// croakers, and stock dungeon halls whose own spawn files prove the tile (shared/trammel/*.json) for the rest.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server;

public enum BestiaryUse
{
    /// <summary>A hunt's target: a named or elite creature whose part the hunt asks for.</summary>
    HuntTarget,

    /// <summary>A cull's species: kill N of them in their home.</summary>
    CullingPack,

    /// <summary>A mini-boss: a hunt for a group.</summary>
    MiniBoss
}

/// <summary>One bestiary creature: its type, the use the League puts it to, its home, its part and its two lines.</summary>
public sealed record BestiaryEntry(
    Type Type, string Name, BestiaryUse Use, string Home, Type Part, int LevelFrom, int LevelTo, string Flavor, string Plain
)
{
    /// <summary>The plain line as achievements show it: in parentheses.</summary>
    public string PlainLine => $"({Plain})";
}

public static class ClusterFLeagueBestiary
{
    public const int FlavorHue = 0x59;  // the System's voice, as the achievement popup's title
    public const int PlainHue = 0x3B2;

    public const string HavenHills = "Haven Island, the hills and woods around New Haven";
    public const string GreatSwamp = "the great swamp south of Britain, and the marsh west of Cove";
    public const string CovetousLake = "Covetous, the flooded halls of the second level";
    public const string YewWoods = "the Yew woods";
    public const string DeceitHalls = "Deceit, the first and second levels";
    public const string FireHalls = "the Fire dungeon";
    public const string IceHalls = "the Ice dungeon";
    public const string ShameHalls = "Shame, the first two levels";
    public const string CovetousDepths = "Covetous, the lower hall past the lake";

    /// <summary>
    /// Every bestiary creature. Level bands are the shard's creature levels (ClusterFCreatureLevels, 1 to 200, the
    /// Bard difficulty): low for New Haven's surroundings, middle for the culls, the mini-bosses past 70. A fact
    /// computes each creature's level and holds it inside its band.
    /// </summary>
    public static readonly BestiaryEntry[] Entries =
    [
        // -- low-level hunt targets and packs for New Haven's surroundings --------------------------------------------
        new(typeof(HedgeSnatcher), "a hedge snatcher", BestiaryUse.CullingPack, HavenHills, typeof(SnatcherEars), 8, 30,
            "It steals from hedgerows and runs. The System respects the commitment.",
            "A small goblin that raids the hills around New Haven in packs; drops snatcher ears."),
        new(typeof(SnatcherOutrider), "a snatcher outrider", BestiaryUse.HuntTarget, HavenHills, typeof(SnatcherEars), 20, 45,
            "A goblin that found a wolf willing to carry it. Nobody asked the wolf.",
            "The hedge snatchers' mounted leader on Haven Island's southern hills; faster and tougher than its pack."),
        new(typeof(HornbackRaider), "a hornback raider", BestiaryUse.HuntTarget, HavenHills, typeof(HornbackHorn), 12, 40,
            "Half goat, half bandit, entirely convinced the hills are its.",
            "A goat-headed raider of the hills north of New Haven; hits harder than it looks and drops a horn."),
        new(typeof(DitchCrawler), "a ditch crawler", BestiaryUse.CullingPack, HavenHills, typeof(CrawlerMandible), 5, 25,
            "A worm with ambitions and far too many legs.",
            "A crawler of Haven Island's ditches and gullies; its bite is mildly venomous."),
        new(typeof(MireCroaker), "a mire croaker", BestiaryUse.HuntTarget, GreatSwamp, typeof(CroakerTongue), 12, 40,
            "It croaks before it stabs. Consider that fair warning.",
            "A frog-folk spear fisher of the swamps; it swims as well as it walks."),

        // -- mid-level culling packs and hunt targets ------------------------------------------------------------------
        new(typeof(BrineLurker), "a brine lurker", BestiaryUse.CullingPack, CovetousLake, typeof(LurkerGills), 28, 60,
            "Something fishy with a spear. The System has heard all the jokes.",
            "A fish-folk hunter of Covetous's flooded halls; its spear strikes cold and it swims."),
        new(typeof(Moonmaw), "a moonmaw", BestiaryUse.CullingPack, YewWoods, typeof(MoonmawPelt), 30, 65,
            "Wolf by night, wolf by day, inconvenient at all hours.",
            "A werewolf of the Yew woods that hunts in packs; its bite bleeds."),
        new(typeof(Thornwisp), "a thornwisp", BestiaryUse.CullingPack, YewWoods, typeof(ThornwispSpark), 35, 70,
            "The forest's idea of a lantern. Do not follow it.",
            "A forest spirit of the Yew woods; it casts, and its touch poisons."),
        new(typeof(PaleDrifter), "a pale drifter", BestiaryUse.CullingPack, DeceitHalls, typeof(DrifterEctoplasm), 35, 70,
            "A ghost that never learned to haunt properly, so it just drifts at you.",
            "A pale shade of Deceit's halls; its touch is cold and no poison troubles it."),
        new(typeof(Slagheart), "a slagheart", BestiaryUse.CullingPack, FireHalls, typeof(SlagCore), 40, 75,
            "A rock that is also a furnace. Neither was improved by the meeting.",
            "A lava golem of the Fire dungeon; it burns to the touch and shrugs off poison and bleeding."),
        new(typeof(Glasswarden), "a glasswarden", BestiaryUse.CullingPack, IceHalls, typeof(GlasswardenShard), 40, 75,
            "Beautiful, sharp and entirely humourless.",
            "A crystal golem of the Ice dungeon; its shards strike with energy, and the cold does not slow it."),
        new(typeof(Tunnelmaw), "a tunnelmaw", BestiaryUse.HuntTarget, ShameHalls, typeof(TunnelmawClaw), 45, 80,
            "It chews through rock. Your armour is softer than rock.",
            "A burrowing hulk of Shame's halls; its claws bleed and nothing out-digs it."),

        // -- the two mini-bosses -----------------------------------------------------------------------------------------
        new(typeof(TheSiegebreaker), "The Siegebreaker", BestiaryUse.MiniBoss, CovetousDepths, typeof(SiegebreakerTusk), 70, 110,
            "A troll that took up siege work. The walls lost.",
            "A war troll holding Covetous's lower hall; a mini-boss whose club crushes armour. Bring friends."),
        new(typeof(TheHollowOak), "The Hollow Oak", BestiaryUse.MiniBoss, YewWoods, typeof(HollowOakHeartwood), 70, 110,
            "An old tree with grievances. Many grievances.",
            "An ancient treant of the Yew woods; a mini-boss, slow, grasping and poisonous, and it fears fire.")
    ];

    /// <summary>The hunt table: every creature a hunt can name (the targets and the mini-bosses).</summary>
    public static IEnumerable<BestiaryEntry> HuntTargets => Entries.Where(e => e.Use != BestiaryUse.CullingPack);

    /// <summary>The culling table: every species a cull can count.</summary>
    public static IEnumerable<BestiaryEntry> CullingPacks => Entries.Where(e => e.Use == BestiaryUse.CullingPack);

    public static IEnumerable<BestiaryEntry> MiniBosses => Entries.Where(e => e.Use == BestiaryUse.MiniBoss);

    private static readonly Dictionary<Type, BestiaryEntry> ByType = Entries.ToDictionary(e => e.Type);

    public static BestiaryEntry Find(Type type) => type != null && ByType.TryGetValue(type, out var e) ? e : null;

    public static BestiaryEntry Find(BaseCreature bc) => bc == null ? null : Find(bc.GetType());

    // ---- the bestiary line, once a creature ---------------------------------------------------------------------------

    /// <summary>
    /// The first time a character's account kills one of the table's creatures: the type goes into the account's
    /// bestiary (EncounteredCreatures) and the killer reads the System's line and the plain line. Returns whether this
    /// kill was the first.
    /// </summary>
    public static bool RecordKill(PlayerMobile pm, BaseCreature bc)
    {
        var entry = Find(bc);
        if (entry == null || pm?.Account is not IAccount acct)
        {
            return false;
        }

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (!data.EncounteredCreatures.Add(entry.Type.Name))
        {
            return false;
        }

        if (pm.NetState != null)
        {
            pm.SendMessage(FlavorHue, $"The System notes a new entry in your bestiary, {entry.Name}: {entry.Flavor}");
            pm.SendMessage(PlainHue, entry.PlainLine);
        }

        return true;
    }

    // cc-P455 (cc-P404 #1): the killer as pinned credits fame (BaseCreature.cs:3570): a pet's or a summon's master,
    // or the bard who provoked the killer onto this creature. Only the killer is read here, and it is not deleted.
    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (KillerOf(bc) is PlayerMobile pm)
        {
            RecordKill(pm, bc);
        }
    }

    public static Mobile KillerOf(BaseCreature bc) =>
        bc.LastKiller is BaseCreature killer ? killer.GetDamageMaster(bc) : bc.LastKiller;

    // ---- spawn data ---------------------------------------------------------------------------------------------------

    public readonly record struct SpawnRow(
        string Label, string Home, int X, int Y, int Count, int HomeRange, int MinMinutes, int MaxMinutes, params string[] Types
    )
    {
        public Point2D Location => new(X, Y);
        public string SpawnerName => "Bestiary: " + Label;
    }

    /// <summary>
    /// Every spawner, all on Trammel. Haven Island tiles were checked walkable on the 7.0.117.0 map (notes, Part B);
    /// each dungeon tile is a stock spawner's own tile from pinned's shared/trammel spawn files; the two swamp tiles
    /// are swamp land (0x3DC2-0x3DF0) on the same map.
    /// </summary>
    public static readonly SpawnRow[] Spawners =
    [
        // -- Haven Island (New Haven's surroundings): the south-western hills and woods, and the hills to the north.
        new("south-west hills: hedge snatchers", HavenHills, 3430, 2700, 3, 20, 5, 10, nameof(HedgeSnatcher)),
        new("north hills: hedge snatchers", HavenHills, 3545, 2395, 2, 15, 5, 10, nameof(HedgeSnatcher)),
        new("southern hills: the snatcher outrider", HavenHills, 3410, 2745, 1, 15, 10, 15, nameof(SnatcherOutrider)),
        new("north hills: hornback raiders", HavenHills, 3570, 2380, 2, 15, 8, 12, nameof(HornbackRaider)),
        new("north-east hills: hornback raiders", HavenHills, 3640, 2400, 2, 12, 8, 12, nameof(HornbackRaider)),
        new("western gullies: ditch crawlers", HavenHills, 3395, 2690, 3, 12, 4, 8, nameof(DitchCrawler)),
        new("southern gullies: ditch crawlers", HavenHills, 3460, 2725, 2, 12, 4, 8, nameof(DitchCrawler)),

        // -- the swamps.
        new("great swamp: mire croakers", GreatSwamp, 1190, 2885, 3, 15, 8, 12, nameof(MireCroaker)),
        new("Cove marsh: mire croakers", GreatSwamp, 2065, 1000, 2, 12, 8, 12, nameof(MireCroaker)),

        // -- Covetous: the lake (stock WaterElemental spawner 5445,2030; spiders 5440,2023), the lower hall (HeadlessOne 5577,2006).
        new("Covetous lake: brine lurkers", CovetousLake, 5445, 2030, 3, 12, 5, 10, nameof(BrineLurker)),
        new("Covetous lake shore: brine lurkers", CovetousLake, 5440, 2023, 2, 10, 5, 10, nameof(BrineLurker)),
        new("Covetous lower hall: the Siegebreaker", CovetousDepths, 5577, 2006, 1, 8, 30, 45, nameof(TheSiegebreaker)),

        // -- the Yew woods (stock Outdoors spawners 1059,1166; 960,910; 837,361) and a clearing checked on the map (1010,1250).
        new("Yew woods south: moonmaws", YewWoods, 1059, 1166, 3, 20, 5, 10, nameof(Moonmaw)),
        new("Yew woods east: moonmaws", YewWoods, 960, 910, 2, 20, 5, 10, nameof(Moonmaw)),
        new("Yew woods east: thornwisps", YewWoods, 960, 910, 2, 20, 6, 10, nameof(Thornwisp)),
        new("Yew woods north: thornwisps", YewWoods, 837, 361, 2, 20, 6, 10, nameof(Thornwisp)),
        new("Yew woods clearing: the Hollow Oak", YewWoods, 1010, 1250, 1, 10, 30, 45, nameof(TheHollowOak)),

        // -- Deceit (stock Shade/Wraith spawners 5170,678 and 5139,661).
        new("Deceit: pale drifters", DeceitHalls, 5170, 678, 3, 10, 5, 10, nameof(PaleDrifter)),
        new("Deceit west: pale drifters", DeceitHalls, 5139, 661, 2, 10, 5, 10, nameof(PaleDrifter)),

        // -- Fire (stock FireElemental spawners 5699,1302 and 5859,1341).
        new("Fire: slaghearts", FireHalls, 5699, 1302, 2, 10, 6, 10, nameof(Slagheart)),
        new("Fire east: slaghearts", FireHalls, 5859, 1341, 2, 8, 6, 10, nameof(Slagheart)),

        // -- Ice (stock IceElemental spawners 5731,176 and 5681,191).
        new("Ice: glasswardens", IceHalls, 5731, 176, 2, 12, 6, 10, nameof(Glasswarden)),
        new("Ice west: glasswardens", IceHalls, 5681, 191, 2, 10, 6, 10, nameof(Glasswarden)),

        // -- Shame (stock EarthElemental spawners 5474,20 and 5649,101).
        new("Shame: a tunnelmaw", ShameHalls, 5474, 20, 1, 12, 10, 15, nameof(Tunnelmaw)),
        new("Shame second level: a tunnelmaw", ShameHalls, 5649, 101, 1, 10, 10, 15, nameof(Tunnelmaw))
    ];

    public static IEnumerable<string> AllTypeNames() => Spawners.SelectMany(r => r.Types).Distinct();

    public static void Configure()
    {
        CommandSystem.Register("ClusterFSeedBestiary", AccessLevel.Administrator, OnSeedCommand);
    }

    [Usage("ClusterFSeedBestiary [dryrun]")]
    [Description("Places the League bestiary's spawners (batch 2: the hedge snatchers, hornback raiders, ditch crawlers, mire croakers, brine lurkers, moonmaws, thornwisps, pale drifters, slaghearts, glasswardens, tunnelmaws, the Siegebreaker and the Hollow Oak) on Trammel where missing. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the League bestiary's spawners (batch 2) on Trammel where missing.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 && e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);
        var result = Seed(dryRun);
        e.Mobile.SendMessage(result.Message);
    }

    public sealed record SeedResult(int Placed, int Present, string Message);

    public static SeedResult Seed(bool dryRun)
    {
        var map = Map.Trammel;
        int placed = 0, present = 0;

        foreach (var row in Spawners)
        {
            if (FindSpawner(map, row) != null)
            {
                present++;
                continue;
            }

            if (!dryRun)
            {
                Place(map, row);
            }

            placed++;
        }

        var verb = dryRun ? "would place" : "placed";
        return new SeedResult(placed, present,
            $"League bestiary{(dryRun ? " (dry run)" : "")}: {verb} {placed} spawners; already there {present}.");
    }

    /// <summary>The row's spawner: one of ours by name within 3 tiles of the row's tile, or null.</summary>
    public static Spawner FindSpawner(Map map, SpawnRow row)
    {
        foreach (var item in map.GetItemsInRange<Spawner>(new Point3D(row.X, row.Y, 0), 3))
        {
            if (!item.Deleted && item.Name == row.SpawnerName)
            {
                return item;
            }
        }

        return null;
    }

    public static Spawner Place(Map map, SpawnRow row)
    {
        var z = map.GetAverageZ(row.X, row.Y);
        var spawner = new Spawner(row.Count, TimeSpan.FromMinutes(row.MinMinutes), TimeSpan.FromMinutes(row.MaxMinutes), 0, default, row.Types)
        {
            Name = row.SpawnerName
        };
        spawner.MoveToWorld(new Point3D(row.X, row.Y, z), map);
        spawner.HomeRange = row.HomeRange; // after the move: the bounds are centred on the spawner's location (BaseSpawner.cs:189-219)
        spawner.Respawn();
        return spawner;
    }
}
