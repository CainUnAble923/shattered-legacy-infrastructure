// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/SorcerersDungeonData.cs (cc-P408), on the shard's P33
// points framework (Services/PointsSystems/PointsSystem.cs) beside BlackthornData, whose shape it shares.
//
// Three things in one system, as ServUO has them:
//   1. The reward points: one per artifact of Enchanted Origin handed to the researcher (SorcerersDungeonResearcher;
//      "You have turned in ~1_COUNT~ artifacts of the Kotl", 1156902: ServUO's own message, its Kotl text and all),
//      spent in the reward gump.
//   2. DungeonPoints, the per-player kill tally that rolls the artifacts: every kill inside "Sorcerer's Dungeon" (the
//      stock Ilshenar region, pinned Distribution/Data/regions.json) by a living player adds fame / 4 x
//      (1 + sqrt(luck) / 100), and the chance of an artifact is A x 10 ^ (B x tally), Blackthorn's A and B; a hit puts
//      an artifact of Enchanted Origin in the pack (bank, then feet, when full) and empties the tally. Only while the
//      event is Enabled.
//   3. The event's own state: Enabled, and the spawner (TOSDSpawner) when one exists, saved inside this system as
//      ServUO saves it. Ours, also here: the stock spawners the event paused (TOSDSpawner.PausedSpawners).
//
// Conversion notes: ServUO reaches ProcessKill through EventSink.OnKilledBy, raised for every damager with looting
// rights. Pinned has neither, so OnCreatureDeath below, on pinned's generated CreatureEvents.CreatureDeathEvent
// (Mobiles/CreatureEvents.cs:8, raised at the end of BaseCreature.OnDeath, BaseCreature.cs:3669), walks
// BaseCreature.GetLootingRights as cc-P196's Blackthorn region does. That event comes AFTER the creature is deleted
// (pinned Mobile.OnDeath deletes a non-player, Server/Mobiles/Mobile.cs:4901, and Delete clears its Region and Map,
// :2484 and :2506), so ServUO's "bc.Region" is gone by then (found by the first probe of the facts). CreatureDeletedEvent
// is raised inside that same Delete while the Map is still set (BaseCreature.OnDelete :3674, from Mobile.Delete :2489), so
// the facet is remembered there and the region is found again at the death spot (Region.Find(location, map)); the
// location survives the delete. ServUO also calls the spawner's OnCreatureDeath
// from each ProcessKill; here it is called once per death, before the walk (it only removes the creature from the
// spawn list once in ServUO too). RealLuck is pinned's Luck. The P33 framework has no loyalty gump, so ServUO's Name
// and ShowOnLoyaltyGump (false) have nothing to read them. InSeason is ServUO's SeasonalEventSystem (October 1 for
// 60 days); here it is the Halloween season switch (SorcerersDungeonGenerate.Season, deviation 1 in the notes).
//
// cc-P624 (stage 1 of shard-migration notes/cc-P564-design-pumpkin-king.md): that same Delete also clears the
// creature's control master (BaseCreature.OnDelete :3688, after the event), so by the death event a tamed pet killed
// in the dungeon read as wild and paid out. The deletion snapshot now keeps the facet, the death spot and the
// controlled and summoned flags, keyed by creature, taken only for a creature at 0 hits (a dying one: Mobile.Kill sets
// Hits = 0 before OnDeath; a staff [delete of a living creature takes none) and dropped when its death is read, so a
// death inside another's deletion or handlers cannot take or lose the other's snapshot. A creature deleted at 0 hits
// with no death event after it (one built without hit points, say) leaves one behind, so the table keeps at most
// MaxPendingDeaths and drops the oldest first: the deaths in progress are always the newest entries. The
// drop roll goes through DropRoll, which a test can pin for one scope (ForceDropRoll); the server uses
// Utility.RandomDouble.

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.Points;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

public class SorcerersDungeonData : PointsSystem
{
    public const int CurrentVersion = 0;
    public const string RegionName = "Sorcerer's Dungeon";

    private const double A = 0.000863316841;
    private const double B = 0.00000425531915;

    private static SorcerersDungeonData _instance;

    public static SorcerersDungeonData Instance => _instance ??= new SorcerersDungeonData();

    public override PointsType Loyalty => PointsType.SorcerersDungeon;
    public override bool AutoAdd => true;
    public override double MaxPoints => double.MaxValue;

    /// <summary>ServUO's Enabled: the event is up (the spawner runs and kills roll artifacts).</summary>
    public bool Enabled { get; set; }

    /// <summary>ServUO's InSeason: the Halloween season switch's window, on the shard's local date.</summary>
    public bool InSeason => SorcerersDungeonGenerate.Season.IsInSeason(SorcerersDungeonGenerate.Season.Clock());

    public Dictionary<Mobile, int> DungeonPoints { get; } = new();

    public static void Configure()
    {
        _ = Instance;
    }

    public override void SendMessage(PlayerMobile from, double old, double points, bool quest)
    {
        from.SendLocalizedMessage(1156902, ((int)points).ToString()); // You have turned in ~1_COUNT~ artifacts of the Kotl
    }

    /// <summary>The artifact chance at a tally (ServUO ProcessKill's formula).</summary>
    public static double ChanceAt(int dungeonPoints) => A * Math.Pow(10, B * dungeonPoints);

    /// <summary>A creature as it was when it died: read before its deletion cleared any of it (see the header).</summary>
    public readonly record struct DeathContext(Map Map, Point3D Location, bool Controlled, bool Summoned)
    {
        public static DeathContext Of(BaseCreature bc) => new(bc.Map, bc.Location, bc.Controlled, bc.Summoned);
    }

    // The dying creatures being deleted, oldest first, kept from CreatureDeletedEvent for the death event that follows
    // each in the same call (see the header): normally only the deaths nested inside one another at that moment.
    private static readonly List<(BaseCreature Creature, DeathContext Context)> _deleting = new();
    private const int MaxPendingDeaths = 64;

    /// <summary>The drop roll, 0 to 1 (an artifact when the chance is above it). Test support: <see cref="ForceDropRoll" />.</summary>
    public static Func<double> DropRoll { get; private set; } = Utility.RandomDouble;

    /// <summary>Test support: every drop roll reads <paramref name="roll" /> until the returned scope is disposed.</summary>
    public static IDisposable ForceDropRoll(double roll)
    {
        var previous = DropRoll;
        DropRoll = () => roll;
        return new RollScope(previous);
    }

    private sealed class RollScope : IDisposable
    {
        private Func<double> _previous;

        public RollScope(Func<double> previous) => _previous = previous;

        public void Dispose()
        {
            if (_previous != null)
            {
                DropRoll = _previous;
                _previous = null;
            }
        }
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeletedEvent(BaseCreature bc)
    {
        if (bc.Hits > 0)
        {
            return;
        }

        _deleting.RemoveAll(e => e.Creature == bc);

        if (_deleting.Count >= MaxPendingDeaths)
        {
            _deleting.RemoveAt(0);
        }

        _deleting.Add((bc, DeathContext.Of(bc)));
    }

    private static bool TakeSnapshot(BaseCreature bc, out DeathContext context)
    {
        for (var i = _deleting.Count - 1; i >= 0; i--)
        {
            if (_deleting[i].Creature == bc)
            {
                context = _deleting[i].Context;
                _deleting.RemoveAt(i);
                return true;
            }
        }

        context = default;
        return false;
    }

    /// <summary>Death snapshots taken and not yet read (test support).</summary>
    public static int PendingDeaths => _deleting.Count;

    /// <summary>Test support: forgets snapshots other facts' 0-hit creatures left behind.</summary>
    public static void ForgetPendingDeaths() => _deleting.Clear();

    // ServUO's EventSink.OnKilledBy, which BaseCreature.OnDeath raises for every damager with looting rights.
    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeathEvent(BaseCreature bc)
    {
        DeathContext context;

        if (!bc.Deleted)
        {
            context = DeathContext.Of(bc); // a bonded pet: not deleted, its state is still its own
        }
        else if (!TakeSnapshot(bc, out context))
        {
            context = default; // no snapshot: no facet, so no credit
        }

        Instance.OnCreatureDeath(bc, context);
    }

    public void OnCreatureDeath(BaseCreature bc, DeathContext context)
    {
        if (bc == null)
        {
            return;
        }

        TOSDSpawner.Instance?.OnCreatureDeath(bc);

        if (!Enabled || context.Map == null || context.Map == Map.Internal || context.Controlled || context.Summoned)
        {
            return;
        }

        var region = Region.Find(context.Location, context.Map);
        var rights = BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax);

        for (var i = 0; i < rights.Count; i++)
        {
            var ds = rights[i];

            if (ds.m_HasRight)
            {
                ProcessKill(bc, ds.m_Mobile, false, region, context);
            }
        }
    }

    /// <summary>
    ///     ServUO ProcessKill, after its spawner call: a kill in the dungeon by a living player, while the event is
    ///     enabled, adds to the tally and rolls an artifact. Returns the artifact when one was given.
    ///     <paramref name="forceDrop" /> is test support: it makes the roll succeed. <paramref name="region" /> is where
    ///     the creature died, for a creature already deleted; null reads the creature's own region (ServUO's bc.Region).
    ///     <paramref name="context" />, from the death hook, is the creature as it died; null reads it now.
    /// </summary>
    public Item ProcessKill(
        Mobile victim, Mobile damager, bool forceDrop = false, Region region = null, DeathContext? context = null
    )
    {
        if (victim is not BaseCreature bc || !Enabled || damager?.Alive != true)
        {
            return null;
        }

        var died = context ?? DeathContext.Of(bc);

        if (died.Controlled || died.Summoned)
        {
            return null;
        }

        region ??= bc.Region;

        if (damager is not PlayerMobile pm || region?.IsPartOf(RegionName) != true)
        {
            return null;
        }

        DungeonPoints.TryGetValue(pm, out var tally);

        var fame = bc.Fame / 4;
        var luck = Math.Max(0, pm.Luck);

        tally += (int)(fame * (1 + Math.Sqrt(luck) / 100));
        DungeonPoints[pm] = tally;

        if (!forceDrop && ChanceAt(tally) <= DropRoll())
        {
            return null;
        }

        var item = EnchantedOriginArtifacts.CreateForKill(bc, pm);

        if (item == null)
        {
            return null;
        }

        pm.PlaySound(0x5B4);
        pm.SendLocalizedMessage(1157613); // You notice some of your fallen foes' equipment to be of enchanted origin and decide it may be of some value...

        if (!pm.PlaceInBackpack(item))
        {
            if (pm.BankBox?.TryDropItem(pm, item, false) == true)
            {
                pm.SendLocalizedMessage(1079730); // The item has been placed into your bank box.
            }
            else
            {
                pm.SendLocalizedMessage(1072523); // You find an artifact, but your backpack and bank are too full to hold it.
                item.MoveToWorld(pm.Location, pm.Map);
            }
        }

        DungeonPoints.Remove(pm);
        return item;
    }

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);

        writer.WriteEncodedInt(CurrentVersion);

        writer.Write(Enabled);

        var spawner = TOSDSpawner.Instance;
        writer.Write(spawner != null);
        spawner?.Serialize(writer);

        var live = new List<KeyValuePair<Mobile, int>>();

        foreach (var kvp in DungeonPoints)
        {
            if (kvp.Key?.Deleted == false && kvp.Value > 0)
            {
                live.Add(kvp);
            }
        }

        writer.WriteEncodedInt(live.Count);

        foreach (var (m, points) in live)
        {
            writer.Write(m);
            writer.Write(points);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);

        DungeonPoints.Clear();

        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"SorcerersDungeonData version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Enabled = reader.ReadBool();

        TOSDSpawner.Instance = null;

        if (reader.ReadBool())
        {
            // ServUO: the spawner rebuilds its entries and makes itself the instance, then reads its state; its timer
            // starts in Initialize (TOSDSpawner.Initialize), after the world has loaded.
            var spawner = new TOSDSpawner(activate: false);
            spawner.Deserialize(reader);
        }

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var points = reader.ReadInt();

            if (m != null && points > 0)
            {
                DungeonPoints[m] = points;
            }
        }
    }
}
