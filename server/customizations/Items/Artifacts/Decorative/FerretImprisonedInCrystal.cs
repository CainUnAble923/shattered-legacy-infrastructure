using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/FerretImprisonedInCrystal.cs (CC9 batch 5). Pinned ModernUO ships
    // BaseImprisonedMobile (Items/Special/ML/BaseImprisonedMobile.cs) with the same double-click / confirm-gump
    // release flow, so only the crystal and its summon are ported.
    [SerializationGenerator(0, false)]
    public partial class FerretImprisonedInCrystal : BaseImprisonedMobile
    {
        [Constructible]
        public FerretImprisonedInCrystal() : base(0x1F19)
        {
        }

        public override string DefaultName => "a ferret imprisoned in a crystal";
        public override double DefaultWeight => 1.0;

        public override BaseCreature Summon => new ShimmeringFerret();
    }
}

namespace Server.Mobiles
{
    // The crystal's summon: a stock Ferret with three skills at 100, deleted on release, tagged (summoned).
    [SerializationGenerator(0, false)]
    public partial class ShimmeringFerret : Ferret
    {
        [Constructible]
        public ShimmeringFerret()
        {
            SetSkill(SkillName.MagicResist, 100.0);
            SetSkill(SkillName.Tactics, 100.0);
            SetSkill(SkillName.Wrestling, 100.0);
        }

        public override bool DeleteOnRelease => true;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1049646); // (summoned)
        }
    }
}
