// cc-P787: the beehive's gump, like the plant gump: stage, health, population, flowers and water (too low, fine, too
// much), ailments, potion effects, and the honey and wax ready. Buttons: pour a potion (a target, as plants do),
// harvest (with a hive tool from the backpack), add a swarm (a target in the backpack), and pack up an empty hive.
// Every reply re-checks the hive, the range and the tender (HouseBeehive's own checks).

#nullable enable

using System;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Beekeeping;

public class BeehiveGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int ValueHue = 0x7FF;
    private const int GoodHue = 0x3F;
    private const int BadHue = 0x22;

    public const int PourButton = 1;
    public const int HarvestButton = 2;
    public const int SwarmButton = 3;
    public const int PackButton = 4;

    private readonly Mobile _from;
    private readonly HouseBeehive _hive;

    public override bool Singleton => true;

    public BeehiveGump(Mobile from, HouseBeehive hive) : base(80, 60)
    {
        _from = from;
        _hive = hive;
    }

    private static string NeedText(HiveNeed need) =>
        need switch { HiveNeed.TooLow => "too low", HiveNeed.TooMuch => "too much", _ => "fine" };

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var t = BeekeepingTable.Current;
        var now = Core.Now;
        var h = _hive;

        builder.AddPage();
        builder.AddBackground(0, 0, 360, 330, 9200);
        builder.AddAlphaRegion(10, 10, 340, 310);
        builder.AddItem(20, 20, HouseBeehive.HiveArt);
        builder.AddLabel(80, 18, LabelHue, "HouseBeehive");

        if (!h.Lived)
        {
            builder.AddLabel(80, 40, ValueHue, "No colony. Bring a bee swarm to start one.");
        }
        else
        {
            builder.AddLabel(80, 40, ValueHue, $"{h.Stage}, day {h.Age}");
        }

        var rows = new System.Collections.Generic.List<(string Label, string Value, int Hue)>();
        void Row(string label, string value, int hue) => rows.Add((label, value, hue));

        if (h.Lived)
        {
            var flowers = h.CountFlowers(now);
            var water = h.CountWater(now);
            var fNeed = HouseBeehive.FlowerNeed(flowers, h.Population, t);
            var wNeed = HouseBeehive.WaterNeed(water, h.Population, t);

            Row("Population", $"{h.Population} of {t.MaxPopulation}", ValueHue);
            Row("Health", $"{h.Health}%", h.Health >= t.GrowHealth ? GoodHue : BadHue);
            Row("Flowers", $"{flowers} ({t.FlowersNeeded(h.Population)} needed): {NeedText(fNeed)}",
                fNeed == HiveNeed.Fine ? GoodHue : BadHue);
            Row("Water", $"{water} ({t.WaterNeeded(h.Population)} needed): {NeedText(wNeed)}",
                wNeed == HiveNeed.Fine ? GoodHue : BadHue);
            Row("Parasites", h.Parasites ? "yes (a Poison potion)" : "none", h.Parasites ? BadHue : GoodHue);
            Row("Disease", h.Disease ? "yes (a Cure potion)" : "none", h.Disease ? BadHue : GoodHue);
            Row("Range", $"{h.RangeAt(now)} tiles{(now < h.AgilityUntil ? " (Agility)" : "")}", ValueHue);
            Row("Hardy", h.Hardy(now) ? "yes (Strength)" : "no", ValueHue);
        }

        Row("Honey ready", $"{h.Honey} of {t.MaxHoney}", ValueHue);
        Row("Raw wax ready", $"{h.Wax} of {t.MaxWax}", ValueHue);
        if (!h.IsLockedInHouse)
        {
            Row("", "Lock it down in a house to keep bees.", BadHue);
        }

        var y = 72;
        foreach (var (label, value, hue) in rows)
        {
            builder.AddLabel(20, y, LabelHue, label);
            builder.AddLabel(150, y, hue, value);
            y += 20;
        }

        if (!h.CanTend(_from))
        {
            return;
        }

        y = Math.Max(y + 6, 270);
        if (h.Lived)
        {
            builder.AddButton(20, y, 4005, 4007, PourButton);
            builder.AddLabel(55, y + 2, LabelHue, "Pour a potion");
        }
        else
        {
            builder.AddButton(20, y, 4005, 4007, SwarmButton);
            builder.AddLabel(55, y + 2, LabelHue, "Add a swarm");
        }

        builder.AddButton(190, y, 4005, 4007, HarvestButton);
        builder.AddLabel(225, y + 2, LabelHue, "Harvest");

        if (!h.Lived && h.Honey == 0 && h.Wax == 0)
        {
            builder.AddButton(190, y + 24, 4005, 4007, PackButton);
            builder.AddLabel(225, y + 26, LabelHue, "Pack up the hive");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = _from;
        var hive = _hive;
        if (hive.Deleted || info.ButtonID == 0 || !from.InRange(hive.GetWorldLocation(), 3) || !hive.CanTend(from))
        {
            return;
        }

        switch (info.ButtonID)
        {
            case PourButton:
                from.SendMessage("Which potion will you pour on the hive?");
                from.Target = new PotionTarget(hive);
                break;
            case HarvestButton:
                if (from.Backpack?.FindItemByType<HiveTool>() is { } tool)
                {
                    hive.Harvest(from, tool);
                }
                else
                {
                    from.SendMessage("You need a hive tool in your backpack to harvest.");
                }

                from.SendGump(new BeehiveGump(from, hive));
                break;
            case SwarmButton:
                if (from.Backpack?.FindItemByType<BeeSwarm>() is { } swarm)
                {
                    hive.AddSwarm(from, swarm);
                }
                else
                {
                    from.SendMessage("You have no bee swarm in your backpack.");
                }

                from.SendGump(new BeehiveGump(from, hive));
                break;
            case PackButton:
                hive.PackUp(from);
                break;
        }
    }

    private class PotionTarget : Target
    {
        private readonly HouseBeehive _hive;

        public PotionTarget(HouseBeehive hive) : base(3, false, TargetFlags.None) => _hive = hive;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_hive.Deleted && targeted is Item item && from.InRange(_hive.GetWorldLocation(), 3))
            {
                _hive.Pour(from, item);
            }
        }

        protected override void OnTargetFinish(Mobile from)
        {
            if (!_hive.Deleted && from.InRange(_hive.GetWorldLocation(), 3))
            {
                from.SendGump(new BeehiveGump(from, _hive));
            }
        }
    }
}
