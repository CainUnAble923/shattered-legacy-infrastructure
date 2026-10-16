// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/MiscSAResources.cs -> ours. Dry reeds (clipped from a
// decorative plant with clippers set to cut reeds), softened reeds (two dry reeds of one colour and a scouring toxin, by
// hand or at the Tinkering and Alchemy menus with the basket-weaving book) and crystal granules (Alchemy 75 from
// shimmering crystals). The reeds carry their plant's colour and stack only with the same colour.
//
// Crafted reeds: pinned's craft engine hands the consumed resource's hue to ICraftable.OnCraft as resHue (CraftItem.cs
// :1459-1463) only for types in its colour tables, which ServUO extends with SetRequireResTarget and the Def* retain
// lists; neither exists in pinned. SoftenedReeds.OnCraft takes the plant hue from resHue when the engine passes one,
// and otherwise reads the first dry reeds in the crafter's pack (ServUO targets the stack to consume; pinned consumes
// the first stack of the type), so the softened reeds come out the colour of the reeds that went in.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Engines.Plants;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DryReeds : Item, IPlantHue
{
    [SerializableField(0, fieldChanged: nameof(OnPlantHueChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantHue _plantHue;

    [Constructible]
    public DryReeds(PlantHue hue = PlantHue.Plain) : base(0x1BD5)
    {
        _plantHue = hue;
        InvalidatePlantHue();
        Stackable = true;
    }

    public override int LabelNumber => 1112248; // dry reeds

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
            list.Add(info.IsBright() ? 1113273 : 1113275, $"{Amount}\t#{info.Name}"); // ~1_AMOUNT~ bright ~2_COLOR~ dry reeds
        }
        else
        {
            list.Add(info.IsBright() ? 1112288 : 1112289, $"#{info.Name}"); // bright ~1_COLOR~ dry reeds
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

[SerializationGenerator(0, false)]
public partial class SoftenedReeds : Item, IPlantHue, ICraftable
{
    [SerializableField(0, fieldChanged: nameof(OnPlantHueChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PlantHue _plantHue;

    [Constructible]
    public SoftenedReeds(PlantHue hue = PlantHue.Plain) : base(0x4006)
    {
        _plantHue = hue;
        InvalidatePlantHue();
        Stackable = true;
    }

    public override int LabelNumber => 1112249; // Softened reeds

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

    public void InvalidateHue()
    {
        var hue = PlantHueLookup.FromItemHue(Hue);

        if (hue != PlantHue.None)
        {
            PlantHue = hue;
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var info = PlantHueInfo.GetInfo(_plantHue);

        if (Amount > 1)
        {
            list.Add(info.IsBright() ? 1113273 : 1113275, $"{Amount}\t#{info.Name}");
        }
        else
        {
            list.Add(info.IsBright() ? 1112288 : 1112289, $"#{info.Name}");
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

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        var hue = PlantHueLookup.FromItemHue(resHue);

        if (hue == PlantHue.None && from?.Backpack?.FindItemByType<DryReeds>() is { } reeds)
        {
            hue = reeds.PlantHue;
        }

        PlantHue = hue == PlantHue.None ? PlantHue.Plain : hue;
        return quality;
    }
}

[SerializationGenerator(0, false)]
public partial class CrystalGranules : Item
{
    [Constructible]
    public CrystalGranules() : base(16392) => Hue = 2625;

    public override int LabelNumber => 1112329; // crystal granules
}
