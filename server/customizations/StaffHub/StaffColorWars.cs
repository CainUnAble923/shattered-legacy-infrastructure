// StaffColorWars.cs
//
// cc-P863 (Chase 11-08, from P854's fold-in item 5): Color Wars, the Staff Hub Events tab's first team event. Our own
// ModernUO build of Omni's "Color Wars" (servuo.dev 2014, third-party, reference only; no code taken). Credit: Omni, for
// the event: a gate players step through, two or four colour teams, a lent event kit, walls up while teams gather and
// down for the fight, and winning stones that pay tokens.
//
// What changed from the original, on purpose:
// - Opt-in: the gate asks first (ColorWarsJoinGump). Young, dead, mounted and staff characters cannot join.
// - No item loss: everything a player wears and carries goes into a bag in their own bank while they play, and comes
//   back (worn things worn again) when they leave or the event ends, even while they are logged out. A death in the
//   arena keeps every kit item (the corpse's contents go back to the backpack) and the player rises at the team's base.
// - The kit is lent: armour, a robe and weapons in the team's colour, a full spellbook with reagents, bandages and
//   arrows. It is taken back when the player leaves. Nothing in it is blessed or kept.
// - No PlayerMobile edit: teams live here, the arena is a region (ColorWarsRegion), and team colours on the name bar
//   come from a wrapped Notoriety handler (teammates green, the other teams orange). Teammates cannot harm each other.
// - Walls up and down are phases: while the teams gather no one can harm anyone; "Start the battle" opens the fight.
// - The arena teaches nothing: no skill or stat gain inside it (Region.AllowGain), and no recall, gate or mark in or out.
// - The winners: the team with the most kills when a GM ends it (or the first to CwKillsToWin, if set) wins
//   CwWinTokens Festival Tokens each, through FestivalTokens.Grant. A draw pays nobody.
// - A run survives a restart (StaffColorWarsStore, versioned, D40); the gate and the exit stones of a run that is gone
//   remove themselves after load.
// Notes: shard-migration notes/cc-P863-color-wars.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.RewardStores;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;

namespace Server;

public enum ColorWarsTeam
{
    Red,
    Blue,
    White,
    Black
}

public enum ColorWarsPhase
{
    Gathering,
    Fighting
}

public sealed class ColorWarsParticipant
{
    public PlayerMobile Mobile;
    public ColorWarsTeam Team;
    public Container Stash;                 // the bag in the player's bank that holds their own things
    public readonly List<Item> Worn = [];   // what they wore when they joined, worn again when they leave
    public readonly List<Item> Kit = [];    // the lent kit, taken back when they leave
    public Map ReturnMap;
    public Point3D ReturnLocation;
}

public sealed class ColorWarsRun
{
    public Map Map;
    public int X1, Y1, X2, Y2; // inclusive
    public int Z;
    public int TeamCount = 2;
    public ColorWarsPhase Phase = ColorWarsPhase.Gathering;
    public readonly int[] Scores = new int[4];
    public Item Gate;
    public readonly List<Item> Stones = [];
    public readonly Dictionary<PlayerMobile, ColorWarsParticipant> Players = new();
    public ColorWarsRegion Region;

    public bool Contains(int x, int y) => x >= X1 && x <= X2 && y >= Y1 && y <= Y2;

    public IEnumerable<ColorWarsTeam> Teams => Enumerable.Range(0, TeamCount).Select(i => (ColorWarsTeam)i);

    public int CountOf(ColorWarsTeam team) => Players.Values.Count(p => p.Team == team);

    /// <summary>Where a team starts and rises: the two ends of the long side, or the four corners, a few tiles in.</summary>
    public Point3D Base(ColorWarsTeam team)
    {
        int w = X2 - X1 + 1, h = Y2 - Y1 + 1;
        var inset = Math.Max(1, Math.Min(3, Math.Min(w, h) / 4));
        int midX = (X1 + X2) / 2, midY = (Y1 + Y2) / 2;
        int x, y;

        if (TeamCount == 2)
        {
            var first = team == ColorWarsTeam.Red;
            (x, y) = w >= h ? (first ? X1 + inset : X2 - inset, midY) : (midX, first ? Y1 + inset : Y2 - inset);
        }
        else
        {
            var east = team is ColorWarsTeam.Blue or ColorWarsTeam.Black;
            var south = team is ColorWarsTeam.White or ColorWarsTeam.Black;
            x = east ? X2 - inset : X1 + inset;
            y = south ? Y2 - inset : Y1 + inset;
        }

        var z = Map == null || Map.CanSpawnMobile(x, y, Z) ? Z : Map.GetAverageZ(x, y);
        return new Point3D(x, y, z);
    }

    /// <summary>The team's exit stone: one tile behind its base, away from the middle of the arena.</summary>
    public Point3D StoneSpot(ColorWarsTeam team)
    {
        var b = Base(team);
        int midX = (X1 + X2) / 2, midY = (Y1 + Y2) / 2;
        var w = X2 - X1 + 1;
        var h = Y2 - Y1 + 1;
        int dx = 0, dy = 0;

        if (TeamCount == 2 && w >= h)
        {
            dx = b.X < midX ? -1 : 1;
        }
        else if (TeamCount == 2)
        {
            dy = b.Y < midY ? -1 : 1;
        }
        else
        {
            dy = b.Y < midY ? -1 : 1;
        }

        int x = Math.Clamp(b.X + dx, X1, X2), y = Math.Clamp(b.Y + dy, Y1, Y2);
        var z = Map == null || Map.CanSpawnMobile(x, y, Z) ? Z : Map.GetAverageZ(x, y);
        return new Point3D(x, y, z);
    }
}

public class ColorWarsRegion : BaseRegion
{
    public ColorWarsRegion(ColorWarsRun run, Region parent)
        : base(null, run.Map, parent, (parent?.Priority ?? DefaultPriority) + 1,
            new Rectangle3D(new Point3D(run.X1, run.Y1, MinZ), new Point3D(run.X2 + 1, run.Y2 + 1, MaxZ)))
    {
        Run = run;
    }

    public ColorWarsRun Run { get; }

    private static bool Staff(Mobile m) => m.AccessLevel > AccessLevel.Player;

    // Walking in is for players of this run only: everyone else joins through the gate.
    public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation)
    {
        if (m is PlayerMobile pm && !Staff(m) && !Contains(oldLocation) && !Run.Players.ContainsKey(pm))
        {
            m.SendMessage("Color Wars is joined through its gate.");
            return false;
        }

        return base.OnMoveInto(m, d, newLocation, oldLocation);
    }

    // A player who walks off the field is brought back to the team's base: they leave by the exit stone.
    public override void OnExit(Mobile m)
    {
        base.OnExit(m);

        if (m is PlayerMobile pm && StaffColorWars.ShouldPullBack(pm, Run))
        {
            Timer.DelayCall(TimeSpan.Zero, () => StaffColorWars.PullBack(pm, Run));
        }
    }

    public override bool AllowHarmful(Mobile from, Mobile target)
    {
        var a = StaffColorWars.TeamOf(from);
        var b = StaffColorWars.TeamOf(target);

        if (a != null && b != null)
        {
            return Run.Phase == ColorWarsPhase.Fighting && a != b;
        }

        if (a != null || b != null)
        {
            return Staff(from); // a player of the event and someone outside it: no
        }

        return base.AllowHarmful(from, target);
    }

    public override bool AllowBeneficial(Mobile from, Mobile target)
    {
        var a = StaffColorWars.TeamOf(from);
        var b = StaffColorWars.TeamOf(target);

        if (a != null || b != null)
        {
            return Staff(from) || a == b && a != null; // teammates only
        }

        return base.AllowBeneficial(from, target);
    }

    // Nothing is learned in the arena: no skill gain, and so no stat gain.
    public override bool AllowGain(Mobile m, Skill skill, object obj) => false;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;
        return Staff(m) && base.CheckTravel(m, newLocation, travelType, out message);
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;
}

public static class StaffColorWars
{
    public const int BroadcastHue = 0x35;
    public const int StoneItemID = 0x0EDB; // 3803, the original's winning stone
    public const int GateItemID = 0x0F6C;  // a blue moongate, as the original's gate

    public static readonly string[] TeamNames = ["Red", "Blue", "White", "Black"];
    public static readonly int[] TeamHues = [0x0021, 0x0003, 0x0481, 0x0455];

    private static StaffEventsSettings S => StaffEventsSettings.Current;

    public static ColorWarsRun Active { get; private set; }

    public static string Name(ColorWarsTeam t) => TeamNames[(int)t];

    public static int Hue(ColorWarsTeam t) => TeamHues[(int)t];

    // Each GM's arena corners, marked before the arena is opened.
    private static readonly Dictionary<Mobile, (Map Map, Point3D First, Point3D? Second)> _corners = new();

    // Players being moved out by our own code, so the region does not pull them back.
    private static readonly HashSet<PlayerMobile> _leaving = [];

    public static (Map Map, Point3D First, Point3D? Second)? CornersOf(Mobile gm) =>
        _corners.TryGetValue(gm, out var c) ? c : null;

    public static void MarkCorner(Mobile gm, Map map, Point3D p, bool first)
    {
        if (first || !_corners.TryGetValue(gm, out var c) || c.Map != map)
        {
            _corners[gm] = (map, p, null);
        }
        else
        {
            _corners[gm] = (map, c.First, p);
        }
    }

    public static ColorWarsParticipant ParticipantOf(Mobile m) =>
        Active is { } run && m is PlayerMobile pm && run.Players.TryGetValue(pm, out var p) ? p : null;

    /// <summary>The team of a player in the event, or of a creature a player in the event controls or summoned.</summary>
    public static ColorWarsTeam? TeamOf(Mobile m)
    {
        if (m is BaseCreature bc && bc.GetMaster() is { } master)
        {
            m = master;
        }

        return ParticipantOf(m)?.Team;
    }

    // ---------------------------------------------------------------- hooks

    private static NotorietyHandler _ours;

    [CallPriority(100)] // after pinned's NotorietyHandlers.Initialize (default 50), which sets the handler we wrap
    public static void Initialize() => EnsureHooks();

    /// <summary>
    /// Wraps the notoriety handler: teammates are Ally (green), the other teams Enemy (orange). Wraps again only if
    /// something has put another handler in since (the facts swap pinned's in and out).
    /// </summary>
    public static void EnsureHooks()
    {
        if (_ours != null && Notoriety.Handler == _ours)
        {
            return;
        }

        var inner = Notoriety.Handler;
        _ours = (source, target) => TeamNotoriety(source, target) ?? inner?.Invoke(source, target) ?? Notoriety.Innocent;
        Notoriety.Handler = _ours;
    }

    public static int? TeamNotoriety(Mobile source, Mobile target)
    {
        if (Active == null || source == null || target == null)
        {
            return null;
        }

        var a = TeamOf(source);
        var b = TeamOf(target);
        if (a == null || b == null)
        {
            return null;
        }

        return a == b ? Notoriety.Ally : Notoriety.Enemy;
    }

    public static void Configure() => EventSink.WorldLoad += RegisterPending;

    // ---------------------------------------------------------------- staff: open, gate, fight, end

    public static bool Open(Mobile gm, int teams, out string message)
    {
        if (CornersOf(gm) is not { Second: { } second } c)
        {
            message = "Mark both arena corners first.";
            return false;
        }

        return Open(c.Map, c.First, second, teams, out message);
    }

    public static bool Open(Map map, Point3D first, Point3D second, int teams, out string message)
    {
        if (Active != null)
        {
            message = "Color Wars is already open. End it first.";
            return false;
        }

        if (teams is not (2 or 4))
        {
            message = "Color Wars has 2 or 4 teams.";
            return false;
        }

        if (map == null || map == Map.Internal || map == Tutorial.TutorialWorld.Isle)
        {
            message = "Not there: pick an arena on a facet, off the Wakening Isle.";
            return false;
        }

        var run = new ColorWarsRun
        {
            Map = map,
            X1 = Math.Min(first.X, second.X), X2 = Math.Max(first.X, second.X),
            Y1 = Math.Min(first.Y, second.Y), Y2 = Math.Max(first.Y, second.Y),
            Z = first.Z,
            TeamCount = teams
        };

        int w = run.X2 - run.X1 + 1, h = run.Y2 - run.Y1 + 1;
        if (Math.Min(w, h) < S.CwMinSide || w * h > S.CwMaxTiles)
        {
            message = $"The arena is {w} x {h}: each side at least {S.CwMinSide}, at most {S.CwMaxTiles} tiles in all.";
            return false;
        }

        Register(run);

        foreach (var team in run.Teams)
        {
            var stone = new ColorWarsExitStone(team);
            stone.MoveToWorld(run.StoneSpot(team), map);
            run.Stones.Add(stone);
        }

        message = $"Color Wars arena open: {w} x {h} in {map.Name}, {teams} teams. Now place the gate where players gather.";
        return true;
    }

    private static void Register(ColorWarsRun run)
    {
        var center = new Point3D((run.X1 + run.X2) / 2, (run.Y1 + run.Y2) / 2, run.Z);
        run.Region = new ColorWarsRegion(run, Region.Find(center, run.Map));
        run.Region.Register();
        Active = run;
    }

    /// <summary>Puts the join gate where the GM stands (or moves it there). Never inside the arena.</summary>
    public static bool PlaceGate(Map map, Point3D at, out string message)
    {
        if (Active is not { } run)
        {
            message = "Open the arena first.";
            return false;
        }

        if (map == run.Map && run.Contains(at.X, at.Y))
        {
            message = "The gate goes outside the arena, where players gather.";
            return false;
        }

        if (run.Gate is not { Deleted: false })
        {
            run.Gate = new ColorWarsGate();
        }

        run.Gate.MoveToWorld(at, map);
        message = $"The Color Wars gate stands at {at.X}, {at.Y} in {map.Name}.";
        World.Broadcast(BroadcastHue, false,
            $"Color Wars is open! Step through the gate at {at.X}, {at.Y} in {map.Name} to join a team. Your own things stay safe in your bank.");
        return true;
    }

    public static bool StartBattle(out string message)
    {
        if (Active is not { } run)
        {
            message = "Color Wars is not open.";
            return false;
        }

        if (run.Phase == ColorWarsPhase.Fighting)
        {
            message = "The battle has already begun.";
            return false;
        }

        run.Phase = ColorWarsPhase.Fighting;
        Tell(run, 0x22, "The walls are down: the battle has begun! Fight for your colour.");
        message = $"The battle has begun with {run.Players.Count} players.";
        return true;
    }

    /// <summary>The team with the most kills, or null for a draw (a tie at the top, or no kills).</summary>
    public static ColorWarsTeam? Leader(ColorWarsRun run)
    {
        var best = run.Teams.Max(t => run.Scores[(int)t]);
        var top = run.Teams.Where(t => run.Scores[(int)t] == best).ToList();
        return best > 0 && top.Count == 1 ? top[0] : null;
    }

    /// <summary>Ends the event: the leading team (or none, with pay false) is paid, then everyone goes home.</summary>
    public static bool End(bool pay, out string message) => End(pay ? Leader(Active) : null, pay, out message);

    public static bool End(ColorWarsTeam? winner, bool pay, out string message)
    {
        if (Active is not { } run)
        {
            message = "Color Wars is not open.";
            return false;
        }

        var score = string.Join(", ", run.Teams.Select(t => $"{Name(t)} {run.Scores[(int)t]}"));

        if (pay && winner is { } w)
        {
            foreach (var p in run.Players.Values.Where(p => p.Team == w).ToList())
            {
                FestivalTokens.Grant(p.Mobile, S.CwWinTokens, $"Color Wars, the {Name(w)} team won");
            }

            World.Broadcast(BroadcastHue, false, $"The {Name(w)} team has won Color Wars! ({score})");
        }
        else
        {
            World.Broadcast(BroadcastHue, false, pay ? $"Color Wars ends in a draw. ({score})" : "Color Wars has been called off.");
        }

        foreach (var pm in run.Players.Keys.ToList())
        {
            Leave(pm, "Color Wars is over. Your own things are back with you.");
        }

        run.Gate?.Delete();
        foreach (var stone in run.Stones)
        {
            stone.Delete();
        }

        run.Region?.Unregister();
        Active = null;
        message = pay && winner is { } won ? $"Color Wars ended: {Name(won)} won ({score})." : $"Color Wars ended with no winner ({score}).";
        return true;
    }

    // ---------------------------------------------------------------- players: join, leave, die

    /// <summary>Why a character cannot join now, or null if it can.</summary>
    public static string CannotJoin(PlayerMobile pm)
    {
        if (Active is not { } run)
        {
            return "Color Wars is not running.";
        }

        if (pm.AccessLevel > AccessLevel.Player)
        {
            return "Staff run Color Wars; they do not play in it.";
        }

        if (run.Players.ContainsKey(pm))
        {
            return "You are already in Color Wars.";
        }

        if (!pm.Alive)
        {
            return "The dead cannot join Color Wars.";
        }

        if (pm.Young)
        {
            return "Young characters cannot join Color Wars, a fight between players.";
        }

        if (pm.Mounted)
        {
            return "Dismount and stable your mount before you join.";
        }

        if (pm.Backpack == null || pm.Map == null || pm.Map == Map.Internal)
        {
            return "You cannot join from here.";
        }

        // An ungraduated trainee on Haven Island may not leave it (TutorialIslandGate), so must not be stripped for a
        // trip that would be refused.
        if (!Tutorial.TutorialIslandGate.CanMove(pm, run.Base(PickTeam(run)), run.Map, false))
        {
            return "Finish the tutorial and graduate before you join Color Wars.";
        }

        return null;
    }

    /// <summary>The team with the fewest players; a tie goes to a random one of them.</summary>
    public static ColorWarsTeam PickTeam(ColorWarsRun run)
    {
        var least = run.Teams.Min(run.CountOf);
        var open = run.Teams.Where(t => run.CountOf(t) == least).ToList();
        return open[Utility.Random(open.Count)];
    }

    public static bool Join(PlayerMobile pm, out string message)
    {
        if (CannotJoin(pm) is { } why)
        {
            message = why;
            return false;
        }

        var run = Active;
        var team = PickTeam(run);
        var p = new ColorWarsParticipant
        {
            Mobile = pm, Team = team, ReturnMap = pm.Map, ReturnLocation = pm.Location
        };

        Stash(pm, p);
        GiveKit(pm, p);
        pm.HueMod = Hue(team);
        run.Players[pm] = p;

        pm.MoveToWorld(run.Base(team), run.Map);
        pm.PlaySound(0x1FE);
        RefreshNames(run, pm);
        message = $"You join the {Name(team)} team! Your own things wait in a bag in your bank. Leave by your team's stone.";
        pm.SendMessage(0x44, message);
        if (run.Phase == ColorWarsPhase.Gathering)
        {
            pm.SendMessage("The teams are gathering: no one can fight until a GM starts the battle.");
        }

        Tell(run, BroadcastHue, $"{pm.Name} joins the {Name(team)} team.");
        return true;
    }

    // Everything worn (but the pack, hair and the bank) and everything carried goes into a bag in the bank.
    private static void Stash(PlayerMobile pm, ColorWarsParticipant p)
    {
        var bag = new Bag { Name = "Your belongings (Color Wars)" };

        foreach (var item in pm.Items.ToList())
        {
            if (item == pm.Backpack || item.Layer is Layer.Bank or Layer.Hair or Layer.FacialHair or Layer.Mount)
            {
                continue;
            }

            p.Worn.Add(item);
            bag.DropItem(item);
        }

        foreach (var item in pm.Backpack.Items.ToList())
        {
            bag.DropItem(item);
        }

        pm.BankBox.DropItem(bag);
        p.Stash = bag;
    }

    private static void GiveKit(PlayerMobile pm, ColorWarsParticipant p)
    {
        var hue = Hue(p.Team);

        void Wear(Item item)
        {
            item.Hue = hue;
            item.LootType = LootType.Regular; // lent, never blessed
            p.Kit.Add(item);
            if (!pm.EquipItem(item))
            {
                pm.Backpack.DropItem(item);
            }
        }

        void Carry(Item item)
        {
            item.Hue = hue;
            item.LootType = LootType.Regular;
            p.Kit.Add(item);
            pm.Backpack.DropItem(item);
        }

        Wear(new Robe());

        if (S.CwKitArmor)
        {
            Wear(new StuddedChest());
            Wear(new StuddedArms());
            Wear(new StuddedGloves());
            Wear(new StuddedLegs());
            Wear(new StuddedGorget());
            Wear(new LeatherCap());
        }

        if (S.CwKitWeapons)
        {
            Wear(new VikingSword());
            Carry(new Bow());
            if (S.CwArrows > 0)
            {
                Carry(new Arrow(S.CwArrows));
            }
        }

        if (S.CwKitSpellbook)
        {
            Carry(new Spellbook(ulong.MaxValue));
            if (S.CwReagents > 0)
            {
                var bag = new Bag { Name = "event reagents" };
                foreach (var reg in Reagents(S.CwReagents))
                {
                    reg.Hue = hue;
                    bag.DropItem(reg);
                }

                Carry(bag);
            }
        }

        if (S.CwBandages > 0)
        {
            Carry(new Bandage(S.CwBandages));
        }
    }

    private static IEnumerable<Item> Reagents(int n) =>
    [
        new BlackPearl(n), new Bloodmoss(n), new Garlic(n), new Ginseng(n),
        new MandrakeRoot(n), new Nightshade(n), new SulfurousAsh(n), new SpidersSilk(n)
    ];

    /// <summary>A kit piece, wherever it went: a lent item, or a stack split off one (same kind, the team's colour).</summary>
    public static bool IsKit(Item item, ColorWarsParticipant p) =>
        p.Kit.Contains(item) ||
        item.Hue == Hue(p.Team) && item is BaseArmor or BaseWeapon or Robe or Arrow or Spellbook or BaseReagent or Bandage;

    /// <summary>Takes the kit back and gives the player's own things back, then sends them home.</summary>
    public static bool Leave(PlayerMobile pm, string message)
    {
        if (Active is not { } run || !run.Players.TryGetValue(pm, out var p))
        {
            return false;
        }

        if (pm.Deleted)
        {
            run.Players.Remove(pm); // a deleted character: its bank and stash went with it
            return true;
        }

        _leaving.Add(pm);

        try
        {
            if (!pm.Alive)
            {
                pm.Resurrect();
            }

            (pm.FindItemOnLayer(Layer.OuterTorso) as DeathRobe)?.Delete();

            // The lent kit, wherever it is, and anything split off it that the player still has.
            foreach (var item in p.Kit)
            {
                item.Delete();
            }

            foreach (var item in Carried(pm).Where(i => IsKit(i, p)).ToList())
            {
                item.Delete();
            }

            // Their own things.
            if (p.Stash is { Deleted: false } stash)
            {
                foreach (var item in p.Worn.Where(i => !i.Deleted && i.Parent == stash).ToList())
                {
                    if (!pm.EquipItem(item))
                    {
                        pm.Backpack.DropItem(item);
                    }
                }

                foreach (var item in stash.Items.ToList())
                {
                    pm.Backpack.DropItem(item);
                }

                stash.Delete();
            }
            else
            {
                pm.SendMessage(0x22, "Your belongings bag was not in your bank. Please page a GM.");
            }

            pm.HueMod = -1;
            pm.Hits = pm.HitsMax;
            run.Players.Remove(pm);
            RefreshNames(run, pm);

            if (pm.Map == Map.Internal)
            {
                // Logged out: they wake where they joined.
                pm.LogoutMap = p.ReturnMap;
                pm.LogoutLocation = p.ReturnLocation;
            }
            else
            {
                pm.MoveToWorld(p.ReturnLocation, p.ReturnMap);
            }

            pm.SendMessage(0x44, message);
        }
        finally
        {
            _leaving.Remove(pm);
        }

        return true;
    }

    private static List<Item> Carried(PlayerMobile pm)
    {
        var list = new List<Item>();
        foreach (var item in pm.Items)
        {
            if (item.Layer != Layer.Bank && item != pm.Backpack)
            {
                list.Add(item);
            }
        }

        if (pm.Backpack is { } pack)
        {
            Collect(pack, list);
        }

        return list;
    }

    private static void Collect(Container c, List<Item> into)
    {
        foreach (var item in c.Items)
        {
            into.Add(item);
            if (item is Container inner)
            {
                Collect(inner, into);
            }
        }
    }

    public static bool ShouldPullBack(PlayerMobile pm, ColorWarsRun run) =>
        Active == run && run.Players.ContainsKey(pm) && !_leaving.Contains(pm) &&
        pm.Map != null && pm.Map != Map.Internal && pm.NetState != null;

    public static void PullBack(PlayerMobile pm, ColorWarsRun run)
    {
        if (!ShouldPullBack(pm, run) || pm.Map == run.Map && run.Contains(pm.X, pm.Y))
        {
            return;
        }

        pm.MoveToWorld(run.Base(run.Players[pm].Team), run.Map);
        pm.SendMessage("The battle field holds you. To leave Color Wars, double-click your team's stone.");
    }

    // A death in the arena keeps every item, scores for the killer's team, and the player rises at the team's base.
    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (Active is not { } run || !run.Players.TryGetValue(pm, out var p))
        {
            return;
        }

        if (pm.Corpse is { Deleted: false } corpse)
        {
            if (pm.Backpack is { } pack)
            {
                foreach (var item in corpse.Items.ToList())
                {
                    pack.DropItem(item);
                }
            }

            corpse.Delete();
        }

        var killer = pm.LastKiller is BaseCreature bc && bc.GetMaster() is { } master ? master : pm.LastKiller;
        pm.LastKiller = null;

        if (TeamOf(killer) is { } kt && kt != p.Team)
        {
            run.Scores[(int)kt]++;
            Tell(run, Hue(kt), $"{killer.Name} ({Name(kt)}) defeats {pm.Name} ({Name(p.Team)}). {ScoreLine(run)}");

            if (S.CwKillsToWin > 0 && run.Scores[(int)kt] >= S.CwKillsToWin)
            {
                Timer.DelayCall(TimeSpan.Zero, () =>
                {
                    if (Active == run)
                    {
                        End(kt, true, out _);
                    }
                });
            }
        }

        pm.SendMessage(0x44, $"Your kit is safe in your backpack. You will rise at your base in {S.CwReviveSeconds} seconds.");
        Timer.DelayCall(TimeSpan.FromSeconds(S.CwReviveSeconds), Revive, pm);
    }

    public static void Revive(PlayerMobile pm)
    {
        if (pm.Deleted || pm.Alive || Active is not { } run || !run.Players.TryGetValue(pm, out var p))
        {
            return;
        }

        pm.MoveToWorld(run.Base(p.Team), run.Map);
        pm.Resurrect();
        (pm.FindItemOnLayer(Layer.OuterTorso) as DeathRobe)?.Delete();

        // Put the kit back on.
        foreach (var item in p.Kit.Where(i => !i.Deleted && i.Parent == pm.Backpack && i.Layer != Layer.Invalid).ToList())
        {
            if (pm.FindItemOnLayer(item.Layer) == null)
            {
                pm.EquipItem(item);
            }
        }

        pm.Hits = pm.HitsMax;
        pm.Stam = pm.StamMax;
        pm.Mana = pm.ManaMax;
    }

    /// <summary>
    /// A join or a leave changes how the event's players see each other (green, orange, or back to the usual), but the
    /// clients that already show them keep the old colour, and TazUO then asks "This may flag you criminal" before an
    /// attack on a foe (seen in the proof run). So each player in the event, and the one who joined or left, is sent
    /// again to every client in range with the notoriety that client should now see.
    /// </summary>
    public static void RefreshNames(ColorWarsRun run, PlayerMobile changed)
    {
        changed.Delta(MobileDelta.Noto);
        foreach (var pm in run.Players.Keys)
        {
            pm.Delta(MobileDelta.Noto);
        }
    }

    public static string ScoreLine(ColorWarsRun run) =>
        string.Join(", ", run.Teams.Select(t => $"{Name(t)} {run.Scores[(int)t]}"));

    private static void Tell(ColorWarsRun run, int hue, string text)
    {
        foreach (var pm in run.Players.Keys)
        {
            pm.SendMessage(hue, text);
        }
    }

    public static List<string> Status()
    {
        if (Active is not { } run)
        {
            return ["Color Wars: not open."];
        }

        var gate = run.Gate is { Deleted: false } g ? $"gate at {g.X},{g.Y} in {g.Map?.Name}" : "no gate yet";
        return
        [
            $"Color Wars: {run.X1},{run.Y1} to {run.X2},{run.Y2} in {run.Map.Name}, {run.TeamCount} teams, {gate}",
            $"{(run.Phase == ColorWarsPhase.Fighting ? "Fighting" : "Gathering")}: " +
            string.Join(", ", run.Teams.Select(t => $"{Name(t)} {run.CountOf(t)} players, {run.Scores[(int)t]} kills"))
        ];
    }

    /// <summary>Whether an item is the live run's gate or one of its stones (others remove themselves after load).</summary>
    public static bool Owns(Item item) =>
        Active is { } run && (run.Gate == item || run.Stones.Contains(item)) ||
        _pending is { } p && (p.Gate == item || p.Stones.Contains(item));

    // ---------------------------------------------------------------- save

    internal static void Write(IGenericWriter w)
    {
        var run = Active;
        w.Write(run != null);
        if (run == null)
        {
            return;
        }

        w.Write(run.Map);
        w.Write(run.X1);
        w.Write(run.Y1);
        w.Write(run.X2);
        w.Write(run.Y2);
        w.Write(run.Z);
        w.WriteEncodedInt(run.TeamCount);
        w.WriteEncodedInt((int)run.Phase);
        for (var i = 0; i < 4; i++)
        {
            w.WriteEncodedInt(run.Scores[i]);
        }

        w.Write(run.Gate);
        WriteItems(w, run.Stones);

        w.WriteEncodedInt(run.Players.Count);
        foreach (var p in run.Players.Values)
        {
            w.Write(p.Mobile);
            w.WriteEncodedInt((int)p.Team);
            w.Write(p.Stash);
            WriteItems(w, p.Worn);
            WriteItems(w, p.Kit);
            w.Write(p.ReturnMap);
            w.Write(p.ReturnLocation);
        }
    }

    private static void WriteItems(IGenericWriter w, List<Item> items)
    {
        w.WriteEncodedInt(items.Count);
        foreach (var item in items)
        {
            w.Write(item);
        }
    }

    private static void ReadItems(IGenericReader r, List<Item> into)
    {
        var count = r.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            if (r.ReadEntity<Item>() is { } item)
            {
                into.Add(item);
            }
        }
    }

    internal static void Read(IGenericReader r)
    {
        ForgetForTests();

        if (!r.ReadBool())
        {
            return;
        }

        var run = new ColorWarsRun
        {
            Map = r.ReadMap(),
            X1 = r.ReadInt(), Y1 = r.ReadInt(), X2 = r.ReadInt(), Y2 = r.ReadInt(), Z = r.ReadInt(),
            TeamCount = r.ReadEncodedInt(),
            Phase = (ColorWarsPhase)r.ReadEncodedInt()
        };

        for (var i = 0; i < 4; i++)
        {
            run.Scores[i] = r.ReadEncodedInt();
        }

        run.Gate = r.ReadEntity<Item>();
        ReadItems(r, run.Stones);

        var count = r.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var p = new ColorWarsParticipant
            {
                Mobile = r.ReadEntity<PlayerMobile>(),
                Team = (ColorWarsTeam)r.ReadEncodedInt(),
                Stash = r.ReadEntity<Container>()
            };
            ReadItems(r, p.Worn);
            ReadItems(r, p.Kit);
            p.ReturnMap = r.ReadMap();
            p.ReturnLocation = r.ReadPoint3D();

            if (p.Mobile != null)
            {
                run.Players[p.Mobile] = p;
            }
        }

        if (run.Map == null || run.Map == Map.Internal)
        {
            return;
        }

        _pending = run; // registered once the world (and its regions) has loaded
    }

    private static ColorWarsRun _pending;

    /// <summary>World load: the saved run's region is registered again. Idempotent; the facts call it directly.</summary>
    public static void RegisterPending()
    {
        if (_pending is { } run && Active == null)
        {
            Register(run);
        }

        _pending = null;
    }

    /// <summary>For the facts: drops the run and the marked corners without broadcasting or moving anyone.</summary>
    public static void ForgetForTests()
    {
        Active?.Region?.Unregister();
        Active = null;
        _pending = null;
        _corners.Clear();
        _leaving.Clear();
    }
}

/// <summary>The join gate: stepping into it (or double-clicking it) asks the player whether to join.</summary>
[SerializationGenerator(0, false)]
public partial class ColorWarsGate : Item
{
    [Constructible]
    public ColorWarsGate() : base(StaffColorWars.GateItemID)
    {
        Movable = false;
        Light = LightType.Circle300;
        Hue = 0x481;
    }

    public override string DefaultName => "the Color Wars gate";

    public override bool OnMoveOver(Mobile m)
    {
        Offer(m);
        return true;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(GetWorldLocation(), 2))
        {
            Offer(from);
        }
    }

    private static void Offer(Mobile m)
    {
        if (m is not PlayerMobile pm || pm.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        if (StaffColorWars.CannotJoin(pm) is { } why)
        {
            pm.SendMessage(why);
            return;
        }

        pm.CloseGump<ColorWarsJoinGump>();
        pm.SendGump(new ColorWarsJoinGump());
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(TimeSpan.Zero, RemoveIfOrphaned);

    private void RemoveIfOrphaned()
    {
        if (!StaffColorWars.Owns(this))
        {
            Delete();
        }
    }
}

/// <summary>A team's exit stone in the arena: a player of the event double-clicks it to leave.</summary>
[SerializationGenerator(0, false)]
public partial class ColorWarsExitStone : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ColorWarsTeam _team;

    [Constructible]
    public ColorWarsExitStone(ColorWarsTeam team = ColorWarsTeam.Red) : base(StaffColorWars.StoneItemID)
    {
        Movable = false;
        _team = team;
        Hue = StaffColorWars.Hue(team);
    }

    public override string DefaultName => $"the {StaffColorWars.Name(_team)} team's stone: leave Color Wars";

    public override void OnDoubleClickDead(Mobile m) => OnDoubleClick(m);

    public override void OnDoubleClick(Mobile from)
    {
        if (StaffColorWars.ParticipantOf(from) == null)
        {
            from.SendMessage("Only players of Color Wars use this stone.");
            return;
        }

        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        StaffColorWars.Leave((PlayerMobile)from, "You leave Color Wars. Your own things are back with you. Thank you for playing!");
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(TimeSpan.Zero, RemoveIfOrphaned);

    private void RemoveIfOrphaned()
    {
        if (!StaffColorWars.Owns(this))
        {
            Delete();
        }
    }
}

/// <summary>Asks a player at the gate whether to join, and says what happens to their things.</summary>
public class ColorWarsJoinGump : Gump
{
    public const int BtnJoin = 1;

    public ColorWarsJoinGump() : base(120, 100)
    {
        AddBackground(0, 0, 400, 290, 9270);
        AddImageTiled(16, 16, 368, 258, 2624);
        AddLabel(30, 24, StaffHubGump.HueGold, "Color Wars: join a team?");
        AddHtml(30, 50, 340, 180,
            "<BASEFONT COLOR=#FFFFFF>A team battle between players, run by staff. You are put on a team and fight with a " +
            "lent kit in your team's colour.<BR><BR>Everything you wear and carry waits in a bag in your bank, and comes " +
            "back when you leave or the battle ends. A death here costs nothing: you rise at your base. Nothing is learned " +
            "here (no skill or stat gain).</BASEFONT>", false, false);
        AddButton(30, 240, 4005, 4007, BtnJoin);
        AddLabel(64, 242, StaffHubGump.HueWhite, "Join");
        AddButton(220, 240, 4017, 4019, 0);
        AddLabel(254, 242, StaffHubGump.HueWhite, "Not now");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is PlayerMobile pm)
        {
            Respond(pm, info.ButtonID);
        }
    }

    /// <summary>A button press, without a client packet; public for the facts.</summary>
    public static bool Respond(PlayerMobile pm, int id)
    {
        if (id != BtnJoin)
        {
            return false;
        }

        var ok = StaffColorWars.Join(pm, out var message);
        if (!ok)
        {
            pm.SendMessage(0x22, message);
        }

        return ok;
    }
}

/// <summary>The run's save; versioned, an unknown version fails loudly (D40).</summary>
public class StaffColorWarsStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static StaffColorWarsStore _instance;

    public StaffColorWarsStore() : base("StaffColorWars", 10)
    {
    }

    public static void Configure() => _instance ??= new StaffColorWarsStore();

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        StaffColorWars.Write(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"StaffColorWarsStore version {version} is not one this build can read.");
        }

        StaffColorWars.Read(reader);
    }
}
