// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/ClockworkLeggings.cs (cc-P195): one of Clockwork Exodus's ten artifact
// replicas (one kill in five hands one to a player with looting rights). ServUO's IsArtifact has no pinned reader
// (the recipe); the rest is the recipe's transformation.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ClockworkLeggings : PlateLegs
{
    [Constructible]
    public ClockworkLeggings()
    {
        Hue = 0xA91;
        Attributes.RegenStam = 5;
        Attributes.DefendChance = 25;
        Attributes.BonusDex = 5;
    }

    public override int LabelNumber => 1153536; // Clockwork Leggings [Replica]

    public override int BasePhysicalResistance => 8;
    public override int BaseFireResistance => 6;
    public override int BaseColdResistance => 5;
    public override int BasePoisonResistance => 6;
    public override int BaseEnergyResistance => 5;
    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
