// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/GargishAsclepius.cs (cc-P195): one of Clockwork Exodus's ten artifact
// replicas (one kill in five hands one to a player with looting rights). ServUO's IsArtifact has no pinned reader
// (the recipe); the rest is the recipe's transformation.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GargishAsclepius : GargishGnarledStaff
{
    [Constructible]
    public GargishAsclepius()
    {
    }

    public override bool CanFortify => false;

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override int LabelNumber => 1153526; // Asclepius [Replica]

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);
        list.Add(1153525); // 15% Bandage Healing Bonus
    }
}
