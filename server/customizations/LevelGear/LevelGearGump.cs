// cc-P261: the levelable item's gump ([ItemLevel): level, XP, unspent points, and one row per property the item may
// take, with what a level has bought, the most it may buy here and the price of +1. Every reply is checked again: the
// player still holds the item, the button is a row of this item's list, and ClusterFLevelGear.Buy re-checks the rules
// (P72 A.6: XmlLevelItems' gump trusted the button id and never re-checked the item).

#nullable enable

using Server.Gumps;
using Server.Network;

namespace Server;

public class LevelGearGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int ValueHue = 0x7FF;
    private const int GreyHue = 0x3B2;
    private const int BuyBase = 100;

    private readonly Mobile _from;
    private readonly Item _item;

    public override bool Singleton => true;

    public LevelGearGump(Mobile from, Item item) : base(60, 40)
    {
        _from = from;
        _item = item;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var s = LevelGearStore.Of(_item);
        var rows = 0;
        foreach (var a in LevelGearAttribute.All)
        {
            if (a.AppliesTo(_item))
            {
                rows++;
            }
        }

        var height = 130 + rows * 22;
        builder.AddPage();
        builder.AddBackground(0, 0, 420, height, 9200);
        builder.AddAlphaRegion(10, 10, 400, height - 20);

        builder.AddLabel(20, 14, LabelHue, _item.Name ?? _item.GetType().Name);
        if (s == null)
        {
            builder.AddLabel(20, 40, GreyHue, "This item is no longer levelable.");
            return;
        }

        var maxLevel = System.Math.Min(s.MaxLevel, LevelGearSettings.MaxLevel);
        var next = s.Level >= maxLevel ? "max level" : $"{ClusterFLevelGear.XpForLevel(s.Level + 1):N0} XP for level {s.Level + 1}";
        builder.AddLabel(20, 36, ValueHue, $"Level {s.Level} of {maxLevel}   {s.Xp:N0} XP ({next})");
        builder.AddLabel(20, 58, ValueHue,
            $"Points to spend: {s.Points}   Properties: {ClusterFLevelGear.SlotsUsed(_item)} of {ClusterFLevelGear.SlotLimit}");

        builder.AddLabel(50, 84, LabelHue, "Property");
        builder.AddLabel(230, 84, LabelHue, "Bought");
        builder.AddLabel(290, 84, LabelHue, "Most");
        builder.AddLabel(345, 84, LabelHue, "Cost");

        var y = 106;
        for (var i = 0; i < LevelGearAttribute.All.Length; i++)
        {
            var a = LevelGearAttribute.All[i];
            if (!a.AppliesTo(_item))
            {
                continue;
            }

            var canBuy = ClusterFLevelGear.WhyNotBuy(_item, a) == null;
            if (canBuy)
            {
                builder.AddButton(20, y, 4005, 4007, BuyBase + i);
            }

            var hue = canBuy ? ValueHue : GreyHue;
            builder.AddLabel(50, y + 2, hue, a.Name);
            builder.AddLabel(230, y + 2, hue, $"+{ClusterFLevelGear.EffectiveBonus(_item, a)}");
            builder.AddLabel(290, y + 2, hue, $"{a.MaxBuy(_item)}");
            builder.AddLabel(345, y + 2, hue, $"{LevelGearSettings.CostOf(a)}");
            y += 22;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var index = info.ButtonID - BuyBase;
        if (index < 0 || index >= LevelGearAttribute.All.Length)
        {
            return;
        }

        if (!ClusterFLevelGear.Holds(_from, _item) || !ClusterFLevelGear.IsLevelable(_item))
        {
            _from.SendMessage("You no longer have that item.");
            return;
        }

        var a = LevelGearAttribute.All[index];
        if (ClusterFLevelGear.Buy(_item, a) is { } why)
        {
            _from.SendMessage(0x22, why);
        }

        _from.SendGump(new LevelGearGump(_from, _item));
    }
}
