// cc-P794: seven earned ethereal mounts, ours (Chase 11-03; P790's rank 2): subclasses of pinned's EtherealMount, no core edit.
//
// IDs: the tigers, ancient hellhound, boura and lasher are ServUO stock Ethereals.cs:1080-1243, as pinned has none of them.
// Stock has no ethereal windrunner or peacock: their statuettes are the client's own (tiledata windrunner_south 0x9ED5,
// Bird_Peacock_Mount 0xA0C0) and their mount items are the client's (TazUO Game/Data/Mounts.cs: 0x3ECC is body 1410, 0x3ECF is
// body 1440). notes/cc-P794-tools/tile.py reads all seven out of the client data.
//
// As ServUO's, these are solid, not translucent: the setting's hue is 0, and pinned's MountMe only ever puts EtherealHue on.
// As pinned's, each costs one follower slot while ridden (ServUO stock too). Both are settings:
//   clusterf.etherealLooks.hue            what the mount wears while ridden; 0 is solid, 16385 (0x4001) is pinned's translucent
//   clusterf.etherealLooks.followerSlots  follower slots while ridden, 0 to 5
//
// They name themselves: the client's labels for the stock ones are newer than the client data we ship, and pinned's single
// click calls every ethereal a "Veteran Reward" (Ethereals.cs:232-237).
//
// How players earn them (Chase's choice, 11-04; never a plain store sale):
//   League shop (1750 AP)  peacock, windrunner, dark tiger, light tiger                       (ShopRows, in LeagueShop.Stock)
//   achievement            lasher, boura, ancient hellhound                                   (Achieved, ClusterFAchievements)
// The prices are settings: clusterf.etherealLooks.shopAp and clusterf.etherealLooks.renown.<key>. Kind Mount keeps every row
// out of the loot boxes (LeagueShop.BoxPool).
//
// An achievement's reward is otherwise paid only at the moment of earning, to a character online then. These three are paid by
// PayOwed instead: at that moment and at every login, once per account, marked in the account's saved achievement counters. So
// an account that earned one before this existed is paid at its next login, and an existing world needs no seeding.
// After the idea of Neshoba's, Shazzy's and Yvan Major's ethereal mounts (reference only, D:\UO\third-party\code).

using System;
using System.Linq;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Items;

namespace Server.Mobiles;

public static class EtherealLooks
{
    public const int SolidHue = 0;
    public const int DefaultFollowerSlots = 1;

    public static int Hue { get; set; } = SolidHue;
    public static int FollowerSlots { get; set; } = DefaultFollowerSlots;

    /// <summary>Every look: its class, statuette and mount item.</summary>
    public static readonly (Type Type, string Name, int Statuette, int MountItem)[] All =
    [
        (typeof(EtherealWindrunner), "an ethereal windrunner statuette", 0x9ED5, 0x3ECC),
        (typeof(EtherealLasher), "an ethereal lasher statuette", 0x9E35, 0x3ECB),
        (typeof(EtherealTiger), "an ethereal tiger statuette", 0x9844, 0x3EC8),
        (typeof(EtherealLightTiger), "an ethereal light tiger statuette", 0x9844, 0x3EC7),
        (typeof(EtherealAncientHellHound), "an ethereal ancient hellhound statuette", 0x3FFD, 0x3EC9),
        (typeof(EtherealBoura), "an ethereal boura statuette", 0x46F8, 0x3EC6),
        (typeof(EtherealPeacock), "an ethereal peacock statuette", 0xA0C0, 0x3ECF)
    ];

    public const string ShopApSetting = "clusterf.etherealLooks.shopAp";
    public const string RenownSettingPrefix = "clusterf.etherealLooks.renown.";
    public const int DefaultShopAp = 1750;

    /// <summary>The League shop's looks: row key, label, default Renown.</summary>
    public static readonly (string Key, Type Type, string Label, int Renown)[] Shop =
    [
        ("peacock", typeof(EtherealPeacock), "Mount: ethereal peacock", 1500),
        ("windrunner", typeof(EtherealWindrunner), "Mount: ethereal windrunner", 2000),
        ("tiger", typeof(EtherealTiger), "Mount: ethereal dark tiger", 2000),
        ("light-tiger", typeof(EtherealLightTiger), "Mount: ethereal light tiger", 2000)
    ];

    /// <summary>The achievements' looks. ClusterFAchievements names the same types as the rewards, for the "+1 item" line.</summary>
    public static readonly (string Achievement, Type Type)[] Achieved =
    [
        ("legacy.new_haven_all", typeof(EtherealLasher)),
        ("mining.master_prospector", typeof(EtherealBoura)),
        ("skills.triple_master", typeof(EtherealAncientHellHound))
    ];

    private static int StatuetteOf(Type type) => All.First(l => l.Type == type).Statuette;

    /// <summary>The League shop rows. LeagueShop.Stock is built once, so these read their settings here, not in Configure.</summary>
    public static LeagueShopEntry[] ShopRows()
    {
        var ap = Math.Max(0, ServerConfiguration.GetOrUpdateSetting(ShopApSetting, DefaultShopAp));

        return Shop.Select(look =>
            {
                var type = look.Type;
                var renown = Math.Max(0, ServerConfiguration.GetOrUpdateSetting(RenownSettingPrefix + look.Key, look.Renown));

                return new LeagueShopEntry(
                    $"ethereal.{look.Key}", look.Label, ap, renown, LeagueShopKind.Mount, StatuetteOf(type), 0,
                    _ => [(Item)Activator.CreateInstance(type)]
                );
            }
        ).ToArray();
    }

    /// <summary>True for a reward PayOwed pays, which SpawnRewardItems must leave alone.</summary>
    public static bool PaysOnce(Type type) => Achieved.Any(a => a.Type == type);

    public static string PaidCounter(string achievement) => $"ethereal_paid.{achievement}";

    /// <summary>Hands this character's account every achieved look it has not had yet. Safe to call at any time, any number of times.</summary>
    public static void PayOwed(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct || pm.Backpack == null)
        {
            return; // not lost: still owed, the next login pays it
        }

        foreach (var (achievement, type) in Achieved)
        {
            var paid = PaidCounter(achievement);
            if (!ClusterFAchievementSystem.HasEarned(acct, achievement) ||
                ClusterFAchievementSystem.GetCounter(acct.Username, paid) > 0)
            {
                continue;
            }

            var statuette = (Item)Activator.CreateInstance(type);
            pm.Backpack.DropItem(statuette);
            ClusterFAchievementSystem.AddToCounter(acct.Username, paid, 1);
            pm.SendMessage(0x44, $"System: Your reward, {statuette.DefaultName}, is in your backpack.");
        }
    }

    public static void Configure()
    {
        Hue = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("clusterf.etherealLooks.hue", SolidHue), 0, 0x7FFF);
        FollowerSlots = Math.Clamp(
            ServerConfiguration.GetOrUpdateSetting("clusterf.etherealLooks.followerSlots", DefaultFollowerSlots),
            0,
            5
        );
    }
}

[SerializationGenerator(0, false)]
public abstract partial class EtherealLook : EtherealMount
{
    protected EtherealLook(int itemID, int mountID) : base(itemID, mountID)
    {
    }

    public override int EtherealHue => EtherealLooks.Hue;

    public override int FollowerSlots => EtherealLooks.FollowerSlots;

    public override void OnSingleClick(Mobile from) => LabelTo(from, DefaultName);
}

[SerializationGenerator(0, false)]
public partial class EtherealWindrunner : EtherealLook
{
    [Constructible]
    public EtherealWindrunner() : base(0x9ED5, 0x3ECC)
    {
    }

    public override string DefaultName => "an ethereal windrunner statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealLasher : EtherealLook
{
    [Constructible]
    public EtherealLasher() : base(0x9E35, 0x3ECB)
    {
    }

    public override string DefaultName => "an ethereal lasher statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealTiger : EtherealLook
{
    [Constructible]
    public EtherealTiger() : base(0x9844, 0x3EC8)
    {
    }

    public override string DefaultName => "an ethereal tiger statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealLightTiger : EtherealLook
{
    [Constructible]
    public EtherealLightTiger() : base(0x9844, 0x3EC7)
    {
    }

    public override string DefaultName => "an ethereal light tiger statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealAncientHellHound : EtherealLook
{
    [Constructible]
    public EtherealAncientHellHound() : base(0x3FFD, 0x3EC9)
    {
    }

    public override string DefaultName => "an ethereal ancient hellhound statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealBoura : EtherealLook
{
    [Constructible]
    public EtherealBoura() : base(0x46F8, 0x3EC6)
    {
    }

    public override string DefaultName => "an ethereal boura statuette";
}

[SerializationGenerator(0, false)]
public partial class EtherealPeacock : EtherealLook
{
    [Constructible]
    public EtherealPeacock() : base(0xA0C0, 0x3ECF)
    {
    }

    public override string DefaultName => "an ethereal peacock statuette";
}
