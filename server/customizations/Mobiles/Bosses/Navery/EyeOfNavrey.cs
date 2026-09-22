// ServUO: Mobiles/Bosses/Navery/EyeOfNavrey.cs (CC6 batch 8, Part C). The proof item of Vernix's Green With Envy
// quest, which Navrey hands to each looting-rights holder on that quest. The quest is S5's engine (Q-025) and is not
// here, so nothing produces this item yet (D-84); it is ported beside her so the hand-out is one line when it is.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EyeOfNavrey : Item
{
    [Constructible]
    public EyeOfNavrey() : base(0x318D)
    {
        Weight = 1;
        Hue = 68;
        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1095154; // Eye of Navrey Night-Eyes
}
