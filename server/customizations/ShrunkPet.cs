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

        pm.Followers -= pet.ControlSlots;   // manually decrement — MoveToWorld alone won't

        return fig;
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

        // Return to world
        pet.MoveToWorld(pm.Location, pm.Map);
        pet.SetControlMaster(pm);
        pet.IsBonded    = true;
        pet.ControlOrder = OrderType.Follow;

        pm.Followers += pet.ControlSlots;

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
