// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/Artifacts/TortureRackArtifact.cs (cc-P194).
//
// The Torture Rack, a three-piece addon (east 0x4AAB/0x4AA3/0x4AA2, south 0x4AA0/0x4AA1/0x4AAD) with its two
// deeds, artifact rarity 10 on every piece. A Wrong decoration artifact; nothing in the two rows drops it (ServUO
// puts it in its Wrong loot through other files), so it is placed by hand or a future loot table.
//
// Conversion notes: ServUO's IArtifact and ShowArtifactRarity have no pinned reader; the rarity line is added in
// GetProperties as the component does. ForceShowProperties is ObjectPropertyList.Enabled there and here.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TortureRackComponent : AddonComponent
{
    public TortureRackComponent(int itemID) : base(itemID)
    {
    }

    public override int LabelNumber => 1152307; // Torture Rack

    public override bool ForceShowProperties => ObjectPropertyList.Enabled;

    public virtual int ArtifactRarity => 10;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1061078, ArtifactRarity.ToString()); // artifact rarity ~1_val~
    }
}

[SerializationGenerator(0, false)]
public partial class TortureRackEast : BaseAddon
{
    [Constructible]
    public TortureRackEast()
    {
        AddComponent(new TortureRackComponent(0x4AAB), 0, 0, 0);
        AddComponent(new TortureRackComponent(0x4AA3), 0, 1, 0);
        AddComponent(new TortureRackComponent(0x4AA2), 0, 2, 0);
    }

    public override BaseAddonDeed Deed => new TortureRackEastDeed();
}

[SerializationGenerator(0, false)]
public partial class TortureRackEastDeed : BaseAddonDeed
{
    [Constructible]
    public TortureRackEastDeed()
    {
    }

    public override int LabelNumber => 1152305; // Torture Rack (east)

    public override BaseAddon Addon => new TortureRackEast();
}

[SerializationGenerator(0, false)]
public partial class TortureRackSouth : BaseAddon
{
    [Constructible]
    public TortureRackSouth()
    {
        AddComponent(new TortureRackComponent(0x4AA0), 0, 0, 0);
        AddComponent(new TortureRackComponent(0x4AA1), 1, 0, 0);
        AddComponent(new TortureRackComponent(0x4AAD), 2, 0, 0);
    }

    public override BaseAddonDeed Deed => new TortureRackSouthDeed();
}

[SerializationGenerator(0, false)]
public partial class TortureRackSouthDeed : BaseAddonDeed
{
    [Constructible]
    public TortureRackSouthDeed()
    {
    }

    public override int LabelNumber => 1152306; // Torture Rack (south)

    public override BaseAddon Addon => new TortureRackSouth();
}
