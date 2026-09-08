using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishWarHammer.cs (CC9 batch 3). Based Off War Hammer. ServUO flips to 0x481, a typo (that is a Prism of Light pillar graphic); every other gargish weapon flips to itemID+1, so this uses 0x48C1. Logged as a deviation.
    [Flippable(0x48C0, 0x48C1)]
    [SerializationGenerator(0, false)]
    public partial class GargishWarHammer : BaseBashing
    {
        [Constructible]
        public GargishWarHammer() : base(0x48C0) => Layer = Layer.TwoHanded;

        public override double DefaultWeight => 10.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.WhirlwindAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.CrushingBlow;

        public override int AosStrengthReq => 95;
        public override int AosMinDamage => 17;
        public override int AosMaxDamage => 20;
        public override int AosSpeed => 28;
        public override float MlSpeed => 3.75f;

        public override int OldStrengthReq => 40;
        public override int OldMinDamage => 8;
        public override int OldMaxDamage => 36;
        public override int OldSpeed => 31;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 110;

        public override WeaponAnimation DefAnimation => WeaponAnimation.Bash2H;
    }
}
