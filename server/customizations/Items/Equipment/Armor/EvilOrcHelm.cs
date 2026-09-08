using ModernUO.Serialization;
using Server.Misc;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/EvilOrcHelm.cs (CC9 batch 3). An OrcHelm that grants +10 str and either
    // -10 int or -10 dex, whichever the wearer has less of, and costs karma to put on.
    //
    // ServUO also overrides UseIntOrDexProperty and IntOrDexPropertyValue. Both are virtuals on ServUO's
    // BaseArmor that nothing in ServUO reads (grep over Scripts/ finds only their declaration and this file),
    // and pinned ModernUO has neither, so they are dropped with no behaviour change. The -10 stays as the
    // constant below. Logged in shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class EvilOrcHelm : OrcHelm
    {
        private const int IntOrDexPropertyValue = -10;

        [Constructible]
        public EvilOrcHelm()
        {
            Hue = 0x96E;
            Attributes.BonusStr = 10;
            Attributes.BonusInt = IntOrDexPropertyValue;
            Attributes.BonusDex = IntOrDexPropertyValue;
        }

        public override int LabelNumber => 1062021; // an evil orc helm

        public override bool OnEquip(Mobile from)
        {
            if (from.RawInt > from.RawDex)
            {
                Attributes.BonusDex = 0;
            }
            else
            {
                Attributes.BonusInt = 0;
            }

            Titles.AwardKarma(from, -22, true);

            return base.OnEquip(from);
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile)
            {
                Attributes.BonusInt = IntOrDexPropertyValue;
                Attributes.BonusDex = IntOrDexPropertyValue;
            }
        }
    }
}
