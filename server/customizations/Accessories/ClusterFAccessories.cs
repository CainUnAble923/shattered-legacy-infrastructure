// cc-P262: accessory slots (extra rings, bracelets, toe rings), server first. Spec: D:\UO\cc-prompts\design\
// accessory-slots-spec.md (decided 2026-10-11). Notes: shard-migration notes/cc-P262-accessory-slots.md.
//
// How it works:
//   - Each player wears an AccessoryContainer (AccessoryContainer.cs) on the unused layer 0x0F, created the first time
//     it is needed. It is invisible, cannot be lifted, opened, stolen or looted, and holds the extra jewellery.
//   - Totals: AosAttributes.GetValue adds the active jewellery's Attributes (patch AOS-accessory-container-aggregation);
//     resists come through the container's own resistance properties, which Mobile.ComputeResistances already reads for
//     every worn item; skill and stat bonuses are applied by calling the jewel's own OnAdded/OnRemoved with the wearer,
//     the same code that runs when it goes on a paperdoll layer. Every stock cap still applies to the totals.
//   - Slots: rings 1 (the paperdoll ring) + 1 at loops 1, 5 and 10; bracelets 1 + 1 at a League rank; toe rings 0 + 1 for
//     Plate Bottoms Only and + 1 for Iron Soles. Every threshold is a clusterf.accessories.* setting. Loops (F-12) do
//     not exist yet, so the ring slots wait behind clusterf.accessories.loopSlotsEnabled (false).
//   - Toe rings count only while the shoes layer is empty or holds Plate Socks. Shoes going on or off reach us through
//     PlayerMobile.OnItemAdded / OnItemRemoved (patch PlayerMobile-accessory-hooks).
//   - Companions: no companion type exists yet (F-12/P41 research only). CompanionSlots and AccessoryFootLines are the
//     companion side, ready for that type, and nothing calls them yet.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;

namespace Server;

public enum AccessoryKind
{
    None,
    Ring,
    Bracelet,
    ToeRing
}

/// <summary>Every slot threshold, as a server setting (Configuration/modernuo.json).</summary>
public static class AccessorySettings
{
    // Rings: the loops that each add one ring slot. Loops do not exist yet (F-12), so they are off until this is true.
    public static bool LoopSlotsEnabled => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.loopSlotsEnabled", false);
    public static int RingLoop1 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.ringLoop1", 1);
    public static int RingLoop2 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.ringLoop2", 5);
    public static int RingLoop3 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.ringLoop3", 10);

    // Bracelets: the League rank (1 to 17: Iron to Celestial Citizen) that adds the second bracelet slot. 9 is the middle
    // of the 17-rank ladder (Valorite Citizen). 0 turns the slot off.
    public static int BraceletLeagueRank => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.braceletLeagueRank", 9);

    // Companions (ready for the companion type): companion ranks for the same ring and bracelet slots.
    public static int CompanionRingRank1 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionRingRank1", 1);
    public static int CompanionRingRank2 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionRingRank2", 5);
    public static int CompanionRingRank3 => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionRingRank3", 10);
    public static int CompanionBraceletRank => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionBraceletRank", 5);

    // The barefoot run: armour pieces needed (legs and chest always among them).
    public static int BarefootArmorPieces => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.barefootArmorPieces", 4);

    // The Mood Toe Ring's colour change, in minutes.
    public static int MoodRingMinutes => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.moodRingMinutes", 3);

    // The companion foot lines' cooldowns, in seconds (Hot and Cold events).
    public static int CompanionHotSeconds => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionHotSeconds", 60);
    public static int CompanionColdSeconds => ServerConfiguration.GetOrUpdateSetting("clusterf.accessories.companionColdSeconds", 300);

    public static void Configure()
    {
        _ = LoopSlotsEnabled; _ = RingLoop1; _ = RingLoop2; _ = RingLoop3; _ = BraceletLeagueRank;
        _ = CompanionRingRank1; _ = CompanionRingRank2; _ = CompanionRingRank3; _ = CompanionBraceletRank;
        _ = BarefootArmorPieces; _ = MoodRingMinutes; _ = CompanionHotSeconds; _ = CompanionColdSeconds;
    }
}

public static class ClusterFAccessories
{
    public const string SuffocatedMessage = "Your toe rings have been suffocated. They will remember this.";

    public const int MaxRings = 4;
    public const int MaxBracelets = 2;
    public const int MaxToeRings = 2;

    // Jewels whose bonuses are applied to their wearer right now (memory only: mods are never saved, see OnWorldLoad).
    private static readonly HashSet<Item> _applied = [];

    private static bool _registered;

    // Tests set this: the loop a character is on. Null: the character's real loop, which does not exist yet (F-12).
    public static Func<Mobile, int>? LoopOverride { get; set; }

    public static void Configure()
    {
        Register();
        AccessorySettings.Configure();
        CommandSystem.Register("Accessories", AccessLevel.Player, OnAccessoriesCommand);
        EventSink.WorldLoad += OnWorldLoad;
    }

    /// <summary>The movement subscription. Idempotent; the test host calls it directly.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventSink.Movement += OnMovement;
    }

    // -- What an item is ------------------------------------------------------------------------------------------------

    public static AccessoryKind KindOf(Item? item) => item switch
    {
        ToeRing     => AccessoryKind.ToeRing,
        BaseRing    => AccessoryKind.Ring,
        BaseBracelet => AccessoryKind.Bracelet,
        _           => AccessoryKind.None
    };

    public static bool IsPlateSocks(Item? item) => item is PlateSocks;

    /// <summary>Toe rings count while the shoes layer is empty or holds Plate Socks.</summary>
    public static bool FeetFree(Mobile m) => m.FindItemOnLayer(Layer.Shoes) is not { } shoes || IsPlateSocks(shoes);

    // -- The container --------------------------------------------------------------------------------------------------

    public static AccessoryContainer? ContainerOf(Mobile m) => m.FindItemOnLayer(AccessoryContainer.AccessoryLayer) as AccessoryContainer;

    public static AccessoryContainer GetOrCreateContainer(Mobile m)
    {
        if (ContainerOf(m) is { } c)
        {
            return c;
        }

        c = new AccessoryContainer();
        m.AddItem(c);
        return c;
    }

    public static List<Item> InContainer(Mobile m, AccessoryKind kind)
    {
        var list = new List<Item>();
        if (ContainerOf(m) is { } c)
        {
            foreach (var item in c.Items)
            {
                if (KindOf(item) == kind)
                {
                    list.Add(item);
                }
            }
        }

        return list;
    }

    // -- Slots (spec section 3) -----------------------------------------------------------------------------------------

    /// <summary>The character's loop. Loops do not exist yet (F-12): 0, unless a test sets LoopOverride.</summary>
    public static int LoopOf(Mobile m) => LoopOverride?.Invoke(m) ?? 0;

    /// <summary>All ring slots: the paperdoll ring plus one per loop threshold reached (when loop slots are on).</summary>
    public static int RingSlots(Mobile m)
    {
        var slots = 1;
        if (AccessorySettings.LoopSlotsEnabled)
        {
            var loop = LoopOf(m);
            foreach (var t in new[] { AccessorySettings.RingLoop1, AccessorySettings.RingLoop2, AccessorySettings.RingLoop3 })
            {
                if (t > 0 && loop >= t)
                {
                    slots++;
                }
            }
        }

        return Math.Min(MaxRings, slots);
    }

    public static int BraceletSlots(Mobile m)
    {
        var rank = AccessorySettings.BraceletLeagueRank;
        return m is PlayerMobile pm && rank > 0 && pm.LeagueRank >= rank ? 2 : 1;
    }

    public static int ToeRingSlots(Mobile m)
    {
        if (m.Account is not IAccount acct)
        {
            return 0;
        }

        var slots = 0;
        if (ClusterFAchievementSystem.HasEarned(acct, ClusterFAchievementSystem.PlateBottomsOnlyKey))
        {
            slots++;
        }

        if (ClusterFAchievementSystem.HasEarned(acct, ClusterFAchievementSystem.IronSolesKey))
        {
            slots++;
        }

        return slots;
    }

    /// <summary>Slots of a kind, the paperdoll one included for rings and bracelets.</summary>
    public static int SlotsFor(Mobile m, AccessoryKind kind) => kind switch
    {
        AccessoryKind.Ring     => RingSlots(m),
        AccessoryKind.Bracelet => BraceletSlots(m),
        AccessoryKind.ToeRing  => ToeRingSlots(m),
        _                      => 0
    };

    /// <summary>The container's share of a kind's slots (the paperdoll layer holds the first ring and bracelet).</summary>
    public static int ExtraSlotsFor(Mobile m, AccessoryKind kind) =>
        kind == AccessoryKind.ToeRing ? SlotsFor(m, kind) : Math.Max(0, SlotsFor(m, kind) - 1);

    /// <summary>The one-line hint for a locked slot (the gump's).</summary>
    public static string UnlockHint(AccessoryKind kind, int slot) => kind switch
    {
        AccessoryKind.Ring when !AccessorySettings.LoopSlotsEnabled => "Unlocks with loops (not yet open on this shard).",
        AccessoryKind.Ring => $"Unlocks at loop {(slot switch { 2 => AccessorySettings.RingLoop1, 3 => AccessorySettings.RingLoop2, _ => AccessorySettings.RingLoop3 })}.",
        AccessoryKind.Bracelet when AccessorySettings.BraceletLeagueRank <= 0 => "Not open on this shard.",
        AccessoryKind.Bracelet => $"Unlocks at League rank {ClusterFLeagueRanks.RankName(AccessorySettings.BraceletLeagueRank)}.",
        AccessoryKind.ToeRing => "The System has noted that your feet are not yet qualified.",
        _ => ""
    };

    // -- Which jewels count ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether a jewel in the container counts now: it is within its kind's slots (in the container's order), and a toe
    /// ring also needs bare feet or Plate Socks.
    /// </summary>
    public static bool IsActive(Mobile m, Item item)
    {
        var kind = KindOf(item);
        if (kind == AccessoryKind.None || item.Parent is not AccessoryContainer c || c.Parent != m)
        {
            return false;
        }

        if (kind == AccessoryKind.ToeRing && !FeetFree(m))
        {
            return false;
        }

        var index = 0;
        foreach (var other in c.Items)
        {
            if (other == item)
            {
                break;
            }

            if (KindOf(other) == kind)
            {
                index++;
            }
        }

        return index < ExtraSlotsFor(m, kind);
    }

    public static List<Item> ActiveJewels(Mobile m)
    {
        var list = new List<Item>();
        if (ContainerOf(m) is { } c)
        {
            foreach (var item in c.Items)
            {
                if (IsActive(m, item))
                {
                    list.Add(item);
                }
            }
        }

        return list;
    }

    /// <summary>AosAttributes.GetValue's hook (patched): what the active accessory jewellery adds to this attribute.</summary>
    public static int AttributeTotal(Mobile m, AosAttribute attribute)
    {
        if (ContainerOf(m) is not { Items.Count: > 0 })
        {
            return 0;
        }

        var total = 0;
        foreach (var item in ActiveJewels(m))
        {
            if (item is BaseJewel jewel)
            {
                total += jewel.Attributes[attribute];
            }
        }

        return total;
    }

    /// <summary>The container's resistance (Mobile.ComputeResistances reads it like any worn item's).</summary>
    public static int ResistTotal(Mobile? m, ResistanceType type)
    {
        if (m == null)
        {
            return 0;
        }

        var total = 0;
        foreach (var item in ActiveJewels(m))
        {
            total += type switch
            {
                ResistanceType.Physical => item.PhysicalResistance,
                ResistanceType.Fire     => item.FireResistance,
                ResistanceType.Cold     => item.ColdResistance,
                ResistanceType.Poison   => item.PoisonResistance,
                ResistanceType.Energy   => item.EnergyResistance,
                _                       => 0
            };
        }

        return total;
    }

    /// <summary>
    /// Puts the skill and stat bonuses of every active jewel on its wearer and takes them off every inactive one, through
    /// the jewel's own OnAdded/OnRemoved (what a paperdoll layer runs), then recomputes resists and the status bars.
    /// </summary>
    public static void Refresh(Mobile m)
    {
        if (ContainerOf(m) is { } c)
        {
            foreach (var item in c.Items.ToArray())
            {
                var active = IsActive(m, item);
                if (active && _applied.Add(item))
                {
                    item.OnAdded(m);
                }
                else if (!active && _applied.Remove(item))
                {
                    item.OnRemoved(m);
                }
            }
        }

        m.UpdateResistances();
        m.Delta(MobileDelta.Hits | MobileDelta.Stam | MobileDelta.Mana | MobileDelta.Stat);
        m.InvalidateProperties();
    }

    /// <summary>The container's OnItemRemoved: a jewel that leaves takes its bonuses with it.</summary>
    internal static void OnLeftContainer(Mobile? m, Item item)
    {
        if (_applied.Remove(item) && m != null)
        {
            item.OnRemoved(m);
        }

        if (m != null)
        {
            Refresh(m);
        }
    }

    /// <summary>Whether the bonuses of this jewel are on its wearer now (tests).</summary>
    public static bool IsApplied(Item item) => _applied.Contains(item);

    private static void OnWorldLoad()
    {
        // Skill and stat mods are not saved; put them back for every container's wearer (BaseJewel does this for its own
        // paperdoll layer when it deserializes).
        var owners = new List<Mobile>();
        foreach (var item in World.Items.Values)
        {
            if (item is AccessoryContainer { Parent: Mobile m })
            {
                owners.Add(m);
            }
        }

        foreach (var m in owners)
        {
            Refresh(m);
        }
    }

    // -- Equipping (the gump, the command) ------------------------------------------------------------------------------

    /// <summary>
    /// Puts a jewel the character carries into its first free slot: the paperdoll layer first for a ring or bracelet,
    /// then the container. Returns why not, or null when done.
    /// </summary>
    public static string? Equip(Mobile m, Item item)
    {
        var kind = KindOf(item);
        if (kind == AccessoryKind.None)
        {
            return "That is not a ring, bracelet or toe ring.";
        }

        if (!item.IsChildOf(m.Backpack))
        {
            return "The jewelry must be in your backpack.";
        }

        if (kind != AccessoryKind.ToeRing)
        {
            var layer = kind == AccessoryKind.Ring ? Layer.Ring : Layer.Bracelet;
            if (m.FindItemOnLayer(layer) == null)
            {
                return m.EquipItem(item) ? AfterChange(m, item) : "You cannot wear that.";
            }
        }

        if (InContainer(m, kind).Count >= ExtraSlotsFor(m, kind))
        {
            return SlotsFor(m, kind) == 0 ? "You have no toe ring slots." : "Those slots are full.";
        }

        GetOrCreateContainer(m).DropItem(item);
        return AfterChange(m, item);
    }

    /// <summary>Takes a worn jewel (paperdoll or container) back to the backpack.</summary>
    public static string? Unequip(Mobile m, Item item)
    {
        if (m.Backpack == null)
        {
            return "You have no backpack.";
        }

        if (item.Parent != m && !(item.Parent is AccessoryContainer c && c.Parent == m))
        {
            return "You are not wearing that.";
        }

        m.Backpack.DropItem(item);
        Refresh(m);
        return null;
    }

    private static string? AfterChange(Mobile m, Item item)
    {
        Refresh(m);
        CheckJewelryAchievements(m);

        if (KindOf(item) == AccessoryKind.ToeRing && m is BaseCreature bc)
        {
            AccessoryFootLines.Say(bc, FootEvent.Equip);
        }

        return null;
    }

    // -- Hooks (patch PlayerMobile-accessory-hooks) ---------------------------------------------------------------------

    /// <summary>PlayerMobile.OnItemAdded: shoes over toe rings smother them; a ring or bracelet on its layer may finish a set.</summary>
    public static void OnMobileItemAdded(Mobile m, Item item)
    {
        if (item is AccessoryContainer || ContainerOf(m) is not { } c)
        {
            return;
        }

        if (item.Layer == Layer.Shoes && !IsPlateSocks(item))
        {
            // Were any toe rings counting before these shoes went on? (FeetFree is already false now.)
            var smothered = false;
            var extra = ExtraSlotsFor(m, AccessoryKind.ToeRing);
            var seen = 0;
            foreach (var other in c.Items)
            {
                if (KindOf(other) == AccessoryKind.ToeRing && seen++ < extra)
                {
                    smothered = true;
                }
            }

            if (smothered)
            {
                m.SendMessage(0x22, SuffocatedMessage);
                if (m is PlayerMobile pm)
                {
                    ClusterFAchievementSystem.NotifyToeRingsSmothered(pm);
                }
                else if (m is BaseCreature bc)
                {
                    AccessoryFootLines.Say(bc, FootEvent.Smothered);
                }
            }
        }

        Refresh(m);
        CheckJewelryAchievements(m);
    }

    /// <summary>PlayerMobile.OnItemRemoved: shoes coming off wake the toe rings.</summary>
    public static void OnMobileItemRemoved(Mobile m, Item item)
    {
        if (item is AccessoryContainer || ContainerOf(m) == null)
        {
            return;
        }

        if (item.Layer == Layer.Shoes && m is BaseCreature bc)
        {
            AccessoryFootLines.Say(bc, FootEvent.Bare);
        }

        Refresh(m);
    }

    // -- Achievements: the jewellery ones (spec section 7) ----------------------------------------------------------------

    public static int RingsWorn(Mobile m) =>
        (m.FindItemOnLayer(Layer.Ring) != null ? 1 : 0) + Math.Min(InContainer(m, AccessoryKind.Ring).Count, ExtraSlotsFor(m, AccessoryKind.Ring));

    /// <summary>
    /// Every jewelry slot filled: the paperdoll ring, bracelet, earrings and a necklace, every ring and bracelet slot the
    /// character has, and at least one toe ring slot with every toe ring slot filled and counting.
    /// </summary>
    public static bool EveryJewelrySlotFilled(Mobile m)
    {
        if (m.FindItemOnLayer(Layer.Ring) == null || m.FindItemOnLayer(Layer.Bracelet) == null ||
            m.FindItemOnLayer(Layer.Earrings) == null || m.FindItemOnLayer(Layer.Neck) is not BaseJewel)
        {
            return false;
        }

        var toeSlots = ToeRingSlots(m);
        if (toeSlots == 0)
        {
            return false;
        }

        var active = ActiveJewels(m);
        foreach (var kind in new[] { AccessoryKind.Ring, AccessoryKind.Bracelet, AccessoryKind.ToeRing })
        {
            var filled = active.FindAll(i => KindOf(i) == kind).Count;
            if (filled < ExtraSlotsFor(m, kind))
            {
                return false;
            }
        }

        return true;
    }

    public static void CheckJewelryAchievements(Mobile m)
    {
        if (m is not PlayerMobile pm || pm.Account is not IAccount)
        {
            return;
        }

        if (RingsWorn(pm) >= MaxRings)
        {
            ClusterFAchievementSystem.NotifyAccessoryEvent(pm, TriggerKind.RingBearer);
        }

        if (EveryJewelrySlotFilled(pm))
        {
            ClusterFAchievementSystem.NotifyAccessoryEvent(pm, TriggerKind.FullyAccessorized);
        }
    }

    // -- The barefoot run (Plate Bottoms Only, Iron Soles) --------------------------------------------------------------

    private static bool IsArmorPiece(Item? item) => item is BaseArmor and not BaseShield;

    /// <summary>
    /// Whether a step counts: running, not mounted, alive, the shoes layer empty, and at least 4 armour pieces worn
    /// (shields are not armour slots), the legs and the chest among them.
    /// </summary>
    public static bool CountsAsBarefootStep(Mobile m, Direction d)
    {
        if ((d & Direction.Running) == 0 || m.Mounted || !m.Alive || m.FindItemOnLayer(Layer.Shoes) != null)
        {
            return false;
        }

        if (!IsArmorPiece(m.FindItemOnLayer(Layer.Pants)) || !IsArmorPiece(m.FindItemOnLayer(Layer.InnerTorso)))
        {
            return false;
        }

        var pieces = 0;
        foreach (var layer in new[] { Layer.Helm, Layer.Neck, Layer.InnerTorso, Layer.Arms, Layer.Gloves, Layer.Pants })
        {
            if (IsArmorPiece(m.FindItemOnLayer(layer)))
            {
                pieces++;
            }
        }

        return pieces >= AccessorySettings.BarefootArmorPieces;
    }

    public static void OnMovement(MovementEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm && CountsAsBarefootStep(pm, e.Direction))
        {
            ClusterFAchievementSystem.NotifyBarefootStep(pm);
        }
    }

    // -- The command ------------------------------------------------------------------------------------------------
    //
    // The spec also asks for an "Accessories" context-menu entry on yourself and your companions. A context entry shows a
    // cliloc, and no cliloc reading "Accessories" could be verified on this PC (notes, "Stubbed"); the entry waits for one,
    // or for the TazUO paperdoll slots. [accessories is the way in until then.

    [Usage("Accessories")]
    [Description("Opens your accessory slots: extra rings, bracelets and toe rings.")]
    [ShardCommand(CommandCategory.Player)]
    public static void OnAccessoriesCommand(CommandEventArgs e) => e.Mobile.SendGump(new AccessoriesGump(e.Mobile, e.Mobile));

    // -- Companions (ready for the companion type; nothing calls these yet) ---------------------------------------------

    /// <summary>A companion's slots by companion rank; toe ring slots only as many as its owner has unlocked.</summary>
    public static int CompanionSlots(BaseCreature companion, int companionRank, AccessoryKind kind)
    {
        switch (kind)
        {
            case AccessoryKind.Ring:
                {
                    var slots = 1;
                    foreach (var t in new[] { AccessorySettings.CompanionRingRank1, AccessorySettings.CompanionRingRank2, AccessorySettings.CompanionRingRank3 })
                    {
                        if (t > 0 && companionRank >= t)
                        {
                            slots++;
                        }
                    }

                    return Math.Min(MaxRings, slots);
                }
            case AccessoryKind.Bracelet:
                {
                    var rank = AccessorySettings.CompanionBraceletRank;
                    return rank > 0 && companionRank >= rank ? 2 : 1;
                }
            case AccessoryKind.ToeRing:
                return companion.ControlMaster is { } owner ? ToeRingSlots(owner) : 0;
            default:
                return 0;
        }
    }
}
