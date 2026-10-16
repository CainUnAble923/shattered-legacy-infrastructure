// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/NaturalDye.cs -> ours. Natural dye (Alchemy 75 or Cooking 65 from
// a plant pigment and a colour fixative): five uses of the pigment's colour on a dyable item, a talisman, book, piece of
// clothing or jewellery, weapon, runebook or spellbook, anything the Tokuno pigments take, leather or metal armour that
// is not bone, or locked-down furniture in a house you co-own.
//
// Three of ServUO's targets are not here: BaseStatuette, DecorativePlant and ShoulderParrot are in neither tree, and
// Item.IsArtifact has no pinned equivalent (AGENTS.md: delete the line and log it); the hooded shroud and monk robe
// refuse, as there.

using ModernUO.Serialization;
using Server.Engines.Plants;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NaturalDye : Item, IPigmentHue
{
    [SerializableField(0, fieldChanged: nameof(OnPigmentHueChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantPigmentHue _pigmentHue;

    [SerializableField(1)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public NaturalDye(PlantPigmentHue hue = PlantPigmentHue.None) : base(0x182B)
    {
        Weight = 1.0;
        PigmentHue = hue;
        _usesRemaining = 5;
    }

    public override int LabelNumber => 1112136; // natural dye

    public bool RetainsColorFrom => true;

    public override bool ForceShowProperties => ObjectPropertyList.Enabled;

    private void OnPigmentHueChanged(PlantPigmentHue oldValue, PlantPigmentHue newValue)
    {
        if (_pigmentHue != PlantPigmentHueInfo.GetInfo(_pigmentHue).PlantPigmentHue)
        {
            _pigmentHue = PlantPigmentHue.Plain;
        }

        Hue = PlantPigmentHueInfo.GetInfo(_pigmentHue).Hue;
        InvalidateProperties();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, $"{_usesRemaining}"); // uses remaining: ~1_val~
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var hueInfo = PlantPigmentHueInfo.GetInfo(_pigmentHue);

        if (Amount > 1)
        {
            list.Add(hueInfo.IsBright() ? 1113277 : 1113276, $"{Amount}\t#{hueInfo.Name}");
        }
        else
        {
            list.Add(hueInfo.IsBright() ? 1112138 : 1112137, $"#{hueInfo.Name}"); // ~1_COLOR~ natural dye
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        from.SendLocalizedMessage(1112139); // Select the item you wish to dye.
        from.Target = new InternalTarget(this);
    }

    public static bool IsValidTarget(Item item) =>
        item is IDyable or BaseTalisman or BaseBook or BaseClothing or BaseJewel or BaseWeapon or Runebook or Spellbook ||
        BasePigmentsOfTokuno.IsValidItem(item);

    /// <summary>ServUO's target: the message sent, or 0 when the item was dyed and a use spent.</summary>
    public int TryDye(Mobile from, Item item)
    {
        if (item == null)
        {
            return 1042083; // You cannot dye that.
        }

        var valid = IsValidTarget(item);

        if (item is HoodedShroudOfShadows or MonkRobe)
        {
            return 1042083; // You cannot dye that.
        }

        if (!valid && FurnitureAttribute.Check(item))
        {
            if (!from.InRange(GetWorldLocation(), 1) || !from.InRange(item.GetWorldLocation(), 1))
            {
                return 500446; // That is too far away.
            }

            var house = BaseHouse.FindHouseAt(item);

            if (house == null || !item.IsLockedDown && !item.IsSecure)
            {
                return 501022; // Furniture must be locked down to paint it.
            }

            if (!house.IsCoOwner(from))
            {
                return 501023; // You must be the owner to use this item.
            }

            valid = true;
        }
        else if (!item.IsChildOf(from.Backpack))
        {
            return 1060640; // The item must be in your backpack to use it.
        }

        if (!valid && item is BaseArmor armor)
        {
            var restype = CraftResources.GetType(armor.Resource);

            if (restype is CraftResourceType.Leather or CraftResourceType.Metal && armor.MaterialType != ArmorMaterialType.Bone)
            {
                valid = true;
            }
        }

        if (!valid)
        {
            return 1042083; // You cannot dye that.
        }

        item.Hue = PlantPigmentHueInfo.GetInfo(PigmentHue).Hue;
        from.PlaySound(0x23E);

        if (--UsesRemaining <= 0)
        {
            Delete();
        }

        return 0;
    }

    private class InternalTarget : Target
    {
        private readonly NaturalDye _item;

        public InternalTarget(NaturalDye item) : base(1, false, TargetFlags.None) => _item = item;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_item.Deleted)
            {
                return;
            }

            var message = _item.TryDye(from, targeted as Item);

            if (message != 0)
            {
                from.SendLocalizedMessage(message);
            }
        }
    }
}
