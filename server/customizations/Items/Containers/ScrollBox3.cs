// cc-P199: ServUO pub57 Scripts/Items/Containers/ScrollBox3.cs. "Reward Scroll Box", a hued wooden box holding an
// Imbuing 105 or 110 power scroll (even odds) and, one time in twenty, a runic mallet and chisel (dull copper 50 uses
// at 25%, shadow iron 45 at 15%, copper 40 at 15%, bronze 35 at 10%, gold 30 at 10%, agapite 25 at 10%, verite 20 at
// 13%, valorite 15 at 2%). In ServUO it is the reward of the quest Secrets of the Soulforge
// (Quests/SecretsoftheSoulforge.cs:13).
//
// No difference from ServUO in what it holds. RunicMalletAndChisel is being ported alongside this batch
// (customizations/Items/Tools/RunicMalletAndChisel.cs, ServUO Items/Tools/RunicMalletAndChisel.cs:32, the
// (CraftResource, int uses) constructor this calls).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ScrollBox3 : WoodenBox
{
    [Constructible]
    public ScrollBox3()
    {
        Movable = true;
        Hue = 1159;

        DropItem(new PowerScroll(SkillName.Imbuing, Utility.RandomBool() ? 105.0 : 110.0));

        if (0.05 >= Utility.RandomDouble())
        {
            DropItem(RandomRunicMalletAndChisel());
        }
    }

    public override string DefaultName => "Reward Scroll Box";

    // ScrollBox3.cs:18-63, the chain of thresholds as ServUO has it.
    public static Item RandomRunicMalletAndChisel()
    {
        var runictype = Utility.RandomDouble();
        CraftResource res;
        int charges;

        if (runictype <= .25)
        {
            res = CraftResource.DullCopper;
            charges = 50;
        }
        else if (runictype <= .40)
        {
            res = CraftResource.ShadowIron;
            charges = 45;
        }
        else if (runictype <= .55)
        {
            res = CraftResource.Copper;
            charges = 40;
        }
        else if (runictype <= .65)
        {
            res = CraftResource.Bronze;
            charges = 35;
        }
        else if (runictype <= .75)
        {
            res = CraftResource.Gold;
            charges = 30;
        }
        else if (runictype <= .85)
        {
            res = CraftResource.Agapite;
            charges = 25;
        }
        else if (runictype <= .98)
        {
            res = CraftResource.Verite;
            charges = 20;
        }
        else
        {
            res = CraftResource.Valorite;
            charges = 15;
        }

        return new RunicMalletAndChisel(res, charges);
    }
}
