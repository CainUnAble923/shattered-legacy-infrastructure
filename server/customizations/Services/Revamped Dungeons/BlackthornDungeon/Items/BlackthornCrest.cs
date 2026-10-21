// cc-P196: the name line of the 136 "bearing the crest of Blackthorn" rewards.
//
// ServUO gives every crest item ReforgedSuffix.Blackthorn, and its BaseClothing / BaseArmor / BaseWeapon /
// BaseJewel AddNameProperty then calls RunicReforging.AddSuffixName (RunicReforging.cs:1732-1742), which for the
// Blackthorn suffix writes one property line: cliloc 1154548 with the item's name string as the argument
// ("~1_ITEM~ bearing the crest of Blackthorn"; the name string is the Name when set, else "#LabelNumber",
// BaseClothing.cs:1016-1030). Pinned ModernUO has no reforging and no ReforgedSuffix on its equipment, so each
// crest item overrides AddNameProperty and calls this instead. Nothing else of ServUO's reforging is needed by
// these items: the suffix carries no attribute, only the name.

namespace Server.Items;

public static class BlackthornCrest
{
    public const int BearingTheCrestOfBlackthorn = 1154548; // ~1_ITEM~ bearing the crest of Blackthorn

    public static void AddName(IPropertyList list, Item item)
    {
        if (item.Name is { Length: > 0 } name)
        {
            list.Add(BearingTheCrestOfBlackthorn, name);
        }
        else
        {
            list.Add(BearingTheCrestOfBlackthorn, $"{item.LabelNumber:#}");
        }
    }
}
