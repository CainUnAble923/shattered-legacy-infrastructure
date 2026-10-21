// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/DupresSword.cs (cc-P195): one of Clockwork Exodus's ten artifact
// replicas (one kill in five hands one to a player with looting rights). ServUO's IsArtifact has no pinned reader
// (the recipe); the rest is the recipe's transformation.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DupresSword : VikingSword
{
    [Constructible]
    public DupresSword()
    {
        Hue = 0xA91;
        Attributes.BonusStr = 10;
        Attributes.AttackChance = 25;
        Attributes.WeaponSpeed = 35;
        Attributes.WeaponDamage = 100;
        // ServUO: WeaponAttributes.HitManaDrain = 50; pinned has no HitManaDrain attribute (D, notes).
    }

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override int LabelNumber => 1153551; // Dupre's Sword [Replica]
}
