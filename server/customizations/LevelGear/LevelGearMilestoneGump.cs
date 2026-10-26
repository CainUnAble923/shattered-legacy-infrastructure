// cc-P633: a levelable weapon's milestone page (from [ItemLevel) and its confirmation. The page shows the milestone's
// level and price, each effect with its chance, damage, cooldown and PvE limit, and why a choice cannot be bought now;
// after a purchase it shows the bound effect and the owner's shared cooldown. A choice opens the confirmation; only the
// confirmation buys, through ClusterFLevelGearProcs.Buy, which reads every rule again (never the gump's earlier state).

#nullable enable

using Server.Gumps;
using Server.Network;

namespace Server;

public class LevelGearMilestoneGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int ValueHue = 0x7FF;
    private const int GreyHue = 0x3B2;
    private const int ChooseBase = 10;
    private const int BackButton = 1;

    private readonly Mobile _from;
    private readonly Item _item;

    public override bool Singleton => true;

    public LevelGearMilestoneGump(Mobile from, Item item) : base(60, 40)
    {
        _from = from;
        _item = item;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var s = LevelGearStore.Of(_item);
        var height = 120 + LevelGearProc.All.Length * 48;
        builder.AddPage();
        builder.AddBackground(0, 0, 440, height, 9200);
        builder.AddAlphaRegion(10, 10, 420, height - 20);
        builder.AddLabel(20, 14, LabelHue, $"Weapon milestone: level {LevelGearProcSettings.UnlockLevel}");

        if (s == null)
        {
            builder.AddLabel(20, 40, GreyHue, "This item is no longer levelable.");
            return;
        }

        var chance = $"{LevelGearProcSettings.ChanceBasisPoints / 100.0:0.##}%";
        var cooldown = $"{LevelGearProcSettings.CooldownSeconds}s";

        if (s.Proc is { } p)
        {
            var name = LevelGearProc.ByKey(p.Key)?.Name ?? p.Key;
            builder.AddLabel(20, 40, ValueHue, $"Bound: {name} (bought at item level {p.PurchaseLevel} for {p.PaidCost} points)");
            builder.AddLabel(20, 62, ValueHue, $"{chance} on a damaging weapon hit; {cooldown} cooldown shared with your companions; PvE");
            var left = ClusterFLevelGearProcs.OwnerCooldown(_from);
            builder.AddLabel(20, 84, ValueHue, left > System.TimeSpan.Zero ? $"Your cooldown: {left.TotalSeconds:0.#}s" : "Your cooldown: ready");
        }
        else
        {
            builder.AddLabel(20, 40, ValueHue,
                $"Choose one effect. Cost: {LevelGearProcSettings.PointCost} item points and 1 property slot. Points: {s.Points}");
            builder.AddLabel(20, 62, ValueHue, $"{chance} on a damaging weapon hit, {cooldown} cooldown shared with your companions, PvE only.");

            var y = 92;
            for (var i = 0; i < LevelGearProc.All.Length; i++)
            {
                var proc = LevelGearProc.All[i];
                var why = ClusterFLevelGearProcs.WhyNotBuy(_from, _item, proc);
                if (why == null)
                {
                    builder.AddButton(20, y, 4005, 4007, ChooseBase + i);
                }

                var hue = why == null ? ValueHue : GreyHue;
                builder.AddLabel(50, y + 2, hue, $"{proc.Name}: {LevelGearProcSettings.RawDamage(proc)} {proc.ElementName} damage");
                builder.AddLabel(50, y + 22, GreyHue, why ?? "Permanent choice.");
                y += 48;
            }
        }

        builder.AddButton(20, height - 34, 4014, 4016, BackButton);
        builder.AddLabel(55, height - 32, LabelHue, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (!ClusterFLevelGear.Holds(_from, _item) || !ClusterFLevelGear.IsLevelable(_item))
        {
            if (info.ButtonID != 0)
            {
                _from.SendMessage("You no longer have that item.");
            }

            return;
        }

        if (info.ButtonID == BackButton)
        {
            _from.SendGump(new LevelGearGump(_from, _item));
            return;
        }

        var index = info.ButtonID - ChooseBase;
        if (index >= 0 && index < LevelGearProc.All.Length)
        {
            _from.SendGump(new LevelGearMilestoneConfirmGump(_from, _item, LevelGearProc.All[index].Key));
        }
    }
}

public class LevelGearMilestoneConfirmGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int ValueHue = 0x7FF;
    private const int ConfirmButton = 1;
    private const int CancelButton = 2;

    private readonly Mobile _from;
    private readonly Item _item;
    private readonly string _key;

    public override bool Singleton => true;

    public LevelGearMilestoneConfirmGump(Mobile from, Item item, string key) : base(80, 60)
    {
        _from = from;
        _item = item;
        _key = key;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var name = LevelGearProc.ByKey(_key)?.Name ?? _key;
        var points = LevelGearStore.Of(_item)?.Points ?? 0;
        builder.AddPage();
        builder.AddBackground(0, 0, 440, 150, 9200);
        builder.AddAlphaRegion(10, 10, 420, 130);
        builder.AddLabel(20, 16, LabelHue, $"Bind {name} to this weapon for {LevelGearProcSettings.PointCost} item points?");
        builder.AddLabel(20, 38, ValueHue, "This choice is permanent until a paid respec is available.");
        builder.AddLabel(20, 60, ValueHue,
            $"Points: {points}   Properties: {ClusterFLevelGear.SlotsUsed(_item)} of {ClusterFLevelGear.SlotLimit}");
        builder.AddButton(20, 100, 4005, 4007, ConfirmButton);
        builder.AddLabel(55, 102, LabelHue, "Bind it");
        builder.AddButton(200, 100, 4017, 4019, CancelButton);
        builder.AddLabel(235, 102, LabelHue, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == CancelButton)
        {
            if (ClusterFLevelGear.Holds(_from, _item) && ClusterFLevelGear.IsLevelable(_item))
            {
                _from.SendGump(new LevelGearMilestoneGump(_from, _item));
            }

            return;
        }

        if (info.ButtonID != ConfirmButton)
        {
            return;
        }

        var proc = LevelGearProc.ByKey(_key);
        if (ClusterFLevelGearProcs.Buy(_from, _item, proc) is { } why)
        {
            _from.SendMessage(0x22, why);
            return;
        }

        _from.SendMessage(0x59, $"{proc!.Name} is bound to your weapon.");
        _from.SendGump(new LevelGearMilestoneGump(_from, _item));
    }
}
