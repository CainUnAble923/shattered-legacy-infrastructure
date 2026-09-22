// ServUO: Items/Artifacts/Equipment/Weapons/VampiricEssence.cs (CC6 batch 8, Part B). One of the Stygian Dragon's
// eight unique artifacts; CC9 left it and LifeSyphon behind for the attribute below (notes/s9-backlog-remeasure.md).
// Dropped: IsArtifact (D-1); WeaponAttributes.BloodDrinker = 1 (D-87: pinned's AosWeaponAttribute has no
// BloodDrinker member and BaseWeapon.OnHit has no leech-over-time branch).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class VampiricEssence : Cutlass
{
    [Constructible]
    public VampiricEssence()
    {
        Hue = 39;
        WeaponAttributes.HitLeechHits = 100;
        WeaponAttributes.HitHarm = 50;
        Attributes.WeaponSpeed = 20;
        Attributes.WeaponDamage = 50;
        AosElementDamages.Cold = 100;
    }

    public override int LabelNumber => 1113873; // Vampiric Essence
    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
