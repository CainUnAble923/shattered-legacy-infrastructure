// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/PlantPigment.cs -> ours. Plant pigment (Alchemy 33 or Cooking 75
// from plant clippings and a bottle): the colour of the clippings, and two pigments mix on double-click by ServUO's
// rules (Alchemy or Cooking 75; 100 to mix with black or white; no mixing the same hue or two shades of one hue;
// a saturated pigment mixes no further), leaving an empty bottle in the pack.
//
// Crafted pigment takes its colour from the consumed clippings the same way SoftenedReeds does (see MiscSAResources.cs).

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Engines.Plants;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PlantPigment : Item, IPigmentHue, ICraftable
{
    [SerializableField(0, fieldChanged: nameof(OnPigmentHueChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantPigmentHue _pigmentHue;

    [Constructible]
    public PlantPigment(PlantPigmentHue hue = PlantPigmentHue.None) : base(0x0F02)
    {
        Weight = 1.0;
        PigmentHue = hue;
    }

    public PlantPigment(PlantHue hue) : this(PlantPigmentHueInfo.HueFromPlantHue(hue))
    {
    }

    public override int LabelNumber => 1112132; // plant pigment

    public bool RetainsColorFrom => true;

    private void OnPigmentHueChanged(PlantPigmentHue oldValue, PlantPigmentHue newValue)
    {
        // any invalid pigment hue becomes Plain
        if (_pigmentHue != PlantPigmentHueInfo.GetInfo(_pigmentHue).PlantPigmentHue)
        {
            _pigmentHue = PlantPigmentHue.Plain;
        }

        Hue = PlantPigmentHueInfo.GetInfo(_pigmentHue).Hue;
        InvalidateProperties();
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var info = PlantPigmentHueInfo.GetInfo(_pigmentHue);

        if (Amount > 1)
        {
            list.Add(info.IsBright() ? 1113271 : 1113270, $"{Amount}\t#{info.Name}");
        }
        else
        {
            list.Add(info.IsBright() ? 1112134 : 1112133, $"#{info.Name}"); // bright ~1_COLOR~ plant pigment
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!PlantPigmentHueInfo.IsMixable(PigmentHue))
        {
            from.SendLocalizedMessage(1112125); // This pigment is saturated and cannot be mixed further.
        }
        else
        {
            from.SendLocalizedMessage(1112123); // Which plant pigment do you wish to mix this with?
            from.Target = new InternalTarget(this);
        }
    }

    /// <summary>ServUO's mixing rules. Returns the message sent, or 0 when the two were mixed (this pigment is gone, the other recoloured).</summary>
    public int TryMix(Mobile from, PlantPigment pigment)
    {
        if (pigment == null)
        {
            return 1112124; // You may only mix this with another non-saturated plant pigment.
        }

        if (from.Skills[SkillName.Alchemy].Base < 75.0 && from.Skills[SkillName.Cooking].Base < 75.0)
        {
            return 1112214; // You lack the alchemy or cooking skills to mix plant pigments.
        }

        if ((pigment.PigmentHue is PlantPigmentHue.White or PlantPigmentHue.Black ||
             PigmentHue is PlantPigmentHue.White or PlantPigmentHue.Black) &&
            from.Skills[SkillName.Alchemy].Base < 100.0 && from.Skills[SkillName.Cooking].Base < 100.0)
        {
            return 1112213; // You lack the alchemy or cooking skills to mix so unstable a pigment.
        }

        if (PigmentHue == pigment.PigmentHue)
        {
            return 1112242; // You decide not to waste pigments by mixing two identical colors.
        }

        const PlantPigmentHue shades = PlantPigmentHue.Bright | PlantPigmentHue.Dark | PlantPigmentHue.Ice;

        if ((PigmentHue & ~shades) == (pigment.PigmentHue & ~shades))
        {
            return 1112243; // You decide not to waste pigments by mixing variations of the same hue.
        }

        if (PigmentHue == PlantPigmentHue.White && PlantPigmentHueInfo.IsBright(pigment.PigmentHue) ||
            pigment.PigmentHue == PlantPigmentHue.White && PlantPigmentHueInfo.IsBright(PigmentHue))
        {
            return 1112241; // This pigment is too diluted to be faded further.
        }

        if (!PlantPigmentHueInfo.IsMixable(pigment.PigmentHue))
        {
            return 1112125; // This pigment is saturated and cannot be mixed further.
        }

        var newHue = PlantPigmentHueInfo.Mix(PigmentHue, pigment.PigmentHue);

        if (newHue == PlantPigmentHue.None)
        {
            return 1112125; // This pigment is saturated and cannot be mixed further.
        }

        pigment.PigmentHue = newHue;

        // ServUO: new Bottle().MoveToWorld(m_Item.Location, m_Item.Map), which for a pigment in a pack drops the bottle on
        // the ground at the pack slot's coordinates. Into the pack instead (D-4 rule, a bug that broke it; notes).
        from.AddToBackpack(new Bottle());
        Delete();
        from.PlaySound(0x240);

        return 0;
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        var hue = PlantHueLookup.FromItemHue(resHue);

        if (hue == PlantHue.None && from?.Backpack?.FindItemByType<PlantClippings>() is { } clippings)
        {
            hue = clippings.PlantHue;
        }

        PigmentHue = PlantPigmentHueInfo.HueFromPlantHue(hue);
        return quality;
    }

    private class InternalTarget : Target
    {
        private readonly PlantPigment _item;

        public InternalTarget(PlantPigment item) : base(2, false, TargetFlags.None) => _item = item;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_item.Deleted)
            {
                return;
            }

            var message = _item.TryMix(from, targeted as PlantPigment);

            if (message != 0)
            {
                from.SendLocalizedMessage(message);
            }
        }
    }
}
