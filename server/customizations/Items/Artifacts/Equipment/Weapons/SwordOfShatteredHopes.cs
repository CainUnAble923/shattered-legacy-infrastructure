using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/SwordOfShatteredHopes.cs (CC6 batch 5; a shared drop of
    // FireElementalRenowned and PixieRenowned). Stock GlassSword is its final parent: the B5 carrier regex hits nothing
    // in it and it has no ServUO descendants. ServUO's version-1 fix-up re-applies SplinteringWeapon to old instances;
    // none exist here, so version 0 with no fix-up.
    // Dropped: IsArtifact (D-1); WeaponAttributes.SplinteringWeapon = 20 (D-74: pinned AosWeaponAttribute has no
    // SplinteringWeapon member and no splintering effect; P4's row).
    [SerializationGenerator(0, false)]
    public partial class SwordOfShatteredHopes : GlassSword
    {
        [Constructible]
        public SwordOfShatteredHopes()
        {
            Hue = 91;
            WeaponAttributes.HitDispel = 25;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
            WeaponAttributes.ResistFireBonus = 15;
        }

        public override int LabelNumber => 1112770; // Sword of Shattered Hopes

        public override int ArtifactRarity => 10;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
