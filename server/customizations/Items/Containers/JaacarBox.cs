// cc-P199: ServUO pub57 Scripts/Items/Containers/JaacarBox.cs. "Jaacar Reward Box", a hued wooden box holding recipe
// scroll 500. In ServUO it is the reward of the quest Bad Company (Quests/BadCompany.cs:14, "Bowl of Rotworm Stew
// Recipe"); 500 is ServUO's CookRecipes.RotWormStew (Services/Craft/DefCooking.cs:10).
//
// No difference from ServUO in the box. Note: neither pinned nor ours registers recipe 500 (pinned's DefCooking has no
// recipes), so the scroll shows no recipe name and its double-click does nothing (pinned RecipeScroll.cs:61-64) until
// the Rotworm Stew recipe is ported.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class JaacarBox : WoodenBox
{
    [Constructible]
    public JaacarBox()
    {
        Movable = true;
        Hue = 1266;

        DropItem(new RecipeScroll(500));
    }

    public override string DefaultName => "Jaacar Reward Box";
}
