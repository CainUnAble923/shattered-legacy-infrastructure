// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/BracersofAlchemicalDevastation.cs (cc-P195): one of Clockwork Exodus's ten artifact
// replicas (one kill in five hands one to a player with looting rights). ServUO's IsArtifact has no pinned reader
// (the recipe); the rest is the recipe's transformation.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BracersofAlchemicalDevastation : BoneArms
{
    [Constructible]
    public BracersofAlchemicalDevastation()
    {
        Attributes.RegenMana = 4;
        Attributes.CastRecovery = 3;
        ArmorAttributes.MageArmor = 1;
        // ServUO: WeaponAttributes.HitLightning = 35; pinned armor carries no weapon attributes (D, notes).
    }

    public override int LabelNumber => 1153523; // Bracers of Alchemical Devastation [Replica]

    public override int BasePhysicalResistance => 10;
    public override int BaseFireResistance => 8;
    public override int BaseColdResistance => 8;
    public override int BasePoisonResistance => 8;
    public override int BaseEnergyResistance => 8;
    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override bool CanFortify => false;
}
