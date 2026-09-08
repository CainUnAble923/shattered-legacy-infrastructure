using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/AxeOfAbandon.cs (CC9).
    //
    // DEVIATION (CC9 D-3): ServUO also sets WeaponAttributes.BattleLust = 1. ModernUO's
    // AosWeaponAttribute enum has no BattleLust and nothing implements the effect, so the line
    // is omitted rather than approximated. Restore it if BattleLust is ever built.
    [Flippable(0xF47, 0xF48)]
    [SerializationGenerator(0, false)]
    public partial class AxeOfAbandon : BattleAxe
    {
        [Constructible]
        public AxeOfAbandon()
        {
            Hue = 556;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.AttackChance = 15;
            Attributes.DefendChance = 10;
            Attributes.CastSpeed = 1;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
        }

        public override int LabelNumber => 1113863; // Axe of Abandon

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
