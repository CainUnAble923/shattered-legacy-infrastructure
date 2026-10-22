// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Spawner.cs (cc-P408). The Treasures of Sorcerer's
// Dungeon spawner: nine rooms of Ilshenar's Sorcerer's Dungeon in turn, one at a time. In the current room it keeps
// up to 15 undead (Zombie, Skeleton, Gargoyle, Lich, LichLord), one every 3 to 8 seconds; when 80 of them have died it
// raises the room's boss; when the boss is gone it waits 1 to 3 minutes and moves to the next room. The rotation:
// Nightmare Fairy, Jack in the Box, Headless Elf, Abominable Snowman, Twisted Holiday Tree, Rabid Reindeer, Garish
// Gingerman, Stocking Serpent, Jack the Pumpkin King, then round again. [TOSDSpawner (Administrator) shows the rooms
// and teleports the caller into one.
//
// Not an Item: ServUO's spawner is a plain object saved inside the points system (SorcerersDungeonData), and so is
// this one. Its timer is pinned's Timer.DelayCall, started by Initialize after the world loads, as in ServUO.
//
// Conversion notes and the two bugs fixed (deviations 5 and 6 in the notes):
//   - XmlSpawner: ServUO sets DoReset on every XmlSpawner inside the nine rooms when the event starts (they stop and
//     their creatures go) and DoRespawn when it ends (they start again, full). The shard has no XmlSpawner; the
//     stock spawners are pinned's BaseSpawner (Engines/Spawners), whose Reset() is Stop() plus RemoveSpawns()
//     (BaseSpawner.cs:418) and whose Start() and Respawn() are the other half (:769, :406). Only spawners that were
//     running are paused, and their serials are saved (PausedSpawners), so ending the event restarts exactly those
//     and never one that staff had switched off.
//   - Map.GetRandomSpawnPoint(rect) is ServUO's (x and y in the rectangle inclusive, z the land's average); pinned has
//     no such method, so RandomPoint below is its body. Map.CanSpawnMobile is pinned's (Map.cs:1100).
//   - Fixed: the staff gump's teleport loops forever when no point in a room can take a mobile (ServUO's
//     do { } while (true)); here it tries 50 points and says so.
//   - Fixed: a spawned creature deleted without dying (a staff [delete, a cleanup) stays in ServUO's spawn list for
//     ever, so the room never refills and the boss can never come. Here OnTick drops deleted entries first (they do
//     not count as kills).

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.SorcerersDungeon;

public class TOSDSpawnEntry
{
    public TOSDSpawnEntry(Type boss, Type[] spawn, Rectangle2D area, int toKill, int maxSpawn)
    {
        Boss = boss;
        Spawn = spawn;
        SpawnArea = area;
        ToKill = toKill;
        MaxSpawn = maxSpawn;
    }

    public Type Boss { get; }
    public Type[] Spawn { get; }
    public Rectangle2D SpawnArea { get; }
    public int ToKill { get; }
    public int MaxSpawn { get; }
}

public class TOSDSpawner
{
    public const int CurrentVersion = 0;

    public TOSDSpawner(bool activate = true)
    {
        Instance = this;

        Entries = new List<TOSDSpawnEntry>();
        Spawn = new List<BaseCreature>();

        BuildEntries();

        if (activate)
        {
            Activate();
        }
    }

    public static TOSDSpawner Instance { get; set; }

    public BaseCreature Boss { get; set; }
    public int Index { get; set; }
    public int KillCount { get; set; }
    public List<BaseCreature> Spawn { get; }

    public bool Spawning { get; set; }
    public Timer Timer { get; set; }
    public List<TOSDSpawnEntry> Entries { get; }

    /// <summary>The stock spawners this event stopped (ours, for XmlSpawner's DoReset / DoRespawn): restarted at the end.</summary>
    public List<BaseSpawner> PausedSpawners { get; } = new();

    public static Map Map => Map.Ilshenar;

    public static void Initialize()
    {
        CommandSystem.Register("TOSDSpawner", AccessLevel.Administrator, TOSDSpawner_OnCommand);

        Instance?.BeginTimer();
    }

    [Usage("TOSDSpawner")]
    [Description("Shows the Treasures of Sorcerer's Dungeon spawner: the nine rooms, the current one, and a button to go to each.")]
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true, Summary = "Shows the Treasures of Sorcerer's Dungeon spawner (ServUO's gump): the nine rooms, which is current, its spawn and kill count, and a button that teleports you into a room.")]
    public static void TOSDSpawner_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm)
        {
            if (Instance != null)
            {
                pm.SendGump(new TOSDSpawnerGump(pm));
            }
            else
            {
                pm.SendMessage("This spawner is not set up at this time. Enabled Treasures of Sorcerer's Dungeon to enable the spawner.");
            }
        }
    }

    private void Activate()
    {
        PausedSpawners.Clear();

        foreach (var entry in Entries)
        {
            foreach (var spawner in Map.GetItemsInBounds<BaseSpawner>(entry.SpawnArea))
            {
                if (spawner.Running && !PausedSpawners.Contains(spawner))
                {
                    PausedSpawners.Add(spawner);
                }
            }
        }

        foreach (var spawner in PausedSpawners)
        {
            spawner.Reset();
        }
    }

    public void Deactivate()
    {
        foreach (var spawner in PausedSpawners)
        {
            if (!spawner.Deleted)
            {
                spawner.Start();
                spawner.Respawn();
            }
        }

        PausedSpawners.Clear();

        EndTimer();

        foreach (var bc in Spawn)
        {
            bc.Delete();
        }

        Spawn.Clear();

        // ServUO leaves a standing boss in the world when the event ends (its Deactivate deletes only the spawn list);
        // the boss is the event's, so it goes with the event here (deviation 7).
        if (Boss?.Deleted == false)
        {
            Boss.Delete();
        }

        Boss = null;
        Instance = null;
    }

    public void BeginTimer()
    {
        EndTimer();

        Timer = Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick);
    }

    public void EndTimer()
    {
        if (Timer != null)
        {
            Timer.Stop();
            Timer = null;
        }
    }

    public void OnTick()
    {
        if (Spawning)
        {
            return;
        }

        Spawn.RemoveAll(bc => bc == null || bc.Deleted);

        if (Boss != null && Boss.Deleted)
        {
            if (Index == Entries.Count - 1)
            {
                Index = 0;
            }
            else
            {
                Index++;
            }

            Reset();
        }
        else if (Boss == null && KillCount >= Entries[Index].ToKill)
        {
            DoSpawn(Entries[Index].Boss, true);
        }
        else if (Spawn.Count < Entries[Index].MaxSpawn && (Boss == null || Entries[Index].SpawnArea.Contains(Boss.Location)))
        {
            Spawning = true;

            Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(3, 8)), () =>
            {
                if (Instance == this)
                {
                    DoSpawn(Entries[Index].Spawn[Utility.Random(Entries[Index].Spawn.Length)], false);
                }

                Spawning = false;
            });
        }
    }

    public void DoSpawn(Type t, bool boss)
    {
        if (Activator.CreateInstance(t) is not BaseCreature spawn)
        {
            return;
        }

        for (var i = 0; i < 20; i++)
        {
            var p = RandomPoint(Entries[Index].SpawnArea);

            if (Map.CanSpawnMobile(p))
            {
                spawn.MoveToWorld(p, Map);

                if (boss)
                {
                    Boss = spawn;
                }
                else
                {
                    AddSpawn(spawn);
                }

                return;
            }
        }

        spawn.Delete();
    }

    // ServUO Map.GetRandomSpawnPoint(Rectangle2D).
    public static Point3D RandomPoint(Rectangle2D rec)
    {
        var x = Utility.RandomMinMax(rec.X, rec.X + rec.Width);
        var y = Utility.RandomMinMax(rec.Y, rec.Y + rec.Height);
        var z = Map.GetAverageZ(x, y);

        return new Point3D(x, y, z);
    }

    public void AddSpawn(BaseCreature bc)
    {
        Spawn.Add(bc);
    }

    public void OnCreatureDeath(BaseCreature bc)
    {
        if (Spawn.Contains(bc))
        {
            Spawn.Remove(bc);

            KillCount++;
        }
    }

    public void Reset()
    {
        Spawn.Clear();
        Boss = null;
        Spawning = false;

        KillCount = 0;

        EndTimer();

        Timer.StartTimer(TimeSpan.FromMinutes(Utility.RandomMinMax(1, 3)), () =>
        {
            if (Instance == this)
            {
                BeginTimer();
            }
        });
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);

        writer.Write(Boss);
        writer.Write(KillCount);
        writer.Write(Index);

        var live = Spawn.FindAll(bc => bc?.Deleted == false);
        writer.Write(live.Count);

        foreach (var bc in live)
        {
            writer.Write(bc);
        }

        var paused = PausedSpawners.FindAll(s => s?.Deleted == false);
        writer.WriteEncodedInt(paused.Count);

        foreach (var spawner in paused)
        {
            writer.Write(spawner);
        }
    }

    public void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new System.IO.InvalidDataException($"TOSDSpawner version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Boss = reader.ReadEntity<BaseCreature>();
        KillCount = reader.ReadInt();
        Index = Math.Clamp(reader.ReadInt(), 0, Entries.Count - 1);

        var count = reader.ReadInt();

        for (var i = 0; i < count; i++)
        {
            var bc = reader.ReadEntity<BaseCreature>();

            if (bc != null)
            {
                AddSpawn(bc);
            }
        }

        var paused = reader.ReadEncodedInt();

        for (var i = 0; i < paused; i++)
        {
            if (reader.ReadEntity<BaseSpawner>() is { } spawner)
            {
                PausedSpawners.Add(spawner);
            }
        }
    }

    public void BuildEntries()
    {
        Entries.AddRange(CreateEntries());
    }

    /// <summary>ServUO BuildEntries' nine rooms, as a list (also read by the status command for the room bounds).</summary>
    public static List<TOSDSpawnEntry> CreateEntries()
    {
        Type[] undead = { typeof(Zombie), typeof(Skeleton), typeof(Gargoyle), typeof(Lich), typeof(LichLord) };
        var entries = new List<TOSDSpawnEntry>();

        entries.Add(new TOSDSpawnEntry(typeof(NightmareFairy), undead, new Rectangle2D(327, 26, 29, 32), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(JackInTheBoxMonster), undead, new Rectangle2D(296, 10, 17, 26), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(HeadlessElf), undead, new Rectangle2D(271, 4, 20, 33), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(AbominableSnowman), undead, new Rectangle2D(227, 39, 21, 19), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(TwistedHolidayTree), undead, new Rectangle2D(251, 68, 25, 32), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(RabidReindeer), undead, new Rectangle2D(144, 5, 23, 19), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(GarishGingerman), undead, new Rectangle2D(60, 53, 13, 34), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(StockingSerpent), undead, new Rectangle2D(152, 48, 16, 23), 80, 15));
        entries.Add(new TOSDSpawnEntry(typeof(JackThePumpkinKing), undead, new Rectangle2D(291, 73, 37, 36), 80, 15));

        return entries;
    }
}

/// <summary>ServUO TOSDSpawnerGump (a BaseGump), on pinned's DynamicGump.</summary>
public class TOSDSpawnerGump : DynamicGump
{
    private readonly PlayerMobile _user;

    public TOSDSpawnerGump(PlayerMobile pm) : base(50, 50) => _user = pm;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();

        builder.AddBackground(0, 0, 500, 300, 9300);
        builder.AddHtml(0, 10, 500, 20, "Treasures of Sorcerer's Dungeon Spawner", align: TextAlignment.Center);

        var spawner = TOSDSpawner.Instance;

        if (spawner == null)
        {
            builder.AddHtml(10, 40, 150, 20, "Spawner Disabled");
            return;
        }

        var y = 60;

        builder.AddLabel(10, 40, 0, "Go");
        builder.AddLabel(40, 40, 0, "Boss");
        builder.AddLabel(240, 40, 0, "Current");
        builder.AddLabel(320, 40, 0, "Max");
        builder.AddLabel(400, 40, 0, "Killed");

        for (var i = 0; i < spawner.Entries.Count; i++)
        {
            var entry = spawner.Entries[i];
            var current = i == spawner.Index;
            var hue = current ? "green" : "red";

            builder.AddButton(7, y, 1531, 1532, i + 100);
            builder.AddHtml(40, y, 200, 20, entry.Boss.Name, hue);
            builder.AddHtml(320, y, 80, 20, entry.MaxSpawn.ToString(), hue);
            builder.AddHtml(240, y, 80, 20, current ? spawner.Spawn.Count.ToString() : "0", hue);
            builder.AddHtml(400, y, 80, 20, current ? spawner.KillCount.ToString() : "0", hue);

            y += 22;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID - 100;
        var spawner = TOSDSpawner.Instance;

        if (spawner == null || id < 0 || id >= spawner.Entries.Count)
        {
            return;
        }

        var entry = spawner.Entries[id];

        for (var i = 0; i < 50; i++)
        {
            var p = TOSDSpawner.RandomPoint(entry.SpawnArea);

            if (TOSDSpawner.Map.CanSpawnMobile(p))
            {
                _user.MoveToWorld(p, TOSDSpawner.Map);
                _user.SendGump(new TOSDSpawnerGump(_user));
                return;
            }
        }

        _user.SendMessage($"No open spot found in the {entry.Boss.Name} room; try again.");
    }
}
