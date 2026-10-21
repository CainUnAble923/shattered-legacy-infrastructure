// JokeAchievementsVerification.cs
//
// cc-P231 (ours). The hidden joke achievements for OSI's easter-egg moments: customizations ClusterFJokeAchievements.cs,
// the hooks in server/patches/*-achievement.patch and our Meditation.cs. Notes in shard-migration
// notes/cc-P231-osi-joke-achievements.md.
//
// Each fact drives the game's own path (the scissors target, the begging timer, BandageContext.BeginHeal, a real craft
// that wears a tool out, the speech event with the client's keyword, a guard's attack timer, an explosion potion), never
// the message text. Facts:
//   1 Run With Scissors: the running refusal counts, and the three tiers fire at exactly 1, 25 and 100; not running
//     counts nothing.
//   2 Not Trustworthy, from the begging timer with bad karma; good karma never.
//   3 Meditating in Plate (our Meditation, armor on); unarmored never.
//   4 Mister Creosote (eating while full) and Placebo (bandaging the unhurt); a hungry eater and a hurt patient never.
//   5 Planned Obsolescence: 100 sewing kits worn out by real crafts; 99 is not enough.
//   6 Soul Searching from the speech keyword, and Boo from a ghost speaking; the same words with no keyword, or alive,
//     never.
//   7 *hic*, from the drunk timer.
//   8 deaths: Instant Justice (a guard's instant kill), Fowl Play (a chicken, a rabbit), Self-Detonation (your own
//     explosion potion); another player's potion, or dying by your own hand otherwise, never.
//   9 Sole Survivor at 10 boots or shoes fished up, Sole Proprietor at 100; a fish never counts.
//  10 none fires from its message text alone: every message these hooks sit beside, sent to the player, earns nothing.
//  11 they are hidden: none is in the list until earned, then it is, marked [Secret], with its flavor and Earned lines.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.SkillHandlers;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class JokeAchievementsVerification : IDisposable
{
    private static readonly Point3D Spot = new(1480, 1750, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<Player> _players = [];
    private readonly List<IEntity> _things = [];

    public JokeAchievementsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        // The test host runs no Configure of ours (LeagueLadderVerification and others may have run this one).
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFJokeAchievements.Register();
    }

    public void Dispose()
    {
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
        public readonly Account Account = new($"p231{Guid.NewGuid():N}"[..16], "p231-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P231 {Guid.NewGuid():N}"[..12] };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public bool Has(string key) => ClusterFAchievementSystem.HasEarned(Account, key);

        public int Count(TriggerKind kind) =>
            ClusterFAchievementSystem.GetCounter(Account.Username, ClusterFAchievementSystem.JokeCounter(kind));

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAchievementSystem.ResetForAccount(Account.Username);
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

    private static readonly string[] AllJokeKeys =
    [
        ClusterFAchievementSystem.SafetyThirdKey, ClusterFAchievementSystem.RunWithScissorsKey,
        ClusterFAchievementSystem.UnsupervisedKey, ClusterFAchievementSystem.NotTrustworthyKey,
        ClusterFAchievementSystem.MeditatingInPlateKey, ClusterFAchievementSystem.MisterCreosoteKey,
        ClusterFAchievementSystem.PlaceboKey, ClusterFAchievementSystem.PlannedObsolescenceKey,
        ClusterFAchievementSystem.SoulSearchingKey, ClusterFAchievementSystem.BooKey, ClusterFAchievementSystem.HicKey,
        ClusterFAchievementSystem.InstantJusticeKey, ClusterFAchievementSystem.SelfDetonationKey,
        ClusterFAchievementSystem.SoleSurvivorKey, ClusterFAchievementSystem.SoleProprietorKey,
        ClusterFAchievementSystem.FowlPlayKey
    ];

    private static bool HasAnyJoke(Player p) => AllJokeKeys.Any(p.Has);

    // ---------------------------------------------------------------- 1

    // One use of the scissors on a gold coin (not scissorable), running or not, through Scissors' own target.
    private static void Snip(Player p, Scissors scissors, Item on, bool running)
    {
        p.Pm.Direction = running ? Direction.North | Direction.Running : Direction.North;
        p.Pm.LastMoveTime = Core.TickCount;
        scissors.OnDoubleClick(p.Pm);
        Assert.NotNull(p.Pm.Target);
        p.Pm.Target.Invoke(p.Pm, on);
    }

    [Fact]
    public void TheScissorsRefusalCountsAndItsThreeTiersFire()
    {
        var p = NewPlayer();
        var scissors = new Scissors();
        var coin = new Gold(1);
        p.Pm.Backpack.DropItem(scissors);
        p.Pm.Backpack.DropItem(coin);

        for (var i = 0; i < 50; i++)
        {
            Snip(p, scissors, coin, false);
        }

        Assert.Equal(0, p.Count(TriggerKind.ScissorsRunning));
        Assert.False(HasAnyJoke(p));

        var attempts = 0;
        while (p.Count(TriggerKind.ScissorsRunning) < 100 && attempts < 1000)
        {
            Snip(p, scissors, coin, true);
            attempts++;

            var n = p.Count(TriggerKind.ScissorsRunning);
            Assert.Equal(n >= 1, p.Has(ClusterFAchievementSystem.SafetyThirdKey));
            Assert.Equal(n >= 25, p.Has(ClusterFAchievementSystem.RunWithScissorsKey));
            Assert.Equal(n >= 100, p.Has(ClusterFAchievementSystem.UnsupervisedKey));
        }

        Assert.Equal(100, p.Count(TriggerKind.ScissorsRunning));
        Assert.True(p.Has(ClusterFAchievementSystem.UnsupervisedKey));
        _out.WriteLine($"100 refusals in {attempts} running attempts (pinned refuses 80% of them)");
    }

    // ---------------------------------------------------------------- 2

    private Banker Mark(Player beggar)
    {
        var banker = Thing(new Banker());
        if (banker.Backpack == null)
        {
            banker.AddItem(new Backpack());
        }

        banker.MoveToWorld(new Point3D(beggar.Pm.X + 1, beggar.Pm.Y, beggar.Pm.Z), Map.Trammel);
        return banker;
    }

    [Fact]
    public void BeggingWithBadKarmaIsRefusedAsUntrustworthy()
    {
        ShardTestClock.Arm();
        var bad = NewPlayer();
        var good = NewPlayer(new Point3D(Spot.X + 20, Spot.Y, Spot.Z));
        bad.Pm.Karma = -15000;  // pinned's bad-karma chance is then above 1: always refused (Begging.cs:100-106)
        good.Pm.Karma = 15000;  // never: the branch needs negative karma

        foreach (var p in new[] { bad, good })
        {
            var banker = Mark(p);
            Begging.OnUse(p.Pm);
            p.Pm.Target.Invoke(p.Pm, banker);
        }

        ShardTestClock.AdvanceSeconds(2.5);
        Assert.True(bad.Has(ClusterFAchievementSystem.NotTrustworthyKey));
        Assert.False(good.Has(ClusterFAchievementSystem.NotTrustworthyKey));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void MeditatingInArmorIsBlocked()
    {
        var plated = NewPlayer();
        var bare = NewPlayer(new Point3D(Spot.X + 20, Spot.Y, Spot.Z));

        // The test host has no tile data to give armor its layer (D27), so the layer is set.
        plated.Pm.AddItem(new PlateChest { Layer = Layer.InnerTorso });
        Assert.True(RegenRates.GetArmorOffset(plated.Pm) > 0);

        foreach (var p in new[] { plated, bare })
        {
            p.Pm.Skills.Meditation.Base = 50;
            p.Pm.RawInt = 50; // a mana pool to be short of (with none, Meditation says "You are at peace" first)
            p.Pm.Mana = 0;
            Meditation.OnUse(p.Pm);
        }

        Assert.True(plated.Has(ClusterFAchievementSystem.MeditatingInPlateKey));
        Assert.False(bare.Has(ClusterFAchievementSystem.MeditatingInPlateKey));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void EatingWhileFullAndBandagingTheUnhurt()
    {
        var full = NewPlayer();
        var hungry = NewPlayer(new Point3D(Spot.X + 20, Spot.Y, Spot.Z));
        full.Pm.Hunger = 20;
        hungry.Pm.Hunger = 5;

        foreach (var p in new[] { full, hungry })
        {
            var ham = new Ham();
            p.Pm.Backpack.DropItem(ham);
            ham.Eat(p.Pm);
        }

        Assert.True(full.Has(ClusterFAchievementSystem.MisterCreosoteKey));
        Assert.False(hungry.Has(ClusterFAchievementSystem.MisterCreosoteKey));

        var healer = NewPlayer(new Point3D(Spot.X + 40, Spot.Y, Spot.Z));
        var unhurt = NewPlayer(new Point3D(Spot.X + 41, Spot.Y, Spot.Z));
        unhurt.Pm.Hits = unhurt.Pm.HitsMax; // a new character starts below full
        BandageContext.BeginHeal(healer.Pm, unhurt.Pm);
        Assert.True(healer.Has(ClusterFAchievementSystem.PlaceboKey));

        var medic = NewPlayer(new Point3D(Spot.X + 60, Spot.Y, Spot.Z));
        var hurt = NewPlayer(new Point3D(Spot.X + 61, Spot.Y, Spot.Z));
        hurt.Pm.Hits = hurt.Pm.HitsMax / 2;
        BandageContext.BeginHeal(medic.Pm, hurt.Pm);
        Assert.False(medic.Has(ClusterFAchievementSystem.PlaceboKey));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AHundredToolsWornOutByRealCrafts()
    {
        var p = NewPlayer();
        p.Pm.Skills.Tailoring.Base = 100;
        var system = DefTailoring.CraftSystem;
        var item = system.CraftItems.SearchFor(typeof(BodySash));
        Assert.NotNull(item);

        var cloth = new Cloth(60000);
        p.Pm.Backpack.DropItem(cloth);

        for (var i = 1; i <= 100; i++)
        {
            var kit = new SewingKit(1);
            p.Pm.Backpack.DropItem(kit);
            item.CompleteCraft(1, false, p.Pm, system, typeof(Cloth), kit, null);

            Assert.True(kit.Deleted, $"craft {i}: the one-use kit was not worn out");
            Assert.Equal(i, p.Count(TriggerKind.ToolsWornOut));
            Assert.Equal(i >= 100, p.Has(ClusterFAchievementSystem.PlannedObsolescenceKey));
        }

        // A tool with uses left is not worn out.
        var sturdy = new SewingKit(50);
        p.Pm.Backpack.DropItem(sturdy);
        item.CompleteCraft(1, false, p.Pm, system, typeof(Cloth), sturdy, null);
        Assert.False(sturdy.Deleted);
        Assert.Equal(100, p.Count(TriggerKind.ToolsWornOut));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void SoulSearchingFromTheKeywordAndBooFromAGhost()
    {
        var p = NewPlayer();

        // The words with no keyword (typed into a client that sends none, or by a script) do nothing.
        p.Pm.DoSpeech("I must consider my sins", [], MessageType.Regular, 0x3B2);
        Assert.False(p.Has(ClusterFAchievementSystem.SoulSearchingKey));

        // The client sends keyword 0x0032 for the phrase, whatever the text.
        p.Pm.DoSpeech("i must consider my sins", [ClusterFJokeAchievements.ConsiderSinsKeyword], MessageType.Regular, 0x3B2);
        Assert.True(p.Has(ClusterFAchievementSystem.SoulSearchingKey));

        // Alive, speaking is nothing; dead, it is Boo.
        p.Pm.DoSpeech("hello", [], MessageType.Regular, 0x3B2);
        Assert.False(p.Has(ClusterFAchievementSystem.BooKey));

        p.Pm.Kill();
        Assert.False(p.Pm.Alive);
        p.Pm.DoSpeech("hello", [], MessageType.Regular, 0x3B2);
        Assert.True(p.Has(ClusterFAchievementSystem.BooKey));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void DrinkingUntilYouHiccup()
    {
        ShardTestClock.Arm();
        var drunk = NewPlayer();
        var sober = NewPlayer(new Point3D(Spot.X + 20, Spot.Y, Spot.Z));
        drunk.Pm.BAC = 40;
        BaseBeverage.CheckHeaveTimer(drunk.Pm);
        BaseBeverage.CheckHeaveTimer(sober.Pm);

        // The heave timer runs every 5 seconds and hiccups one time in four (Beverage.cs:831, :856).
        for (var s = 0; s < 600 && !drunk.Has(ClusterFAchievementSystem.HicKey); s += 5)
        {
            drunk.Pm.BAC = 40;
            ShardTestClock.AdvanceSeconds(5);
        }

        Assert.True(drunk.Has(ClusterFAchievementSystem.HicKey));
        Assert.False(sober.Has(ClusterFAchievementSystem.HicKey));
        drunk.Pm.BAC = 0;
        BaseBeverage.CheckHeaveTimer(drunk.Pm);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void DeathsByGuardsFowlAndYourOwnPotion()
    {
        ShardTestClock.Arm();

        // A guard's instant kill (BaseGuard.cs:310-321), switched on as the shard's config has it (the host runs no
        // Configure).
        var setter = typeof(BaseGuard).GetProperty(nameof(BaseGuard.GuardsInstantKill))!.GetSetMethod(true)!;
        var was = BaseGuard.GuardsInstantKill;
        setter.Invoke(null, [true]);
        try
        {
            var outlaw = NewPlayer();
            var guard = Thing(new WarriorGuard());
            guard.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            guard.Focus = outlaw.Pm;
            ShardTestClock.AdvanceSeconds(1);
            Assert.False(outlaw.Pm.Alive);
            Assert.True(outlaw.Has(ClusterFAchievementSystem.InstantJusticeKey));
            Assert.False(outlaw.Has(ClusterFAchievementSystem.FowlPlayKey));
        }
        finally
        {
            setter.Invoke(null, [was]);
        }

        // A chicken and a rabbit, each landing the killing blow; an orc does not count.
        foreach (var (killer, counts) in new (BaseCreature, bool)[] { (new Chicken(), true), (new Rabbit(), true), (new Orc(), false) })
        {
            var victim = NewPlayer(new Point3D(Spot.X + 10, Spot.Y + _players.Count, Spot.Z));
            Thing(killer).MoveToWorld(victim.Pm.Location, Map.Trammel);
            victim.Pm.Damage(victim.Pm.Hits + 10, killer);
            Assert.False(victim.Pm.Alive);
            Assert.Equal(counts, victim.Has(ClusterFAchievementSystem.FowlPlayKey));
            Assert.False(victim.Has(ClusterFAchievementSystem.InstantJusticeKey));
        }

        // Your own explosion potion; someone else's does not count, nor does killing yourself any other way.
        var bomber = NewPlayer(new Point3D(Spot.X + 30, Spot.Y, Spot.Z));
        bomber.Pm.Hits = 1;
        Thing(new ExplosionPotion()).Explode(bomber.Pm, true, bomber.Pm.Location, Map.Trammel);
        Assert.False(bomber.Pm.Alive);
        Assert.True(bomber.Has(ClusterFAchievementSystem.SelfDetonationKey));

        var thrower = NewPlayer(new Point3D(Spot.X + 50, Spot.Y, Spot.Z));
        var target = NewPlayer(new Point3D(Spot.X + 52, Spot.Y, Spot.Z));
        target.Pm.Hits = 1;
        thrower.Pm.Hits = thrower.Pm.HitsMax;
        Thing(new ExplosionPotion()).Explode(thrower.Pm, true, target.Pm.Location, Map.Trammel);
        Assert.False(target.Pm.Alive);
        Assert.False(target.Has(ClusterFAchievementSystem.SelfDetonationKey));

        var other = NewPlayer(new Point3D(Spot.X + 70, Spot.Y, Spot.Z));
        other.Pm.Damage(other.Pm.Hits + 10, other.Pm);
        Assert.False(other.Pm.Alive);
        Assert.False(other.Has(ClusterFAchievementSystem.SelfDetonationKey));
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void FishingUpFootwearCountsToTenAndAHundred()
    {
        var p = NewPlayer();
        var fishing = Fishing.System;
        Item[] catches = [new Boots(), new Shoes(), new Sandals(), new ThighBoots()];

        fishing.SendSuccessTo(p.Pm, Thing(new Fish()), null);
        Assert.Equal(0, p.Count(TriggerKind.FishedFootwear));

        for (var i = 1; i <= 100; i++)
        {
            fishing.SendSuccessTo(p.Pm, catches[i % catches.Length], null);
            Assert.Equal(i >= 10, p.Has(ClusterFAchievementSystem.SoleSurvivorKey));
            Assert.Equal(i >= 100, p.Has(ClusterFAchievementSystem.SoleProprietorKey));
        }

        foreach (var c in catches)
        {
            c.Delete();
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void NoneFiresFromItsMessageTextAlone()
    {
        var p = NewPlayer();
        int[] messages = [1063305, 500406, 500135, 500867, 500955, 500849, 1044038, 1008124];
        foreach (var number in messages)
        {
            p.Pm.SendLocalizedMessage(number);
            p.Pm.PublicOverheadMessage(MessageType.Regular, 0x3B2, number);
        }

        p.Pm.SendMessage("Didn't your parents ever tell you not to run with scissors in your hand?!");
        p.Pm.SendMessage("Thou dost not look trustworthy... no gold for thee today!");
        p.Pm.SendMessage("*hic*");

        // Nor from a player saying them (no keyword): only the game's own event counts.
        foreach (var line in new[] { "*hic*", "I must consider my sins", "run with scissors", "Thou dost not look trustworthy" })
        {
            p.Pm.DoSpeech(line, [], MessageType.Regular, 0x3B2);
        }

        Assert.False(HasAnyJoke(p));
        Assert.All(Enum.GetValues<TriggerKind>().Where(k => k >= TriggerKind.ScissorsRunning),
            k => Assert.Equal(0, p.Count(k)));
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void TheyAreHiddenUntilEarned()
    {
        var defs = AllJokeKeys.Select(k => ClusterFAchievementSystem.Definitions[k]).ToList();
        Assert.All(defs, d =>
        {
            Assert.True(d.Hidden, d.Key);
            Assert.Equal(AchievementCategory.Mischief, d.Category);
            Assert.False(string.IsNullOrWhiteSpace(d.FlavorText), d.Key);
            Assert.StartsWith("(Earned: ", d.EarnedLine);
        });
        Assert.Equal("(Earned: tried to use scissors while running)",
            ClusterFAchievementSystem.Definitions[ClusterFAchievementSystem.SafetyThirdKey].EarnedLine);
        Assert.Equal("Your parents tried. The scissors tried. Nobody can help you.",
            ClusterFAchievementSystem.Definitions[ClusterFAchievementSystem.SafetyThirdKey].FlavorText);

        var p = NewPlayer();
        string Listed() => string.Join(" ", Enumerable.Range(0, 30)
            .Select(i => new AchievementsGump(p.Pm, null, false, i))
            .SelectMany(g => g.Entries.OfType<GumpHtml>().Select(h => h.Text).Concat(g.Entries.OfType<GumpLabel>().Select(l => l.Text))));

        var before = Listed();
        Assert.All(defs, d => Assert.DoesNotContain(d.Title, before));

        // Earned through its real path, it is listed, marked secret, with both lines.
        var scissors = new Scissors();
        var coin = new Gold(1);
        p.Pm.Backpack.DropItem(scissors);
        p.Pm.Backpack.DropItem(coin);
        for (var i = 0; i < 200 && !p.Has(ClusterFAchievementSystem.SafetyThirdKey); i++)
        {
            Snip(p, scissors, coin, true);
        }

        var after = Listed();
        var safety = ClusterFAchievementSystem.Definitions[ClusterFAchievementSystem.SafetyThirdKey];
        Assert.Contains(safety.Title, after);
        Assert.Contains("[Secret]", after);
        Assert.Contains(safety.FlavorText, after);
        Assert.Contains(safety.EarnedLine, after);
        Assert.DoesNotContain(ClusterFAchievementSystem.Definitions[ClusterFAchievementSystem.RunWithScissorsKey].Title, after);
    }
}
