// cc-P764: the pet leash. A port of "Pet shrink for 57.2" by Queen (servuo.dev, 2022, from Xanthos's Shrink System;
// reference copy D:\UO\third-party\code\pet-shrink). Double-click the leash and target your own pet: it shrinks into a
// figurine in your backpack (weight 1) and comes back out at your feet when you double-click the figurine. Its name,
// loyalty, owner and bond are kept as they were: no stats, taming or control slots change.
//
// What is ported from Queen's ShrinkTarget and ShrinkItem: the refusals (not your pet, a summon, a dead pet, a pet carrying
// items, a fight, a ridden mount), the figurine's art from ShrinkTable and the pet's hue, charges and the blessed options.
// What differs:
// - Only the owner can call the pet out. Queen's figurine let any holder claim it unless locked; here there is no lock.
// - "In a fight" is any aggression within clusterf.petLeash.combatSeconds, or a current combatant, for the player and
//   the pet. Queen used SpellHelper.CheckCombat, which on pinned counts only fights with players.
// - No loyalty floor. Queen raised loyalty to 60% on shrink and restore; that would change a pet's loyalty.
// - Storage is the shard's ShrunkPet (the Outrider's Crook figurine), which LeashedPet extends: the pet keeps its master
//   and leaves the follower count by hand, and ShrunkPet.Initialize redoes that after every load. Queen's figurine cleared
//   the master and marked the pet stabled; on pinned IsStabled is not saved, so the next load would start the 3-day
//   abandoned-pet delete timer (BaseCreature.cs:2433, 2504).
// - Not ported: the hitching post (notes cc-P764), the staff [Shrink, [ShrinkLockDown and [ShrinkRelease commands, the
//   lock and unlock context entries, the stat and skill lines on the figurine, the XML config parser.
//
// Config (modernuo.json): clusterf.petLeash.price, .charges (-1 = unlimited), .refuseInCombat, .combatSeconds,
// .blessedLeash, .blessedFigurine.

using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

public static class ClusterFPetLeash
{
    /// <summary>The animal trainer's price for a leash (SBPetRebirth).</summary>
    public static int Price => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.price", 150);

    /// <summary>Shrinks a new leash holds; -1 is unlimited. Restoring never uses a charge.</summary>
    public static int Charges => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.charges", -1);

    /// <summary>Refuse to shrink or restore while the player (or, to shrink, the pet) is fighting.</summary>
    public static bool RefuseInCombat => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.refuseInCombat", true);

    /// <summary>How long after the last blow a player or pet still counts as fighting.</summary>
    public static int CombatSeconds => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.combatSeconds", 15);

    public static bool BlessedLeash => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.blessedLeash", true);

    public static bool BlessedFigurine => ServerConfiguration.GetOrUpdateSetting("clusterf.petLeash.blessedFigurine", true);

    /// <summary>The leash's reach, in tiles.</summary>
    public const int Range = 3;

    public const int HintHue = 0x3B2;
    public const int RefuseHue = 0x22;

    /// <summary>A current combatant, or any aggression either way within CombatSeconds.</summary>
    public static bool InCombat(Mobile m)
    {
        if (m.Combatant != null)
        {
            return true;
        }

        var window = TimeSpan.FromSeconds(CombatSeconds);
        foreach (var info in m.Aggressed)
        {
            if (Core.Now - info.LastCombatTime < window)
            {
                return true;
            }
        }

        foreach (var info in m.Aggressors)
        {
            if (Core.Now - info.LastCombatTime < window)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Why <paramref name="pet"/> cannot be leashed by <paramref name="pm"/>, or null when it can.</summary>
    public static string? Refusal(PlayerMobile pm, BaseCreature? pet)
    {
        if (pet == null || pet.Deleted || pet.Map == null || pet.Map == Map.Internal || pet.Map != pm.Map)
        {
            return "A leash only holds a pet.";
        }

        if (pet.Summoned || pet is BaseTalismanSummon)
        {
            return "A summoned creature slips the leash. Only pets can be leashed.";
        }

        if (!pet.Controlled || pet.ControlMaster != pm)
        {
            return "That is not your pet.";
        }

        if (pet.IsDeadPet)
        {
            return "Your pet must be alive to be leashed.";
        }

        if (pet is BaseMount { Rider: not null })
        {
            return "Dismount first.";
        }

        if (pet.Backpack?.Items.Count > 0)
        {
            return "Your pet is carrying things. Unload its pack first.";
        }

        if (RefuseInCombat && (InCombat(pm) || InCombat(pet)))
        {
            return "Not in the middle of a fight. Try again once things calm down.";
        }

        if (!pm.InRange(pet, Range))
        {
            return "Your pet is too far away. Bring it closer.";
        }

        return null;
    }

    /// <summary>
    /// Shrinks the pet into a figurine in the backpack, or says why not and returns null. The pet keeps its master,
    /// as ShrunkPet.TryShrink does, and leaves the follower count and AllFollowers by hand.
    /// </summary>
    public static LeashedPet? Shrink(PlayerMobile pm, BaseCreature? pet)
    {
        var refusal = pm.Backpack == null ? "You have no backpack to carry it in." : Refusal(pm, pet);
        if (refusal != null)
        {
            pm.SendMessage(RefuseHue, refusal);
            return null;
        }

        var fig = new LeashedPet(pet!);

        pet!.Combatant = null;
        pet.ControlTarget = null;
        pet.ControlOrder = OrderType.None;
        pet.MoveToWorld(new Point3D(1, 1, 0), Map.Internal);
        pm.Followers -= Math.Min(pet.ControlSlots, pm.Followers);
        pm.RemoveFollower(pet);

        pm.Backpack!.DropItem(fig);
        pm.PlaySound(0x1FE);
        pm.SendMessage(
            HintHue,
            $"{pet.Name} shrinks into a figurine in your backpack. Double-click the figurine to call {pet.Name} back out."
        );
        return fig;
    }
}

/// <summary>cc-P764: Queen's pet leash. Double-click, then target your pet.</summary>
[SerializationGenerator(0)]
public partial class PetLeash : Item
{
    /// <summary>Art 0x1374 (a bridle), as Queen's leash.</summary>
    public const int LeashItemID = 0x1374;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _charges;

    [Constructible]
    public PetLeash() : this(ClusterFPetLeash.Charges)
    {
    }

    [Constructible]
    public PetLeash(int charges) : base(LeashItemID)
    {
        _charges = charges;
        Weight = 1.0;
        LootType = ClusterFPetLeash.BlessedLeash ? LootType.Blessed : LootType.Regular;
    }

    public override string DefaultName => "a pet leash";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (_charges >= 0)
        {
            list.Add($"Uses left: {_charges}");
        }

        list.Add("Double-click, then target your pet: it shrinks into a figurine");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        pm.SendMessage(ClusterFPetLeash.HintHue, "Which pet will you leash?");
        pm.Target = new LeashTarget(this);
    }

    /// <summary>Leashes the pet: the figurine, or null with the reason said. Uses a charge when the leash has them.</summary>
    public LeashedPet? Use(PlayerMobile pm, BaseCreature? pet)
    {
        if (Deleted || !IsChildOf(pm.Backpack))
        {
            return null;
        }

        if (_charges == 0)
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "This leash is worn through.");
            return null;
        }

        var fig = ClusterFPetLeash.Shrink(pm, pet);
        if (fig != null && _charges > 0)
        {
            Charges = _charges - 1;
            if (_charges == 0)
            {
                pm.SendMessage(ClusterFPetLeash.HintHue, "The leash wears through and falls apart.");
                Delete();
            }
            else
            {
                InvalidateProperties();
            }
        }

        return fig;
    }

    private class LeashTarget : Target
    {
        private readonly PetLeash _leash;

        public LeashTarget(PetLeash leash) : base(ClusterFPetLeash.Range, false, TargetFlags.None) => _leash = leash;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is PlayerMobile pm)
            {
                _leash.Use(pm, targeted as BaseCreature);
            }
        }
    }
}

/// <summary>
/// cc-P764: a leashed pet's figurine, the pet's own statuette art in its hue. Only the pet's owner can call it out, not in a
/// fight. A ShrunkPet, so ShrunkPet.Initialize keeps the follower count right across restarts; its own double-click
/// replaces ShrunkPet's, which lets any holder claim the pet and bonds it.
/// </summary>
[SerializationGenerator(0)]
public partial class LeashedPet : ShrunkPet
{
    [Constructible]
    public LeashedPet()
    {
        Weight = 1.0;
    }

    public LeashedPet(BaseCreature pet) : this()
    {
        Pet = pet;
        ItemID = ShrinkTable.Lookup(pet);
        Hue = pet.Hue;
        Name = $"a figurine of {pet.Name}";
        LootType = ClusterFPetLeash.BlessedFigurine ? LootType.Blessed : LootType.Regular;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Pet is { Deleted: false, ControlMaster: { } owner })
        {
            list.Add($"Owner: {owner.Name}");
        }

        list.Add("Double-click to call your pet back out");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm)
        {
            Restore(pm);
        }
    }

    /// <summary>Calls the pet out at the player's feet: true when it came out; otherwise the reason is said.</summary>
    public bool Restore(PlayerMobile pm)
    {
        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        var pet = Pet is { Deleted: false } p ? p : null;
        if (pet == null)
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "The figurine is empty. It crumbles to dust.");
            Delete();
            return false;
        }

        if (pet.Map != Map.Internal)
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "This figurine holds no pet.");
            return false;
        }

        var owner = pet.ControlMaster;
        if (owner != pm)
        {
            pm.SendMessage(
                ClusterFPetLeash.RefuseHue,
                owner == null ? "The pet in this figurine answers to no one." : $"Only {owner.Name} can call this pet out."
            );
            return false;
        }

        if (!pm.Alive)
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "Your pet will not come out to a ghost.");
            return false;
        }

        if (ClusterFPetLeash.RefuseInCombat && ClusterFPetLeash.InCombat(pm))
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "Not in the middle of a fight. Try again once things calm down.");
            return false;
        }

        if (pm.Followers + pet.ControlSlots > pm.FollowersMax)
        {
            pm.SendMessage(ClusterFPetLeash.RefuseHue, "You have too many followers to call this pet out.");
            return false;
        }

        // The reverse of ClusterFPetLeash.Shrink. Not a raw ControlOrder = Follow: IssueOrder sets the target, as
        // ShrunkPet's restore does (PetOrders.cs:638-644 drops a Follow with no target).
        pm.Followers += pet.ControlSlots;
        pm.AddFollower(pet);
        pet.MoveToWorld(pm.Location, pm.Map);
        pet.IssueOrder(OrderType.Follow, null, pm);

        pm.SendMessage(ClusterFPetLeash.HintHue, $"{pet.Name} comes out of the figurine and follows you.");
        pm.PlaySound(0x1FA);

        Pet = null;
        Delete();
        return true;
    }

    // A figurine deleted with its pet still inside would leave the pet on the internal map for good: delete the pet too,
    // as Queen's ShrinkItem does.
    public override void OnDelete()
    {
        if (Pet is { Deleted: false } pet && pet.Map == Map.Internal)
        {
            Pet = null;
            pet.Delete();
        }

        base.OnDelete();
    }
}
