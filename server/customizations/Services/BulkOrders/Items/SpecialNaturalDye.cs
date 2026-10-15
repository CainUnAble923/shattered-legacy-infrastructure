// cc-P123: the 2016 alchemy, inscription and cooking bulk-order dyes, ported from ServUO
// Scripts/Services/BulkOrders/Items/SpecialNaturalDye.cs. Five uses of one of five colours; a "Spellbook Only" dye colours
// spellbooks only. The colours and their names are ServUO's (Configure :250-260).
//
// What it may dye is ServUO's list as pinned has it: anything dyable, books, clothing, jewellery, weapons, runebooks,
// talismans, spellbooks, what Pigments of Tokuno may colour, metal and leather armour that is not bone, and a locked-down
// or secured piece of house furniture by its house's co-owner. ServUO's statuettes and "any artifact" are not in pinned
// (pinned keeps no IsArtifact, D-1), so they are not dyable here.

using ModernUO.Serialization;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

public enum DyeType
{
    None,
    WindAzul,
    DullRuby,
    PoppieWhite,
    ZentoOrchid,
    UmbranViolet
}

[SerializationGenerator(0, false)]
public partial class SpecialNaturalDye : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DyeType _dyeType;

    [SerializableField(1)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [SerializableField(2)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _booksOnly;

    [Constructible]
    public SpecialNaturalDye(DyeType type, bool booksOnly = false) : base(0x182B)
    {
        _dyeType = type;
        _usesRemaining = 5;
        _booksOnly = booksOnly;
        Hue = HueFor(type);
    }

    public override int LabelNumber => 1112136; // natural dye

    public override double DefaultWeight => 1.0;

    /// <summary>ServUO's HueInfo: each colour's hue and the cliloc that names it.</summary>
    public static (int Hue, int Name) Info(DyeType type) =>
        type switch
        {
            DyeType.WindAzul     => (2741, 1157277),
            DyeType.DullRuby     => (2731, 1157267),
            DyeType.PoppieWhite  => (2735, 1157271),
            DyeType.ZentoOrchid  => (2732, 1157268),
            DyeType.UmbranViolet => (2740, 1157276),
            _                    => (0, 0)
        };

    public static int HueFor(DyeType type) => Info(type).Hue;

    public override void AddNameProperty(IPropertyList list)
    {
        var name = Info(_dyeType).Name;

        if (name == 0)
        {
            base.AddNameProperty(list);
        }
        else if (Amount > 1)
        {
            list.Add(1113276, $"{Amount}\t{name:#}"); // ~1_AMOUNT~ ~2_COLOR~ natural dyes
        }
        else
        {
            list.Add(1112137, $"{name:#}"); // ~1_COLOR~ natural dye
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1060584, _usesRemaining); // uses remaining: ~1_val~

        if (_booksOnly)
        {
            list.Add(1157205); // Spellbook Only Dye
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        from.SendLocalizedMessage(1112139); // Select the item you wish to dye.
        from.BeginTarget(1, false, TargetFlags.None, (m, targeted) => Dye(m, targeted));
    }

    public void Dye(Mobile from, object targeted)
    {
        if (Deleted)
        {
            return;
        }

        var refused = false;

        if (targeted is not Item item || !CanDye(from, item, out refused))
        {
            if (!refused)
            {
                from.SendLocalizedMessage(1042083); // You cannot dye that.
            }

            return;
        }

        item.Hue = Hue;
        from.PlaySound(0x23E);

        if (--UsesRemaining <= 0)
        {
            Delete();
        }
    }

    /// <summary>ServUO's InternalTarget.OnTarget checks; refused is true when a message has already been sent.</summary>
    private bool CanDye(Mobile from, Item item, out bool refused)
    {
        refused = false;

        if (_booksOnly)
        {
            return item is Spellbook;
        }

        if (item is IDyable or BaseBook or BaseClothing or BaseJewel or BaseWeapon or Runebook or BaseTalisman or Spellbook ||
            BasePigmentsOfTokuno.IsValidItem(item))
        {
            return true;
        }

        if (item is BaseArmor armor && armor.MaterialType != ArmorMaterialType.Bone &&
            CraftResources.GetType(armor.Resource) is CraftResourceType.Leather or CraftResourceType.Metal)
        {
            return true;
        }

        if (!FurnitureAttribute.Check(item))
        {
            return false;
        }

        refused = true;

        if (!from.InRange(GetWorldLocation(), 1) || !from.InRange(item.GetWorldLocation(), 1))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return false;
        }

        var house = BaseHouse.FindHouseAt(item);

        if (house == null || !item.IsLockedDown && !item.IsSecure)
        {
            from.SendLocalizedMessage(501022); // Furniture must be locked down to paint it.
            return false;
        }

        if (!house.IsCoOwner(from))
        {
            from.SendLocalizedMessage(501023); // You must be the owner to use this item.
            return false;
        }

        refused = false;
        return true;
    }
}
