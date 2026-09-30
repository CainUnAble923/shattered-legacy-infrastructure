using System;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Craft;

// ClusterF: Make X, the quantity picker. cc-P18 (F-1): Craft X counts finished items (default) or attempts, can keep
// going until X exceptional items, and is capped (ClusterFCraftRun). The toggles are remembered in the CraftContext.
// Button ids are kept from the first make-X (0 cancel, 1 custom, 10-14 presets).
public class CraftCountGump : DynamicGump
{
    private const int LabelColor     = 0x7FFF;
    private const int LabelHue       = 0x480;

    public const int SwItems       = 100;
    public const int SwAttempts    = 101;
    public const int SwExceptional = 102;

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
        var context     = _craftSystem.GetContext(_from);
        var attempts    = context?.MakeXCountAttempts == true;
        var canExc      = ClusterFCraftRun.CanBeExceptional(_craftItem);
        var exceptional = canExc && context?.MakeXExceptionalOnly == true;

        builder.AddPage();
        builder.AddBackground(0, 0, 260, 330, 9200);
        builder.AddAlphaRegion(10, 10, 240, 310);

        // Title
        builder.AddLabel(80, 12, LabelHue, "Make How Many?");

        // Item name
        if (_craftItem.NameNumber > 0)
            builder.AddHtmlLocalized(15, 36, 230, 20, _craftItem.NameNumber, LabelColor);
        else
            builder.AddLabel(15, 36, LabelHue, _craftItem.NameString);

        // Count mode
        builder.AddGroup(1);
        builder.AddRadio(15, 62, 9720, 9723, !attempts, SwItems);
        builder.AddLabel(50, 66, LabelHue, $"Count items made (up to {ClusterFCraftRun.ItemsCap})");
        builder.AddRadio(15, 92, 9720, 9723, attempts, SwAttempts);
        builder.AddLabel(50, 96, LabelHue, $"Count attempts (up to {ClusterFCraftRun.AttemptsCap})");

        // Exceptional only, where the item can be exceptional
        if (canExc)
        {
            builder.AddCheckbox(15, 122, 9720, 9723, exceptional, SwExceptional);
            builder.AddLabel(50, 126, LabelHue, "Exceptional only (items mode)");
        }
        else
        {
            builder.AddLabel(50, 126, 0x3B2, "This item cannot be exceptional.");
        }

        // Preset buttons
        for (var i = 0; i < Presets.Length; i++)
        {
            builder.AddButton(15, 156 + i * 22, 4005, 4007, 10 + i);
            builder.AddLabel(50, 158 + i * 22, LabelHue, $"{Presets[i]}");
        }

        // Custom count row
        builder.AddLabel(15, 272, LabelHue, "Custom:");
        builder.AddBackground(72, 268, 60, 22, 9350);
        builder.AddTextEntry(75, 270, 54, 18, LabelHue, 0, "");
        builder.AddButton(142, 268, 4005, 4007, 1);
        builder.AddLabel(177, 270, LabelHue, "Make");

        // Cancel
        builder.AddButton(15, 298, 4014, 4016, 0);
        builder.AddHtmlLocalized(50, 300, 100, 18, 1011012, LabelColor); // CANCEL
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (info.ButtonID == 0)
        {
            // Cancel - return to item detail gump
            from.SendGump(new CraftGumpItem(from, _craftSystem, _craftItem, _tool));
            return;
        }

        var countAttempts = info.IsSwitched(SwAttempts);
        var exceptionalOnly = !countAttempts && info.IsSwitched(SwExceptional) &&
                              ClusterFCraftRun.CanBeExceptional(_craftItem);

        var context = _craftSystem.GetContext(from);

        if (context != null)
        {
            context.MakeXCountAttempts = countAttempts;
            context.MakeXExceptionalOnly = info.IsSwitched(SwExceptional);
        }

        int count;

        if (info.ButtonID == 1)
        {
            // Custom text entry. A number over the cap is lowered to the cap by the run (Chase, 2026-09-30).
            var text = info.GetTextEntry(0)?.Trim() ?? "";
            if (!int.TryParse(text, out count) || count < 1)
            {
                from.SendMessage(0x20, "Please enter a number of 1 or more.");
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

        var num = _craftSystem.CanCraft(from, _tool, _craftItem.ItemType);
        if (num > 0)
        {
            from.SendGump(new CraftGump(from, _craftSystem, _tool, num));
            return;
        }

        Type typeRes = null;

        if (context != null)
        {
            var res      = _craftItem.UseSubRes2 ? _craftSystem.CraftSubRes2 : _craftSystem.CraftSubRes;
            var resIndex = _craftItem.UseSubRes2 ? context.LastResourceIndex2 : context.LastResourceIndex;

            if (resIndex >= 0 && resIndex < res.Count)
                typeRes = res.GetAt(resIndex).ItemType;
        }

        if (exceptionalOnly)
        {
            // Rejects not recycled on the spot go to a bag the player picks (ClusterFCraftRejects).
            from.SendMessage(0x44, "Target a bag in your backpack for the rejects, or your backpack. Esc leaves them where they are made.");
            from.Target = new RejectsBagTarget(_craftSystem, _craftItem, _tool, typeRes, count);
            return;
        }

        ClusterFCraftRun.Begin(from, _craftSystem, _craftItem, _tool, typeRes, count, countAttempts, false, null);
    }

    public class RejectsBagTarget : Target
    {
        private readonly CraftSystem _system;
        private readonly CraftItem   _item;
        private readonly BaseTool    _tool;
        private readonly Type        _typeRes;
        private readonly int         _count;

        public RejectsBagTarget(CraftSystem system, CraftItem item, BaseTool tool, Type typeRes, int count)
            : base(-1, false, TargetFlags.None)
        {
            _system  = system;
            _item    = item;
            _tool    = tool;
            _typeRes = typeRes;
            _count   = count;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Container bag || from.Backpack == null ||
                bag != from.Backpack && !bag.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "That must be a bag in your backpack, or your backpack.");
                from.Target = new RejectsBagTarget(_system, _item, _tool, _typeRes, _count);
                return;
            }

            ClusterFCraftRun.Begin(from, _system, _item, _tool, _typeRes, _count, false, true, bag);
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            if (cancelType == TargetCancelType.Canceled)
            {
                ClusterFCraftRun.Begin(from, _system, _item, _tool, _typeRes, _count, false, true, null);
            }
        }
    }
}
