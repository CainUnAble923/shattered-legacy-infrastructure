using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// ShrunkPet — a ceramic figurine that holds a temporarily stored pet.
//
// Created when a player uses an OutridersCrook (or higher tier) and selects
// "Shrink Pet" on a bonded, following animal. The pet is moved to Map.Internal
// where it persists across restarts but is invisible to the world.
//
// Double-click the figurine to summon the pet back to your feet.
// The figurine is deleted when the pet is returned.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0)]
public partial class ShrunkPet : Item
{
    // Store direct reference — ModernUO serialises BaseCreature as a serial lookup.
    [SerializableField(0)]
    private BaseCreature? _pet;

    [Constructible]
    public ShrunkPet() : base(0x12B4)   // small ceramic figurine art
    {
        Weight  = 1.0;
        Movable = true;
        Name    = "a ceramic pet figurine";
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Shrinks <paramref name="pet"/> owned by <paramref name="pm"/>:
    /// moves it to Map.Internal and returns the figurine to drop into the pack.
    /// Returns null if the pet is not eligible for shrinking.
    /// </summary>
    public static ShrunkPet? TryShrink(PlayerMobile pm, BaseCreature pet)
    {
        if (pet is null || pet.Deleted || pet.Map == Map.Internal) return null;
        if (!pet.IsBonded)                                          return null;
        if (pet.ControlMaster != pm)                               return null;
        if (pet.ControlOrder  != OrderType.Follow)                 return null;
        if (pm.Backpack       == null)                             return null;

        var fig = new ShrunkPet
        {
            _pet = pet,
            Hue  = pet.Hue,
            Name = $"a figurine of {pet.Name}"
        };

        // Move pet off the map — stays in World.Mobiles but is invisible
        pet.ControlOrder = OrderType.None;
        pet.MoveToWorld(new Point3D(1, 1, 0), Map.Internal);

        // Take the pet out of pm's follower count and AllFollowers by hand; MoveToWorld alone won't.
        // Left in AllFollowers, pinned's SE auto-stable stables it on logout and at startup and login
        // puts it back at pm's feet (PlayerMobile.cs:1522, :1081, :1276), emptying the figurine.
        // Master stays pm on purpose: clearing it, as stabling does, would expose the pet to the 3-day
        // abandon timer at the next load (BaseCreature.cs:2504), because IsStabled is not saved.
        pm.Followers -= pet.ControlSlots;
        pm.RemoveFollower(pet);

        return fig;
    }

    // Neither Followers nor AllFollowers is saved (pinned Mobile.cs:6331-6334). At load every creature's
    // Deserialize puts itself back in both for its master (BaseCreature.cs:2514), a figurine's pet
    // included, undoing what TryShrink did. Undo it again once the world is loaded (Main.cs: World.Load,
    // then Initialize), before startup's CheckPets timer can auto-stable the pet. Also corrects
    // figurines shrunk before this fix.
    public static void Initialize()
    {
        foreach (var item in World.Items.Values)
        {
            if (item is ShrunkPet { _pet: { Deleted: false, ControlMaster: { } master } pet } &&
                pet.Map == Map.Internal)
            {
                master.Followers -= Math.Min(pet.ControlSlots, master.Followers);
                (master as PlayerMobile)?.RemoveFollower(pet);
            }
        }
    }

    // ── Use ───────────────────────────────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)           return;
        if (!IsChildOf(pm.Backpack))
        {
            from.SendMessage("The figurine must be in your backpack.");
            return;
        }

        var pet = _pet is { Deleted: false } ? _pet : null;

        if (pet == null)
        {
            from.SendMessage("The spirit of this creature has faded. The figurine crumbles.");
            Delete();
            return;
        }

        if (pet.Map != Map.Internal)
        {
            from.SendMessage("This creature does not appear to be stored in the figurine.");
            return;
        }

        if (from.Followers + pet.ControlSlots > from.FollowersMax)
        {
            from.SendMessage("You have too many followers to call this creature forth.");
            return;
        }

        // While shrunk the pet is in no one's Followers or AllFollowers. Put it back with its master
        // first; SetControlMaster then leaves it there or, for a different holder, moves it through the
        // Master setter (BaseCreature.cs:1163-1170). Adding the slots after SetControlMaster counted the
        // pet twice whenever the master changed.
        var owner = pet.ControlMaster;
        if (owner != null)
        {
            owner.Followers += pet.ControlSlots;
            (owner as PlayerMobile)?.AddFollower(pet);
        }

        if (owner != pm && !pet.SetControlMaster(pm))
        {
            if (owner != null)
            {
                owner.Followers -= pet.ControlSlots;
                (owner as PlayerMobile)?.RemoveFollower(pet);
            }

            return;
        }

        // Return to world
        pet.MoveToWorld(pm.Location, pm.Map);
        pet.IsBonded = true;

        // Not a raw ControlOrder = Follow: SetControlMaster clears ControlTarget, and Follow with no
        // target drops to None on the next AI tick (PetOrders.cs:638-644). IssueOrder sets the target.
        pet.IssueOrder(OrderType.Follow, null, pm);

        pm.SendMessage(0x44, $"{pet.Name} materialises at your feet.");
        Effects.PlaySound(pm.Location, pm.Map, 0x1FA);

        Delete();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (_pet is { Deleted: false } p)
        {
            list.Add($"Contains: {p.Name} ({p.GetType().Name})");
            if (p.IsBonded) list.Add("Bonded");
        }
    }
}
