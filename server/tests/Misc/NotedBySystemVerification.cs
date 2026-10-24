// NotedBySystemVerification.cs
//
// cc-P263 (ours). The "Noted by the System" hidden achievements: customizations ClusterFNotedBySystem.cs, the joke items
// and the Dented Box (LootBoxes/NotedJokeItems.cs), the store-bought rule (ClusterFStoreBought.cs) and the commission
// abandon cooldown, and the hooks in server/patches/*-achievement.patch and SmallBOD-crafted-only.patch. Notes in
// shard-migration notes/cc-P263-noted-by-the-system-achievements.md.
//
// One fact per event family, each showing its achievements hidden until earned and earned once; the game's own path is
// driven wherever the test host can drive it (a kill, a gate, a runebook drop, a mining check on an emptied vein, a loot
// box double-click, a death in a house, a commission turn-in); a craft's success roll and a recycle's forge are not
// drivable here, so those call the hook the patch calls. Facts:
//   1 combat: Geography Major from a real kill; a creature that hit you, or a mage, never; a second kill earns nothing new.
//     cc-P427 (cc-P379 finding 3): a creature that hit you still counts as having reached you after you heal to full
//     (pinned empties the player's damage list at full hits), and a creature is forgotten once it is gone.
//     cc-P427 (cc-P379 finding 4): a released pet that is deleted, or whose window has passed, is forgotten.
//   2 bosses: Last Hit Larry (and its title) and Bare Minimum from one killing blow; a full-damage, shod kill neither.
//   3 pets and provocation: Not My Problem, Acceptable Losses, Remote Work, Commitment Issues (and its window).
//   4 travel: Tactical Retreat through a moongate, Wrong Turn from the death that follows, Any% Speedrun, Rune Hoarder
//     (a book counts once), and the step counters (overweight, lava barefoot, pantsless; a turn in place is no step).
//   5 crafting and mining: Skill Issue's streak, Built to Be Recycled and Quality Control, Rock Bottom on an empty vein.
//   6 housing: Home Sweet Grave, Hoarder, Superstitious and Lucky Spot (whose box is a Dented Box with a Lucky Rug).
//   7 boxes: Can't Wait (its earn box a Dented Box holding a bound Box-Shaped Box), Edge Lord, Unboxing Video.
//   8 commissions and orders: Pre-Crafted; store-bought items refused by a small deed and a commission; the abandon
//     cooldown.
//   9 data only and secrets: the five unreachable achievements are hidden; a Noted title is not in the picker until held.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Harvest;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

using N = Server.ClusterFNotedBySystem;
using A = Server.ClusterFAchievementSystem;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NotedBySystemVerification : IDisposable
{
    private static readonly Point3D Spot = new(1620, 1720, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<Player> _players = [];
    private readonly List<IEntity> _things = [];

    public NotedBySystemVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        if (A.Definitions.Count == 0)
        {
            A.Configure();
        }

        N.Register();
        LootTable.Current = LootTable.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ClusterFStoreBought.CraftedOnly = true;
        ShardTestClock.Arm();
    }

    public void Dispose()
    {
        ClusterFFanfare.FlushAll();

        foreach (var p in _players)
        {
            p.Dispose();
        }

        foreach (var e in _things)
        {
            e.Delete();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p263{Guid.NewGuid():N}"[..16], "p263-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P263 {Guid.NewGuid():N}"[..12] };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public bool Has(string key) => A.HasEarned(Account, key);

        public int Count(TriggerKind kind) => A.GetCounter(Account.Username, A.NotedCounter(kind));

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            A.ResetForAccount(Account.Username);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private Player NewPlayer(Point3D? at = null)
    {
        var p = new Player(at ?? Spot);
        _players.Add(p);
        return p;
    }

    private T Thing<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private T Placed<T>(T m, Point3D at) where T : Mobile
    {
        Thing(m).MoveToWorld(at, Map.Trammel);
        return m;
    }

    // What the achievements window lists, every page.
    private static string Listed(Player p) => string.Join(" ", Enumerable.Range(0, 40)
        .Select(i => new AchievementsGump(p.Pm, null, false, i))
        .SelectMany(g => g.Entries.OfType<GumpHtml>().Select(h => h.Text).Concat(g.Entries.OfType<GumpLabel>().Select(l => l.Text))));

    // Hidden before, listed (with [Secret]) after, and earned exactly once: a second qualifying event changes no AP.
    private void AssertHiddenThenEarned(Player p, string key, string listedBefore, Action again)
    {
        var def = A.Definitions[key];
        Assert.True(def.Hidden, key);
        Assert.Equal(AchievementCategory.Mischief, def.Category);
        Assert.DoesNotContain(def.Title, listedBefore);
        Assert.True(p.Has(key), $"{key} not earned");

        var after = Listed(p);
        Assert.Contains(def.Title, after);
        Assert.Contains("[Secret]", after);
        Assert.Contains(def.EarnedLine, after);

        var ap = p.Data.AchievementPoints;
        again();
        Assert.Equal(ap, p.Data.AchievementPoints);
        _out.WriteLine($"{def.Title}: {def.EarnedLine}");
    }

    private static void Dress(Mobile m, Item item, Layer layer)
    {
        // The test host has no tile data, so an item has no layer of its own (D27): set it, as P231's armor test does.
        item.Layer = layer;
        m.AddItem(item);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void CombatGeographyMajorFromAKillThatNeverReachedYou()
    {
        var p = NewPlayer();
        var before = Listed(p);

        // An orc that hit you first does not count; a mage never does.
        var hitter = Placed(new Orc(), new Point3D(Spot.X + 2, Spot.Y, Spot.Z));
        p.Pm.Damage(1, hitter);
        hitter.Damage(hitter.Hits + 50, p.Pm);
        Assert.False(hitter.Alive);
        var mage = Placed(new OrcishMage(), new Point3D(Spot.X + 3, Spot.Y, Spot.Z));
        mage.Damage(mage.Hits + 50, p.Pm);
        Assert.Equal(0, p.Count(TriggerKind.NeverReachedKills));
        Assert.False(p.Has(A.GeographyMajorKey));

        var orc = Placed(new Orc(), new Point3D(Spot.X + 4, Spot.Y, Spot.Z));
        orc.Damage(orc.Hits + 50, p.Pm);
        Assert.Equal(1, p.Count(TriggerKind.NeverReachedKills));

        AssertHiddenThenEarned(p, A.GeographyMajorKey, before, () =>
        {
            var next = Placed(new Orc(), new Point3D(Spot.X + 5, Spot.Y, Spot.Z));
            next.Damage(next.Hits + 50, p.Pm);
            Assert.Equal(2, p.Count(TriggerKind.NeverReachedKills));
        });
        Assert.False(p.Has(A.RockEnthusiastKey));
    }

    [Fact]
    public void ACreatureThatHitYouStillReachedYouAfterAHealToFull()
    {
        var p = NewPlayer();

        var orc = Placed(new Orc(), new Point3D(Spot.X + 2, Spot.Y + 2, Spot.Z));
        p.Pm.Damage(5, orc);

        // Healed to full: pinned clears the player's damage list (Mobile.cs:2043); the creature still reached you.
        p.Pm.Hits = p.Pm.HitsMax;
        Assert.Null(p.Pm.FindDamageEntryFor(orc));

        orc.Damage(orc.Hits + 50, p.Pm);
        Assert.False(orc.Alive);
        Assert.Equal(0, p.Count(TriggerKind.NeverReachedKills));
        Assert.False(N.HasReached(orc, p.Pm)); // gone: forgotten

        // A creature that never hit you still counts.
        var other = Placed(new Orc(), new Point3D(Spot.X + 3, Spot.Y + 2, Spot.Z));
        other.Damage(other.Hits + 50, p.Pm);
        Assert.Equal(1, p.Count(TriggerKind.NeverReachedKills));

        // A creature deleted without dying is forgotten too.
        var gone = Placed(new Orc(), new Point3D(Spot.X + 4, Spot.Y + 2, Spot.Z));
        p.Pm.Damage(1, gone);
        var reached = N.ReachedCount;
        gone.Delete();
        Assert.Equal(reached - 1, N.ReachedCount);
    }

    [Fact]
    public void AReleasedPetThatIsGoneOrPastTheWindowIsForgotten()
    {
        var p = NewPlayer();

        // Deleted after release: forgotten at once.
        var cat = Placed(new Cat(), new Point3D(Spot.X + 2, Spot.Y + 40, Spot.Z));
        Assert.True(cat.SetControlMaster(p.Pm));
        N.Released(cat, p.Pm);
        Assert.True(N.RemembersRelease(cat));
        cat.Delete();
        Assert.False(N.RemembersRelease(cat));

        // Past the retame window: dropped when the next release comes in.
        var dog = Placed(new Dog(), new Point3D(Spot.X + 3, Spot.Y + 40, Spot.Z));
        Assert.True(dog.SetControlMaster(p.Pm));
        N.Released(dog, p.Pm);
        ShardTestClock.AdvanceSeconds((int)N.RetameWindow.TotalSeconds + 60);
        var rabbit = Placed(new Rabbit(), new Point3D(Spot.X + 4, Spot.Y + 40, Spot.Z));
        Assert.True(rabbit.SetControlMaster(p.Pm));
        N.Released(rabbit, p.Pm);
        Assert.False(N.RemembersRelease(dog));
        Assert.True(N.RemembersRelease(rabbit));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BossesLastHitLarryAndBareMinimumFromOneKillingBlow()
    {
        var at = new Point3D(Spot.X + 20, Spot.Y, Spot.Z);
        var larry = NewPlayer(at);
        var worker = NewPlayer(at);
        var before = Listed(larry);

        Dress(larry.Pm, new PlateChest(), Layer.InnerTorso);
        Assert.True(N.IsArmored(larry.Pm));
        Assert.True(N.IsBarefoot(larry.Pm));

        var boss = Placed(new Barracoon(), at);
        Assert.True(ClusterFLootBoxes.IsBoss(boss));
        boss.Hits = 1001;
        boss.Damage(1000, worker.Pm);
        Assert.True(boss.Alive);
        boss.Damage(5, larry.Pm); // 5 of 1,005: under 1%
        Assert.False(boss.Alive);
        Assert.Same(larry.Pm, boss.LastKiller);

        Assert.True(larry.Has(A.BareMinimumKey));
        Assert.Contains(NotedRewards.LarryTitle, larry.Data.LeagueTitles.Owned);
        Assert.Equal("Larry", LeagueTitles.Display(NotedRewards.LarryTitle));

        // The worker, shod, doing all of a boss's damage: neither.
        Dress(worker.Pm, new Boots(), Layer.Shoes);
        Dress(worker.Pm, new PlateChest(), Layer.InnerTorso);
        var second = Placed(new Barracoon(), at);
        second.Damage(second.Hits + 50, worker.Pm);
        Assert.False(worker.Has(A.LastHitLarryKey));
        Assert.False(worker.Has(A.BareMinimumKey));

        AssertHiddenThenEarned(larry, A.LastHitLarryKey, before, () =>
        {
            var third = Placed(new Barracoon(), at);
            third.Hits = 1001;
            third.Damage(1000, worker.Pm);
            third.Damage(5, larry.Pm);
            Assert.False(third.Alive);
        });
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void PetsAndProvocation()
    {
        var p = NewPlayer();
        var before = Listed(p);

        // Not My Problem: your provoked orc kills another, and you never touched it.
        var provoked = Placed(new Orc(), new Point3D(Spot.X + 1, Spot.Y + 30, Spot.Z));
        provoked.BardProvoked = true;
        provoked.BardMaster = p.Pm;
        var victim = Placed(new Orc(), new Point3D(Spot.X + 2, Spot.Y + 30, Spot.Z));
        victim.Damage(victim.Hits + 50, provoked);
        Assert.True(p.Has(A.NotMyProblemKey));

        // ...but not when you hit it too.
        var touched = Placed(new Orc(), new Point3D(Spot.X + 3, Spot.Y + 30, Spot.Z));
        touched.Damage(1, p.Pm);
        touched.Damage(touched.Hits + 50, provoked);
        Assert.Equal(1, p.Count(TriggerKind.ProvokedKills));

        // Acceptable Losses: a pet dies while you are at full health; hurt, it does not count.
        p.Pm.Hits = p.Pm.HitsMax;
        var pet = Placed(new Horse(), new Point3D(Spot.X + 1, Spot.Y + 31, Spot.Z));
        Assert.True(pet.SetControlMaster(p.Pm));
        pet.Kill();
        Assert.True(p.Has(A.AcceptableLossesKey));
        p.Pm.Hits = p.Pm.HitsMax - 5;
        var pet2 = Placed(new Horse(), new Point3D(Spot.X + 2, Spot.Y + 31, Spot.Z));
        Assert.True(pet2.SetControlMaster(p.Pm));
        pet2.Kill();
        Assert.Equal(1, p.Count(TriggerKind.PetDiedAtFullHp));

        // Remote Work: your pet's kill while you are hidden; in sight and near, nothing.
        var dog = Placed(new Dog(), new Point3D(Spot.X + 2, Spot.Y, Spot.Z));
        Assert.True(dog.SetControlMaster(p.Pm));
        var prey = Placed(new Rabbit(), new Point3D(Spot.X + 3, Spot.Y, Spot.Z));
        prey.Damage(prey.Hits + 50, dog);
        Assert.False(p.Has(A.RemoteWorkKey));
        p.Pm.Hidden = true;
        var prey2 = Placed(new Rabbit(), new Point3D(Spot.X + 3, Spot.Y + 1, Spot.Z));
        prey2.Damage(prey2.Hits + 50, dog);
        Assert.True(p.Has(A.RemoteWorkKey));
        p.Pm.Hidden = false;

        // Commitment Issues: released and tamed back within five minutes; after six, not.
        var cat = Placed(new Cat(), new Point3D(Spot.X + 7, Spot.Y + 31, Spot.Z));
        Assert.True(cat.SetControlMaster(p.Pm));
        N.Released(cat, cat.ControlMaster);
        ShardTestClock.AdvanceSeconds(60);
        PetTrainingHooks.OnTamed(cat, p.Pm);
        Assert.True(p.Has(A.CommitmentIssuesKey));
        N.Released(cat, p.Pm);
        ShardTestClock.AdvanceSeconds(6 * 60);
        PetTrainingHooks.OnTamed(cat, p.Pm);
        Assert.Equal(1, p.Count(TriggerKind.RetameAfterRelease));

        AssertHiddenThenEarned(p, A.NotMyProblemKey, before, () =>
        {
            // Provoked again: the clock has moved on past the first provocation.
            provoked.BardProvoked = true;
            provoked.BardMaster = p.Pm;
            var again = Placed(new Orc(), new Point3D(Spot.X + 4, Spot.Y + 30, Spot.Z));
            again.Damage(again.Hits + 50, provoked);
            Assert.Equal(2, p.Count(TriggerKind.ProvokedKills));
        });
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TravelGatesRunebooksAndSteps()
    {
        var p = NewPlayer(new Point3D(Spot.X, Spot.Y + 60, Spot.Z));
        var before = Listed(p);
        var there = new Point3D(Spot.X + 40, Spot.Y + 60, Spot.Z);

        // Through a moongate at full health: no retreat. Under a tenth, in a fight: Tactical Retreat.
        var gate = Thing(new Moongate(there, Map.Trammel));
        gate.MoveToWorld(p.Pm.Location, Map.Trammel);
        gate.UseGate(p.Pm);
        Assert.Equal(there, p.Pm.Location);
        Assert.Equal(0, p.Count(TriggerKind.LowHpRetreats));

        var orc = Placed(new Orc(), new Point3D(there.X + 1, there.Y, there.Z));
        p.Pm.Combatant = orc;
        p.Pm.Hits = p.Pm.HitsMax / 20;
        var back = Thing(new Moongate(new Point3D(Spot.X, Spot.Y + 60, Spot.Z), Map.Trammel));
        back.UseGate(p.Pm);
        Assert.True(p.Has(A.TacticalRetreatKey));

        // Dying within ten seconds of that arrival: Wrong Turn.
        ShardTestClock.AdvanceSeconds(3);
        p.Pm.Kill();
        Assert.True(p.Has(A.WrongTurnKey));
        p.Pm.Resurrect();
        p.Pm.Hits = p.Pm.HitsMax;
        p.Pm.Combatant = null;

        // Ten recalls inside a minute (the hook the recall patch calls after the move): Any% Speedrun, at the tenth.
        for (var i = 1; i <= 10; i++)
        {
            N.Arrived(p.Pm, false);
            Assert.Equal(i == 10, p.Has(A.AnyPercentKey));
            ShardTestClock.AdvanceSeconds(5);
        }

        // A runebook filled by its sixteenth rune: Rune Hoarder; the same book filled again does not count twice.
        var book = new Runebook();
        p.Pm.Backpack.DropItem(book);
        RecallRune Rune()
        {
            var rune = new RecallRune();
            rune.Mark(p.Pm);
            p.Pm.Backpack.DropItem(rune);
            return rune;
        }

        for (var i = 1; i <= 16; i++)
        {
            Assert.True(book.OnDragDrop(p.Pm, Rune()));
            Assert.Equal(i == 16, p.Has(A.RuneHoarderKey));
        }

        book.Entries.RemoveAt(0);
        Assert.True(book.OnDragDrop(p.Pm, Rune()));
        Assert.Equal(1, p.Count(TriggerKind.RunebooksFilled));

        // Steps: overweight, then barefoot on lava in armor, then pantsless in boots. A turn in place is not a step.
        var anvil = new Item(0xFAF) { Weight = 2000 };
        p.Pm.Backpack.DropItem(anvil);
        p.Pm.Direction = Direction.North;
        N.OnMovement(new MovementEventArgs(p.Pm, Direction.East));
        Assert.Equal(0, p.Count(TriggerKind.OverweightSteps));
        for (var i = 0; i < 100; i++)
        {
            N.Stepped(p.Pm, N.Ground.Other);
        }

        Assert.True(p.Has(A.AmbitionKey));
        anvil.Delete();

        Dress(p.Pm, new PlateChest(), Layer.InnerTorso);
        for (var i = 0; i < 25; i++)
        {
            N.Stepped(p.Pm, N.Ground.Lava);
        }

        Assert.True(p.Has(A.HotFootKey));
        Assert.Equal(0, p.Count(TriggerKind.PantslessTiles));

        Dress(p.Pm, new Boots(), Layer.Shoes);
        N.Stepped(p.Pm, N.Ground.Lava);
        Assert.Equal(25, p.Count(TriggerKind.HotFootTiles));
        Assert.Equal(1, p.Count(TriggerKind.PantslessTiles));
        Dress(p.Pm, new PlateLegs(), Layer.Pants);
        N.Stepped(p.Pm, N.Ground.Other);
        Assert.Equal(1, p.Count(TriggerKind.PantslessTiles));

        AssertHiddenThenEarned(p, A.TacticalRetreatKey, before, () =>
        {
            p.Pm.Combatant = orc;
            p.Pm.Hits = 1;
            gate.UseGate(p.Pm);
            Assert.Equal(2, p.Count(TriggerKind.LowHpRetreats));
        });
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void CraftingAndMining()
    {
        var p = NewPlayer(new Point3D(Spot.X, Spot.Y + 90, Spot.Z));
        var before = Listed(p);

        // Skill Issue: ten failures in a row of the same item. A success, or another item, starts the streak over.
        for (var i = 0; i < 9; i++)
        {
            N.CraftAttempt(p.Pm, typeof(Longsword), true);
        }

        N.CraftAttempt(p.Pm, typeof(Longsword), false);
        N.CraftAttempt(p.Pm, typeof(Dagger), true);
        for (var i = 0; i < 9; i++)
        {
            N.CraftAttempt(p.Pm, typeof(Longsword), true);
        }

        Assert.False(p.Has(A.SkillIssueKey));
        N.CraftAttempt(p.Pm, typeof(Longsword), true);
        Assert.True(p.Has(A.SkillIssueKey));
        Assert.False(p.Has(A.PersistenceKey));

        // Recycling: a fresh crafted item counts, an old one or a bought one does not; an exceptional one under ten
        // seconds old is Quality Control.
        var bought = new PlateGorget();
        N.Recycled(p.Pm, bought);
        var old = new PlateGorget { PlayerConstructed = true, Created = Core.Now - TimeSpan.FromMinutes(2) };
        N.Recycled(p.Pm, old);
        Assert.Equal(0, p.Count(TriggerKind.FreshRecycles));

        var fresh = new PlateGorget { PlayerConstructed = true, Created = Core.Now - TimeSpan.FromSeconds(30) };
        N.Recycled(p.Pm, fresh);
        Assert.True(p.Has(A.BuiltToBeRecycledKey));
        Assert.False(p.Has(A.QualityControlKey));

        var gem = new PlateGorget { PlayerConstructed = true, Quality = ArmorQuality.Exceptional, Created = Core.Now - TimeSpan.FromSeconds(5) };
        N.Recycled(p.Pm, gem);
        Assert.True(p.Has(A.QualityControlKey));
        foreach (var i in new Item[] { bought, old, fresh, gem })
        {
            i.Delete();
        }

        // Rock Bottom: twenty swings at an emptied vein, through Mining's own resource check; the follow-up checks
        // inside a swing (timed) are not swings, and another tile starts the count over.
        var mining = Mining.System;
        var def = mining.OreAndStone;
        var vein = new Point3D(Spot.X + 10, Spot.Y + 90, Spot.Z);
        var bank = def.GetBank(Map.Trammel, vein.X, vein.Y);
        bank.Consume(bank.Current, p.Pm);
        Assert.False(mining.CheckResources(p.Pm, null, def, Map.Trammel, vein, true));
        for (var i = 1; i <= 19; i++)
        {
            Assert.False(mining.CheckResources(p.Pm, null, def, Map.Trammel, vein, false));
        }

        Assert.False(p.Has(A.RockBottomKey));
        Assert.False(mining.CheckResources(p.Pm, null, def, Map.Trammel, vein, false));
        Assert.True(p.Has(A.RockBottomKey));

        AssertHiddenThenEarned(p, A.SkillIssueKey, before, () =>
        {
            for (var i = 0; i < 10; i++)
            {
                N.CraftAttempt(p.Pm, typeof(Dagger), true);
            }
        });
    }

    // ---------------------------------------------------------------- 6

    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public int Secures { get; set; } = 425;

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => Secures;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    private static LootBox Dented(Player p)
    {
        var found = new List<LootBox>();
        foreach (var b in p.Pm.Backpack.FindItemsByType<LootBox>())
        {
            if (b.BoxName == LootBox.DentedBoxName)
            {
                found.Add(b);
            }
        }

        return Assert.Single(found);
    }

    private static LootBox BoxFor(Player p)
    {
        var box = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Miner, 1, null, "a test", null);
        p.Pm.Backpack.DropItem(box);
        return box;
    }

    [Fact]
    public void HousingDeathsStorageAndTheSameTile()
    {
        var at = new Point3D(Spot.X + 60, Spot.Y + 120, Spot.Z);
        var p = NewPlayer(at);
        var before = Listed(p);
        var house = Thing(new TestHouse(p.Pm));
        house.MoveToWorld(at, Map.Trammel);
        Assert.Same(house, BaseHouse.FindHouseAt(p.Pm));

        // Home Sweet Grave: a death in your own house.
        p.Pm.Kill();
        Assert.True(p.Has(A.HomeSweetGraveKey));
        p.Pm.Resurrect();

        // Hoarder: storage at 100%, noticed on a step (every tenth); with room left, nothing.
        for (var i = 0; i < 10; i++)
        {
            N.Stepped(p.Pm, N.Ground.Other);
        }

        Assert.False(p.Has(A.HoarderKey));
        house.Secures = 0;
        Assert.True(N.IsStorageFull(house));
        for (var i = 0; i < 10; i++)
        {
            N.Stepped(p.Pm, N.Ground.Other);
        }

        Assert.True(p.Has(A.HoarderKey));

        // Superstitious: five boxes in a row from one tile of your house; a step aside starts over.
        for (var i = 1; i <= 4; i++)
        {
            BoxFor(p).OnDoubleClick(p.Pm);
        }

        p.Pm.MoveToWorld(new Point3D(at.X + 1, at.Y, at.Z), Map.Trammel);
        BoxFor(p).OnDoubleClick(p.Pm);
        Assert.False(p.Has(A.SuperstitiousKey));
        for (var i = 2; i <= 5; i++)
        {
            BoxFor(p).OnDoubleClick(p.Pm);
        }

        Assert.True(p.Has(A.SuperstitiousKey));

        // Lucky Spot at 25: its earn box is a Dented Box with a Lucky Rug inside, bound to the account.
        for (var i = 6; i <= 25; i++)
        {
            BoxFor(p).OnDoubleClick(p.Pm);
        }

        ClusterFFanfare.FlushAll();
        Assert.True(p.Has(A.LuckySpotKey));
        var dented = Dented(p);
        var rug = dented.Contents.OfType<LuckyRug>().Single();
        Assert.Equal(p.Account.Username, rug.BoundAccount);
        Assert.Equal(1, dented.Tier);

        AssertHiddenThenEarned(p, A.HomeSweetGraveKey, before, () =>
        {
            p.Pm.Kill();
            p.Pm.Resurrect();
            Assert.Equal(2, p.Count(TriggerKind.DiedAtHome));
        });
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void BoxesCantWaitEdgeLordAndUnboxingVideo()
    {
        var wild = new Point3D(1300, 1300, 0);
        var p = NewPlayer(wild);
        var before = Listed(p);

        // Can't Wait: ten refusals in the wild. Its earn box is a Dented Box holding a bound Box-Shaped Box.
        var box = BoxFor(p);
        for (var i = 1; i <= 10; i++)
        {
            box.OnDoubleClick(p.Pm);
            Assert.Equal(i == 10, p.Has(A.CantWaitKey));
        }

        ClusterFFanfare.FlushAll();
        var dented = Dented(p);
        Assert.Equal("Dented Box", dented.DefaultName);
        var joke = dented.Contents.OfType<BoxShapedBox>().Single();
        Assert.Equal(p.Account.Username, joke.BoundAccount);
        Assert.True(dented.IsRareSlot(dented.Contents.Count - 1));

        var region = new GuardedRegion("p263 town", Map.Trammel, 50, new Rectangle2D(1390, 1390, 20, 20));
        region.Register();
        try
        {
            // In the middle of town, alone: neither. A step from the guards' edge: Edge Lord.
            p.Pm.MoveToWorld(new Point3D(1400, 1400, 0), Map.Trammel);
            BoxFor(p).OnDoubleClick(p.Pm);
            Assert.False(p.Has(A.EdgeLordKey));
            Assert.False(p.Has(A.UnboxingVideoKey));

            p.Pm.MoveToWorld(new Point3D(1409, 1400, 0), Map.Trammel);
            Assert.True(N.IsAtGuardEdge(p.Pm));
            BoxFor(p).OnDoubleClick(p.Pm);
            Assert.True(p.Has(A.EdgeLordKey));

            // Another player within three tiles: Unboxing Video.
            p.Pm.MoveToWorld(new Point3D(1400, 1400, 0), Map.Trammel);
            NewPlayer(new Point3D(1402, 1401, 0));
            BoxFor(p).OnDoubleClick(p.Pm);
            Assert.True(p.Has(A.UnboxingVideoKey));
        }
        finally
        {
            region.Unregister();
        }

        AssertHiddenThenEarned(p, A.CantWaitKey, before, () =>
        {
            p.Pm.MoveToWorld(wild, Map.Trammel);
            BoxFor(p).OnDoubleClick(p.Pm);
            Assert.Equal(11, p.Count(TriggerKind.BoxTooSoon));
        });
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void CommissionsPreCraftedStoreBoughtAndTheAbandonCooldown()
    {
        var p = NewPlayer(new Point3D(Spot.X, Spot.Y + 150, Spot.Z));
        var before = Listed(p);
        p.Pm.Skills.Blacksmith.Base = 50;
        var guild = p.Data.GetOrCreateGuildData(p.Pm.Serial);

        var c = SmithCommissionSystem.Generate(p.Pm);
        Assert.NotNull(c);
        Assert.NotNull(c.ItemType);
        var bought = (Item)Activator.CreateInstance(c.ItemType)!;
        SetResource(bought, c.Material, c.RequireExceptional, crafted: false);
        Assert.False(SmithCommissionSystem.IsMatch(c, bought), "a store-bought item matched a commission");

        var made = (Item)Activator.CreateInstance(c.ItemType)!;
        SetResource(made, c.Material, c.RequireExceptional, crafted: true);
        Assert.True(SmithCommissionSystem.IsMatch(c, made));
        p.Pm.Backpack.DropItem(made);
        ShardTestClock.AdvanceSeconds(20);
        SmithCommissionSystem.Complete(p.Pm, c, made);
        Assert.True(p.Has(A.PreCraftedKey));
        bought.Delete();

        // A commission turned in after a minute is not Pre-Crafted (counted by its kind's counter).
        var slow = SmithCommissionSystem.Generate(p.Pm)!;
        var slowItem = (Item)Activator.CreateInstance(slow.ItemType!)!;
        SetResource(slowItem, slow.Material, slow.RequireExceptional, crafted: true);
        p.Pm.Backpack.DropItem(slowItem);
        ShardTestClock.AdvanceSeconds(90);
        SmithCommissionSystem.Complete(p.Pm, slow, slowItem);
        Assert.Equal(1, p.Count(TriggerKind.QuickCommission));

        // A small deed refuses a bought item and takes a crafted one.
        var deed = Thing(new SmallSmithBOD(0, 10, typeof(Longsword), 1025049, 0x0F61, false, BulkMaterialType.None));
        var shopSword = new Longsword();
        p.Pm.Backpack.DropItem(shopSword);
        deed.EndCombine(p.Pm, shopSword);
        Assert.Equal(0, deed.AmountCur);
        Assert.False(shopSword.Deleted);
        var smithSword = new Longsword { PlayerConstructed = true };
        p.Pm.Backpack.DropItem(smithSword);
        deed.EndCombine(p.Pm, smithSword);
        Assert.Equal(1, deed.AmountCur);
        shopSword.Delete();

        // Reroll Goblin: an abandon holds back the next request for the cooldown.
        guild.SmithCommissions.Clear();
        SmithCommissionSystem.Abandoned(p.Pm);
        Assert.Null(SmithCommissionSystem.Generate(p.Pm));
        Assert.True(SmithCommissionSystem.RequestCooldownLeft(p.Pm) > TimeSpan.Zero);
        ShardTestClock.AdvanceSeconds((int)SmithCommissionSystem.AbandonCooldown.TotalSeconds + 1);
        Assert.NotNull(SmithCommissionSystem.Generate(p.Pm));

        AssertHiddenThenEarned(p, A.PreCraftedKey, before, () =>
        {
            guild.SmithCommissions.Clear();
            var again = SmithCommissionSystem.Generate(p.Pm)!;
            var item = (Item)Activator.CreateInstance(again.ItemType!)!;
            SetResource(item, again.Material, again.RequireExceptional, crafted: true);
            p.Pm.Backpack.DropItem(item);
            SmithCommissionSystem.Complete(p.Pm, again, item);
            Assert.Equal(2, p.Count(TriggerKind.QuickCommission));
        });
    }

    private static void SetResource(Item item, CraftResource material, bool exceptional, bool crafted)
    {
        switch (item)
        {
            case BaseWeapon w:
                w.Resource = material;
                w.PlayerConstructed = crafted;
                if (exceptional) w.Quality = WeaponQuality.Exceptional;
                break;
            case BaseArmor a:
                a.Resource = material;
                a.PlayerConstructed = crafted;
                if (exceptional) a.Quality = ArmorQuality.Exceptional;
                break;
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void DataOnlyAchievementsAreHiddenAndNotedTitlesAreSecretUntilHeld()
    {
        foreach (var key in new[] { A.MiddleManagementKey, A.DejaVuKey, A.ProcrastinatorKey, A.ColdFeetKey, A.InfamousKey })
        {
            var def = A.Definitions[key];
            Assert.True(def.Hidden, key);
            Assert.StartsWith("(Earned: ", def.EarnedLine);
        }

        // Every Noted achievement is hidden, in Mischief, with a flavor line; none duplicates another's title.
        var noted = A.Definitions.Values.Where(d => d.Key.StartsWith("noted.", StringComparison.Ordinal)).ToList();
        Assert.Equal(50, noted.Count); // cc-P263's 45 and cc-P262's 5 accessory ones (Accessories/AccessoryAchievements.cs)
        Assert.All(noted, d =>
        {
            Assert.True(d.Hidden, d.Key);
            Assert.Equal(AchievementCategory.Mischief, d.Category);
            Assert.False(string.IsNullOrWhiteSpace(d.FlavorText), d.Key);
        });
        Assert.All(noted, d => Assert.Single(A.Definitions.Values, o => o.Title == d.Title));

        // Every reward row names a real achievement, and every joke item is bound and statless once personalized.
        foreach (var r in NotedRewards.Table)
        {
            Assert.True(A.Definitions.ContainsKey(r.AchievementKey), r.AchievementKey);
        }

        var p = NewPlayer();
        foreach (var type in NotedRewards.Table.Select(r => r.JokeItem).OfType<Type>())
        {
            var item = Thing((Item)Activator.CreateInstance(type)!);
            Assert.IsAssignableFrom<INotedJoke>(item);
            ((INotedJoke)item).Personalize(p.Pm);
            Assert.Equal(LootType.Blessed, item.LootType);
            if (item is BaseJewel j) Assert.True(j.Attributes.IsEmpty, type.Name);
            if (item is BaseClothing cl) Assert.True(cl.Attributes.IsEmpty, type.Name);
        }

        // The title picker leaves a Noted title out until it is held.
        string Picker() => string.Join(" ", new LeagueTitleGump(p.Pm).Entries.OfType<GumpLabel>().Select(l => l.Text));
        Assert.DoesNotContain("Larry", Picker());
        p.Data.LeagueTitles.Owned.Add(NotedRewards.LarryTitle);
        Assert.Contains("Larry", Picker());
    }
}
