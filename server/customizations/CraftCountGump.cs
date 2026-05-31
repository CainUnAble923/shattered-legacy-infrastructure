using System;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Engines.Craft;

// ClusterF: Make X — quantity picker gump
public class CraftCountGump : DynamicGump
{
    private const int LabelColor     = 0x7FFF;
    private const int LabelHue       = 0x480;

    private static readonly int[] Presets = { 5, 10, 20, 50, 100 };

    private readonly Mobile      _from;
    private readonly CraftSystem _craftSystem;
    private readonly CraftItem   _craftItem;
    private readonly BaseTool    _tool;

    public override bool Singleton => true;

    public CraftCountGump(Mobile from, CraftSystem craftSystem, CraftItem craftItem, BaseTool tool)
        : base(40, 40)
    {
        _from        = from;
        _craftSystem = craftSystem;
        _craftItem   = craftItem;
        _tool        = tool;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 230, 230, 9200);
        builder.AddImageTiled(10, 10, 210, 22, 2624);
        builder.AddImageTiled(10, 37, 210, 22, 2624);
        builder.AddImageTiled(10, 64, 210, 130, 2624);
        builder.AddAlphaRegion(10, 10, 210, 210);

        // Title
        builder.AddLabel(70, 12, LabelHue, "Make How Many?");

        // Item name
        if (_craftItem.NameNumber > 0)
            builder.AddHtmlLocalized(10, 39, 210, 20, _craftItem.NameNumber, LabelColor);
        else
            builder.AddLabel(15, 39, LabelHue, _craftItem.NameString);

        // Preset buttons (vertical list)
        for (var i = 0; i < Presets.Length; i++)
        {
            builder.AddButton(15, 68 + i * 22, 4005, 4007, 10 + i);
            builder.AddLabel(50, 70 + i * 22, LabelHue, $"{Presets[i]} items");
        }

        // Custom count row
        builder.AddLabel(15, 182, LabelHue, "Custom:");
        builder.AddBackground(72, 178, 60, 22, 9350);
        builder.AddTextEntry(75, 180, 54, 18, LabelHue, 0, "");
        builder.AddButton(142, 178, 4005, 4007, 1);
        builder.AddLabel(164, 180, LabelHue, "Make");

        // Cancel
        builder.AddButton(15, 205, 4014, 4016, 0);
        builder.AddHtmlLocalized(50, 208, 100, 18, 1011012, LabelColor); // CANCEL
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (info.ButtonID == 0)
        {
            // Cancel — return to item detail gump
            from.SendGump(new CraftGumpItem(from, _craftSystem, _craftItem, _tool));
            return;
        }

        int count;

        if (info.ButtonID == 1)
        {
            // Custom text entry
            var text = info.GetTextEntry(0)?.Trim() ?? "";
            if (!int.TryParse(text, out count) || count < 1)
            {
                from.SendMessage(0x20, "Please enter a valid number (1-200).");
                from.SendGump(new CraftCountGump(from, _craftSystem, _craftItem, _tool));
                return;
            }
        }
        else if (info.ButtonID >= 10 && info.ButtonID <= 14)
        {
            count = Presets[info.ButtonID - 10];
        }
        else
        {
            from.SendGump(new CraftCountGump(from, _craftSystem, _craftItem, _tool));
            return;
        }

        count = Math.Clamp(count, 1, 200);

        var num = _craftSystem.CanCraft(from, _tool, _craftItem.ItemType);
        if (num > 0)
        {
            from.SendGump(new CraftGump(from, _craftSystem, _tool, num));
            return;
        }

        Type typeRes = null;
        var context = _craftSystem.GetContext(from);

        if (context != null)
        {
            var res      = _craftItem.UseSubRes2 ? _craftSystem.CraftSubRes2 : _craftSystem.CraftSubRes;
            var resIndex = _craftItem.UseSubRes2 ? context.LastResourceIndex2 : context.LastResourceIndex;

            if (resIndex >= 0 && resIndex < res.Count)
                typeRes = res.GetAt(resIndex).ItemType;

            // ClusterF: Make X — arm repeat state; first craft counts as #1
            context.RepeatCount   = count - 1;
            context.RepeatItem    = _craftItem;
            context.RepeatTypeRes = typeRes;
        }

        _craftSystem.CreateItem(from, _craftItem.ItemType, typeRes, _tool, _craftItem);
    }
}
