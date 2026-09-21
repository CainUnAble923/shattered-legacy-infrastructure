using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/TheImpalersPick.cs (CC6 batch 5; a unique drop of
    // GreenGoblinAlchemistRenowned). Stock HammerPick is its final parent: the B5 carrier regex hits nothing in it and it
    // has no ServUO descendants. [Flippable] is copied because ModernUO reads it with inherit: false (CC9 batch 4).
    // Dropped: IsArtifact (D-1); WeaponAttributes.HitManaDrain = 10 (D-75: pinned AosWeaponAttribute has no
    // HitManaDrain member and no mana-drain hit effect; P4's row).
    [Flippable(0x143D, 0x143C)]
    [SerializationGenerator(0, false)]
    public partial class TheImpalersPick : HammerPick
    {
        [Constructible]
        public TheImpalersPick()
        {
            Hue = 2101;
            Slayer = SlayerName.Repond;
            WeaponAttributes.HitLightning = 40;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 45;
        }

        public override int LabelNumber => 1113822; // The Impaler's Pick

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
