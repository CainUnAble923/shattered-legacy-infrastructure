// cc-P128: ServUO pub57 Scripts/Items/Resource/PlantClippings.cs -> ours, a forced leaf dependency of the basket
// weaving port (the port recipe's LuckyCoin rule): clippings cut from a decorative plant with clippers, the resource
// behind plant pigment (Alchemy 33, Cooking 75) and three of the Eodon potions. Coloured like the plant; stacks by colour.

using ModernUO.Serialization;
using Server.Engines.Plants;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PlantClippings : Item, IPlantHue
{
    [SerializableField(0, fieldChanged: nameof(OnPlantHueChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantHue _plantHue;

    [Constructible]
    public PlantClippings(PlantHue hue = PlantHue.Plain) : base(0x4022)
    {
        _plantHue = hue;
        InvalidatePlantHue();
        Stackable = true;
    }

    public override int LabelNumber => 1112131; // plant clippings

    private void OnPlantHueChanged(PlantHue oldValue, PlantHue newValue) => InvalidatePlantHue();

    public void InvalidatePlantHue()
    {
        var info = PlantHueInfo.GetInfo(_plantHue);

        if (info.PlantHue != _plantHue)
        {
            _plantHue = PlantHue.Plain;
            Hue = 0;
        }
        else
        {
            Hue = info.Hue;
        }

        InvalidateProperties();
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var info = PlantHueInfo.GetInfo(_plantHue);

        if (Amount > 1)
        {
            list.Add(info.IsBright() ? 1113272 : 1113274, $"{Amount}\t#{info.Name}"); // ~1_AMOUNT~ bright ~2_COLOR~ plant clippings
        }
        else
        {
            list.Add(info.IsBright() ? 1112121 : 1112122, $"#{info.Name}"); // bright ~1_COLOR~ plant clippings
        }
    }

    public override bool CanStackWith(Item dropped) =>
        dropped is IPlantHue other && other.PlantHue == _plantHue && base.CanStackWith(dropped);

    public override void OnAfterDuped(Item newItem)
    {
        if (newItem is IPlantHue other)
        {
            other.PlantHue = _plantHue;
        }

        base.OnAfterDuped(newItem);
    }
}
