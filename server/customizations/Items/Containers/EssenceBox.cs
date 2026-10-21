// cc-P199: ServUO pub57 Scripts/Items/Containers/EssenceBox.cs. "Essence Box" (1113770), a hued wooden box holding one
// random imbuing essence. In ServUO it is the reward of the quest Unusual Goods (Quests/UnusualGoods.cs:14).
//
// ServUO draws the essence with Loot.RandomEssence (Misc/Loot.cs:430-433), which pinned's Loot does not have; the
// eleven essence types of its list (Loot.cs:222-227) are copied here, all of them ours (customizations/Items/Resource).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EssenceBox : WoodenBox
{
    private static readonly Type[] _imbuingEssenceIngreds =
    {
        typeof(EssencePrecision), typeof(EssenceAchievement), typeof(EssenceBalance), typeof(EssenceControl),
        typeof(EssenceDiligence), typeof(EssenceDirection), typeof(EssenceFeeling), typeof(EssenceOrder),
        typeof(EssencePassion), typeof(EssencePersistence), typeof(EssenceSingularity)
    };

    [Constructible]
    public EssenceBox()
    {
        Movable = true;
        Hue = 2306;

        DropItem(RandomEssence());
    }

    public override int LabelNumber => 1113770; // Essence Box

    public static Item RandomEssence() => Loot.Construct(_imbuingEssenceIngreds);
}
