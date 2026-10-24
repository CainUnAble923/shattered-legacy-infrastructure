// RewardMountVerification.cs
//
// cc-P365: the reward mounts (server/customizations/Mobiles/Mounts/RewardMounts.cs, RewardMountStatuette.cs) and the evo
// lines' new stages (Mobiles/Evo/EvoPetData.cs). Notes in shard-migration notes/cc-P365-reward-mounts-and-evo-stages.md.
//
// Facts:
//   1. Each of the eighteen mounts: its statuette calls it bonded and following (one follower slot); it walks on its body
//      (an Animal to the server), its owner rides it (the mount item of the package goes on the Mount layer, the mount
//      leaves the map) and dismounts (back where the rider stands); an animal trainer stables it (pinned EndStable);
//      it round-trips the generated serializer; and it shrinks and comes back (ShrunkPet).
//   2. Each evo stage change shows the stage's body: every body of the horse and lizard lines, and the dog's top stage
//      (the KR Wolf by default, the windrunner as a pick).
//   3. The routes: six League shop rows at 1750 AP handing over statuettes, four achievements, and the Tamer and Boss
//      boxes' mounts (none below their tiers, none above the chance).

#nullable enable

using System;
using System.Linq;
using Server;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RewardMountVerification
{
    private static readonly Point3D Here = new(1030, 1030, 0);

    private readonly ITestOutputHelper _out;

    public RewardMountVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardBodyTypes.Apply();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    public static TheoryData<string> Keys()
    {
        var data = new TheoryData<string>();
        foreach (var d in RewardMounts.Defs)
        {
            data.Add(d.Key);
        }

        return data;
    }

    private static PlayerMobile Owner()
    {
        var pm = new PlayerMobile { Player = true, Body = 400, FollowersMax = 5 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Here, Map.Trammel);
        return pm;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [MemberData(nameof(Keys))]
    public void EachMountIsCalledMountedDismountedStabledAndShrunk(string key)
    {
        var def = RewardMounts.Find(key)!;
        var pm = Owner();
        var trainer = new AnimalTrainer();
        BaseMount? mount = null;

        try
        {
            // The statuette calls it, bonded, following, one slot.
            var statuette = new RewardMountStatuette(key);
            pm.Backpack!.DropItem(statuette);
            Assert.Equal(def.MountItem, statuette.ItemID);
            Assert.False(statuette.CanEquip(pm));
            statuette.OnDoubleClick(pm);
            Assert.True(statuette.Deleted);
            mount = pm.AllFollowers.OfType<BaseMount>().Single();
            Assert.IsType(def.MountType, mount);
            Assert.Same(pm, mount.ControlMaster);
            Assert.True(mount.IsBonded);
            Assert.Equal(1, mount.ControlSlots);
            Assert.Equal(1, pm.Followers);
            Assert.False(mount.Tamable);
            Assert.Equal(def.Name, mount.Name ?? mount.DefaultName);

            // Its body and its mount item.
            Assert.Equal(def.Body, (int)mount.Body);
            // An animal, or (the riding drake) the stock drake's monster body; never a human or an empty body.
            Assert.True(mount.Body.IsAnimal || mount.Body.IsMonster, $"{key}: body {def.Body} is {mount.Body.Type}");
            Assert.Equal(def.MountItem, mount.ItemID);

            // Mounted: the mount item on the Mount layer, the mount off the map.
            mount.OnDoubleClick(pm);
            Assert.Same(pm, mount.Rider);
            Assert.Same(mount, pm.Mount);
            Assert.Equal(def.MountItem, pm.FindItemOnLayer(Layer.Mount)?.ItemID);
            Assert.Equal(Map.Internal, mount.Map);

            // Dismounted: back where the rider stands.
            BaseMount.Dismount(pm);
            Assert.Null(pm.Mount);
            Assert.Null(mount.Rider);
            Assert.Equal(Map.Trammel, mount.Map);
            Assert.Equal(pm.Location, mount.Location);

            // Shrunk and back.
            mount.ControlOrder = OrderType.Follow;
            var fig = ShrunkPet.TryShrink(pm, mount);
            Assert.NotNull(fig);
            Assert.Equal(0, pm.Followers);
            pm.Backpack.DropItem(fig!);
            fig!.OnDoubleClick(pm);
            Assert.True(fig.Deleted);
            Assert.Equal(Map.Trammel, mount.Map);
            Assert.Equal(1, pm.Followers);

            // Stabled by an animal trainer (pinned AnimalTrainer.EndStable, 30 gold).
            trainer.MoveToWorld(Here, Map.Trammel);
            pm.Backpack.DropItem(new Gold(30));
            trainer.EndStable(pm, mount);
            Assert.True(mount.IsStabled, $"{key} was not stabled");
            Assert.Contains(mount, pm.Stabled);
            Assert.Equal(Map.Internal, mount.Map);

            // Round trip (last: the copy shares the mount's serial).
            var copy = RoundTrip(mount);
            Assert.Equal(def.Body, (int)copy.Body);
            Assert.Equal(def.MountItem, copy.ItemID);
        }
        finally
        {
            if (mount != null)
            {
                pm.RemoveStabled(mount);
                mount.Delete();
            }

            trainer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EachEvoStageChangeShowsItsBody()
    {
        foreach (var line in new[] { EvoPetData.Horse, EvoPetData.Lizard })
        {
            var pet = new EvoPet(line.Key);
            try
            {
                Assert.Equal(line.Stage(1).Bodies[0], (int)pet.Body);
                foreach (var stage in line.Stages)
                {
                    foreach (var body in stage.Bodies)
                    {
                        pet.StaffSetStage(stage.Number, body);
                        Assert.Equal(body, (int)pet.Body);
                        Assert.True(pet.Body.IsAnimal, $"{line.Key} stage {stage.Number}: {body} is {pet.Body.Type}");
                    }
                }
            }
            finally
            {
                pet.Delete();
            }
        }

        // The dog's top stage: the KR Wolf by default, the windrunner still a pick.
        var dog = new EvoPet("dog");
        try
        {
            dog.StaffSetStage(5);
            Assert.Equal(2133, (int)dog.Body);
            dog.StaffSetStage(5, 1410);
            Assert.Equal(1410, (int)dog.Body);
        }
        finally
        {
            dog.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EveryMountHasItsRoute()
    {
        // League shop: six statuette rows at 1750 AP, past every League box's reach.
        var rows = LeagueShop.Stock.Where(r => r.Kind == LeagueShopKind.Mount).ToArray();
        Assert.Equal(RewardMounts.Defs.Where(d => d.Route == RewardMountRoute.LeagueShop).Select(d => $"mount.{d.Key}"), rows.Select(r => r.Key));
        Assert.Equal(6, rows.Length);
        var pm = Owner();
        try
        {
            foreach (var row in rows)
            {
                Assert.Equal(RewardMounts.ShopAp, row.ApRequired);
                Assert.True(row.ApRequired > LeagueShop.BoxInfo(LeagueShop.BoxTier.Gold).MaxAp);
                var item = Assert.Single(row.Create!(pm));
                var statuette = Assert.IsType<RewardMountStatuette>(item);
                Assert.Equal(row.Key, $"mount.{statuette.MountKey}");
                item.Delete();
            }

            // Achievements: each route names an achievement whose reward is that mount's statuette.
            foreach (var d in RewardMounts.Defs.Where(d => d.Route == RewardMountRoute.Achievement))
            {
                var ach = ClusterFAchievementSystem.Definitions[d.RouteDetail];
                var type = Assert.Single(ach.RewardItems);
                var made = (RewardMountStatuette)Activator.CreateInstance(type)!;
                Assert.Equal(d.Key, made.MountKey);
                made.Delete();
            }

            Assert.Equal(4, RewardMounts.Defs.Count(d => d.Route == RewardMountRoute.Achievement));

            // Loot boxes: Tamer tiers 5-6 and Boss tier 6, each from its own pool; nothing below the tier or above the chance.
            Assert.Null(RewardMounts.LootBoxMount(BoxTheme.Tamer, 4, 0.0, 0));
            Assert.Null(RewardMounts.LootBoxMount(BoxTheme.Boss, 5, 0.0, 0));
            Assert.Null(RewardMounts.LootBoxMount(BoxTheme.Miner, 6, 0.0, 0));
            Assert.Null(RewardMounts.LootBoxMount(BoxTheme.Tamer, 5, 0.99, 0));
            var tamer = RewardMounts.Defs.Where(d => d.RouteDetail == "Tamer box, tier 5-6").Select(d => d.Key).ToArray();
            var boss = RewardMounts.Defs.Where(d => d.RouteDetail == "Boss box, tier 6").Select(d => d.Key).ToArray();
            Assert.Equal(new[] { "riding-drake", "riding-bear", "riding-wolf", "saddled-horse" }, tamer);
            Assert.Equal(new[] { "marsh-raptor", "dusk-raptor", "sky-raptor", "bog-strider" }, boss);
            for (var pick = 0; pick < 8; pick++)
            {
                var t = RewardMounts.LootBoxMount(BoxTheme.Tamer, 6, 0.0, pick)!;
                Assert.Contains(t.MountKey, tamer);
                t.Delete();
                var b = RewardMounts.LootBoxMount(BoxTheme.Boss, 6, 0.0, pick)!;
                Assert.Contains(b.MountKey, boss);
                b.Delete();
            }

            Assert.Equal(18, RewardMounts.Defs.Select(d => d.Key).Distinct().Count());
            _out.WriteLine(string.Join("\n", RewardMounts.Defs.Select(d => $"{d.Name}: {d.Route} ({d.RouteDetail})")));
        }
        finally
        {
            pm.Delete();
        }
    }
}
