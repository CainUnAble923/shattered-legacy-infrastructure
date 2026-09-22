// ServUO: Items/Artifacts/Equipment/Clothing/Tangle1.cs (CC6 batch 8, Part C). "Tangle", one of Navrey Night-Eyes'
// two artifacts (with NightEyes, CC9). ServUO names the class Tangle1 because Tangle is the Blighted Grove creature;
// pinned has the same creature under the same name, so the same reason applies here.
//
// Dropped: IsArtifact (D-1); [Alterable(typeof(DefTailoring), typeof(GargishTangle1))] (D-20: pinned has no alter-item
// system). GargishTangle1 itself is not ported: its base, GargoyleHalfApron, does not exist in pinned (no gargish
// half apron at all), and with D-20 the only route to it would have been [add.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Tangle1 : HalfApron
{
    [Constructible]
    public Tangle1()
    {
        Hue = 506;
        Attributes.BonusInt = 10;
        Attributes.DefendChance = 5;
        Attributes.RegenMana = 2;
    }

    public override int LabelNumber => 1114784; // Tangle
}
