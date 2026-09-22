// ServUO: Items/Artifacts/Equipment/Weapons/LifeSyphon.cs (CC6 batch 8, Part B). One of the Stygian Dragon's eight
// unique artifacts; CC9 left it and VampiricEssence behind for the attribute below (notes/s9-backlog-remeasure.md).
// Dropped: IsArtifact (D-1); WeaponAttributes.BloodDrinker = 1 (D-87: pinned's AosWeaponAttribute has no
// BloodDrinker member and BaseWeapon.OnHit has no leech-over-time branch).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class LifeSyphon : BloodBlade
{
    [Constructible]
    public LifeSyphon()
    {
        Hue = 1172;
        WeaponAttributes.HitHarm = 30;
        WeaponAttributes.HitLeechHits = 100;
        Attributes.BonusHits = 10;
        Attributes.WeaponSpeed = 25;
        Attributes.WeaponDamage = 50;
    }

    public override int LabelNumber => 1113524; // Life Syphon
    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
