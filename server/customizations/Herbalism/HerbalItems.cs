// HerbalItems.cs
//
// cc-P804 (F-51, phase 1): the items of Herbalism. Notes: shard-migration notes/cc-P804-herbalism-phase-1.md.
//
//   ReagentSeed       a Seed that carries its strain tier
//   RawHerb           picked or harvested herbs, stackable by herb; double-click to process
//   ReagentBundle     processed herbs; double-click to open into reagents
//   HerbPatch         a wild patch; picked, it hides and comes back on a timer
//   AlembicHerbTable  the Order's starter: a bowl and seeds once per character, and a bed deed for gold
//
// Every item uses the source generator (D40). Wild patches and the table appear on an existing world at load, by key, idempotent.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Plants;

[SerializationGenerator(0, false)]
public partial class ReagentSeed : Seed
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _strainTier;

    [Constructible]
    public ReagentSeed() : this(PlantType.Garlic, 0)
    {
    }

    public ReagentSeed(PlantType type, int strainTier) : base(type, PlantHue.Plain, true)
    {
        _strainTier = Math.Clamp(strainTier, 0, HerbalLane.TopStrain);
        Hue = HerbalLane.StrainHue(_strainTier);
    }

    public string Words => $"{HerbalLane.StrainName(_strainTier)} {HerbalLane.HerbOf(PlantType)?.Name ?? "herb"} seed";

    public override void AddNameProperty(IPropertyList list) => list.Add(Amount > 1 ? $"{Amount} {Words}s" : $"a {Words}");

    public override void OnSingleClick(Mobile from) => LabelTo(from, Amount > 1 ? $"{Amount} {Words}s" : $"a {Words}");

    public override bool StackWith(Mobile from, Item dropped, bool playSound) =>
        dropped is ReagentSeed other && other.StrainTier == _strainTier && base.StackWith(from, other, playSound);
}

[SerializationGenerator(0, false)]
public partial class RawHerb : Item
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _herbKey;

    [Constructible]
    public RawHerb() : this("Garlic")
    {
    }

    public RawHerb(string herbKey, int amount = 1) : base(HerbalismSettings.Current.Herb(herbKey)?.RawArt ?? 0xF84)
    {
        _herbKey = herbKey;
        Stackable = true;
        Amount = Math.Max(1, amount);
        Hue = HerbalismSettings.Current.Herb(herbKey)?.RawHue ?? 0;
    }

    public HerbRow Herb => HerbalismSettings.Current.Herb(_herbKey);

    public override double DefaultWeight => 0.1;

    public override string DefaultName => $"raw {Herb?.Name ?? "herb"}";

    public override bool StackWith(Mobile from, Item dropped, bool playSound) =>
        dropped is RawHerb other && other.HerbKey == _herbKey && base.StackWith(from, other, playSound);

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042664); // You must have the object in your backpack to use it.
            return;
        }

        Process(from);
    }

    /// <summary>Every whole lot of this stack into bundles, with a Herbalism gain. Returns the bundles made.</summary>
    public int Process(Mobile from)
    {
        var s = HerbalismSettings.Current;
        var herb = Herb;

        if (herb == null || herb.Stub)
        {
            return 0;
        }

        if (HerbalLane.Skill(from) < HerbalLane.SkillFloor(herb))
        {
            from.SendMessage(0x22, $"You need {HerbalLane.SkillFloor(herb)} Herbalism to process {herb.Name}.");
            return 0;
        }

        var bundles = Amount / s.HerbsPerBundle;

        if (bundles <= 0)
        {
            from.SendMessage(0x22, $"It takes {s.HerbsPerBundle} raw {herb.Name} to make a bundle.");
            return 0;
        }

        var bundle = new ReagentBundle(herb.Key, HerbalLane.ReagentsInBundle(HerbalLane.Skill(from))) { Amount = bundles };

        if (!from.PlaceInBackpack(bundle))
        {
            bundle.Delete();
            from.SendLocalizedMessage(500720); // You don't have enough room in your backpack!
            return 0;
        }

        Consume(bundles * s.HerbsPerBundle);
        from.PlaySound(0x242);
        from.SendMessage(0x44, $"You work the {herb.Name} into {bundles} bundle{(bundles == 1 ? "" : "s")}.");
        var (min, max) = HerbalLane.GainWindow(herb, 0);
        from.CheckSkill(SkillName.Herbalism, min, max);
        return bundles;
    }
}

[SerializationGenerator(0, false)]
public partial class ReagentBundle : Item
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _herbKey;

    [InvalidateProperties]
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _reagents;

    [Constructible]
    public ReagentBundle() : this("Garlic", 10)
    {
    }

    public ReagentBundle(string herbKey, int reagents) : base(0x1A9C)
    {
        _herbKey = herbKey;
        _reagents = reagents;
        Stackable = true;
        Hue = HerbalismSettings.Current.Herb(herbKey)?.RawHue ?? 0;
    }

    public HerbRow Herb => HerbalismSettings.Current.Herb(_herbKey);

    public override double DefaultWeight => 1.0;

    public override string DefaultName => $"a bundle of {Herb?.Name ?? "herbs"}";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Opens into {_reagents} reagents");
    }

    public override bool StackWith(Mobile from, Item dropped, bool playSound) =>
        dropped is ReagentBundle other && other.HerbKey == _herbKey && other.Reagents == _reagents && base.StackWith(from, other, playSound);

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042664); // You must have the object in your backpack to use it.
            return;
        }

        var type = AssemblyHandler.FindTypeByName(Herb?.Reagent ?? "");

        if (type == null || type.CreateInstance<Item>() is not { } reagent)
        {
            return;
        }

        reagent.Amount = _reagents * Amount;

        if (!from.PlaceInBackpack(reagent))
        {
            reagent.Delete();
            from.SendLocalizedMessage(500720); // You don't have enough room in your backpack!
            return;
        }

        from.SendMessage(0x44, $"You open the bundle: {reagent.Amount} reagents.");
        Delete();
    }
}

[SerializationGenerator(0, false)]
public partial class HerbPatch : Item
{
    public static readonly List<HerbPatch> Patches = [];

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _herbKey;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _spotKey;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _ripeAt;

    [Constructible]
    public HerbPatch() : this("Garlic", "")
    {
    }

    public HerbPatch(string herbKey, string spotKey) : base(HerbalismSettings.Current.Herb(herbKey)?.PatchArt ?? 0x18E1)
    {
        _herbKey = herbKey;
        _spotKey = spotKey;
        Movable = false;
        Patches.Add(this);
    }

    public HerbRow Herb => HerbalismSettings.Current.Herb(_herbKey);

    public override string DefaultName => $"wild {Herb?.Name ?? "herbs"}";

    public bool Ripe => Visible;

    public override void OnDoubleClick(Mobile from) => Pick(from);

    /// <summary>One try. Picked herbs go to the pack, the find goes in the ledger, and the patch hides until it regrows.</summary>
    public bool Pick(Mobile from, bool? force = null)
    {
        var s = HerbalismSettings.Current;
        var herb = Herb;

        if (herb == null || !Ripe)
        {
            return false;
        }

        if (!from.InRange(GetWorldLocation(), 2) || !from.InLOS(this))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3E9, 1019045); // I can't reach that.
            return false;
        }

        var floor = HerbalLane.SkillFloor(herb);

        if (HerbalLane.Skill(from) < floor)
        {
            from.SendMessage(0x22, $"You don't know how to pick {herb.Name}. It takes {floor} Herbalism.");
            return false;
        }

        var (min, max) = HerbalLane.GainWindow(herb, 0);
        var ok = from.CheckSkill(SkillName.Herbalism, min - 25, max);

        if (force ?? ok)
        {
            var amount = Utility.RandomMinMax(s.ForageMin, s.ForageMax);
            from.AddToBackpack(new RawHerb(herb.Key, amount));
            HerbalLane.LogFind(from, herb.Key, amount);
            from.SendMessage(0x44, $"You pick {amount} {herb.Name}.");

            if (herb.Grown && Enum.TryParse<PlantType>(herb.Key, out var type) && Utility.Random(100) < s.SeedChancePercent)
            {
                from.AddToBackpack(new ReagentSeed(type, 0));
                from.SendMessage(0x44, "Among the leaves, you find a seed.");
            }

            Visible = false;
            RipeAt = Core.Now + TimeSpan.FromMinutes(s.RespawnMinutes);
            return true;
        }

        from.SendMessage(0x22, $"You damage the {herb.Name} and get nothing usable.");
        return false;
    }

    public static int RespawnDue()
    {
        var count = 0;

        foreach (var patch in Patches)
        {
            if (!patch.Visible && !patch.Deleted && Core.Now >= patch.RipeAt)
            {
                patch.Visible = true;
                count++;
            }
        }

        return count;
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Patches.Add(this);

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        Patches.Remove(this);
    }
}

public static class HerbalismSystem
{
    public const string StarterFlag = "herbalism.starter";

    private static string StarterKey(Mobile m) => $"character:{(uint)m.Serial}:{StarterFlag}";

    public static void Configure()
    {
        foreach (var line in HerbalismSettings.LoadOrWriteDefaults(HerbalismSettings.FilePath))
        {
            Console.WriteLine($"[Herbalism] {line}");
        }

        CommandSystem.Register("Herbalism", AccessLevel.Player, OnCommand);

        // Items made during the load reach World.Items only after it.
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
            {
                Console.WriteLine($"[Herbalism] {SeedWorld()}");
                Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), () => HerbPatch.RespawnDue());
            }
        );
    }

    /// <summary>Places what is missing and nothing else: a patch per spot, by key, and one table.</summary>
    public static string SeedWorld()
    {
        var s = HerbalismSettings.Current;
        var have = new HashSet<string>();

        foreach (var patch in HerbPatch.Patches)
        {
            have.Add(patch.SpotKey);
        }

        var placed = 0;
        var blocked = new List<string>();

        foreach (var spot in s.Patches)
        {
            if (have.Contains(spot.Key))
            {
                continue;
            }

            var map = Map.Parse(spot.Map);

            if (!FindGround(map, spot.X, spot.Y, out var at))
            {
                blocked.Add(spot.Key);
                continue;
            }

            new HerbPatch(spot.Herb, spot.Key).MoveToWorld(at, map);
            placed++;
        }

        var tables = 0;

        foreach (var item in World.Items.Values)
        {
            if (item is AlembicHerbTable)
            {
                tables++;
            }
        }

        if (tables == 0)
        {
            new AlembicHerbTable().MoveToWorld(new Point3D(s.TableX, s.TableY, s.TableZ), Map.Trammel);
        }

        return $"wild patches: {placed} placed, {have.Count} already there, {blocked.Count} with no open ground ({string.Join(", ", blocked)}); herb table {(tables == 0 ? "placed" : "already there")}.";
    }

    /// <summary>The spot, or the nearest open ground within 8 tiles: a tree or a grave on the tile must not lose the patch.</summary>
    public static bool FindGround(Map map, int x, int y, out Point3D at)
    {
        at = default;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        for (var r = 0; r <= 8; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var z = map.GetAverageZ(x + dx, y + dy);

                    if (map.CanFit(x + dx, y + dy, z, 16, true, false))
                    {
                        at = new Point3D(x + dx, y + dy, z);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    [Usage("Herbalism [reload]")]
    [Description("Shows your herb ledger. Staff: 'reload' reads Configuration/herbalism.json again; a bad file is refused whole and the settings in force are kept.")]
    [ShardCommand(CommandCategory.Player, Shard = CommandShard.Any)]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length >= 1 && e.GetString(0).Equals("reload", StringComparison.OrdinalIgnoreCase) && from.AccessLevel >= AccessLevel.GameMaster)
        {
            foreach (var line in HerbalismSettings.Reload(HerbalismSettings.FilePath))
            {
                from.SendMessage(line);
            }

            return;
        }

        from.SendGump(new HerbLedgerGump(from));
    }

    public static bool HadStarter(Mobile m) =>
        m.Account is IAccount acct && ClusterFAccountPersistence.Get(acct)?.FlagValues.ContainsKey(StarterKey(m)) == true;

    /// <summary>The Order's starter: a bowl and each starting herb's seeds, once per character.</summary>
    public static bool GiveStarter(Mobile from, out string message)
    {
        if (HadStarter(from) || from.Account is not IAccount acct)
        {
            message = "The Order has already given you your bowl and seeds.";
            return false;
        }

        ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[StarterKey(from)] = "1";
        from.AddToBackpack(new PlantBowl());
        from.AddToBackpack(new ReagentSeed(PlantType.Garlic, 0) { Amount = HerbalismSettings.Current.StarterSeeds });
        from.AddToBackpack(new ReagentSeed(PlantType.Ginseng, 0) { Amount = HerbalismSettings.Current.StarterSeeds });
        message = "Fill the bowl with dirt, soften it with water and plant a seed. Lock it down at home, or keep it in your pack. " +
                  "Pick wild herbs too: [herbalism keeps your ledger.";
        return true;
    }

    public static bool BuyBedDeed(Mobile from, out string message)
    {
        var fee = HerbalismSettings.Current.BedDeedGold;

        if (from.Backpack?.ConsumeTotal(typeof(Gold), fee) != true && !Banker.Withdraw(from, fee))
        {
            message = $"A raised bed deed costs {fee:N0} gold.";
            return false;
        }

        from.AddToBackpack(new RaisedGardenDeed());
        message = "A raised bed for your house. Plant in every plot, and tend the bed as one.";
        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class AlembicHerbTable : Item
{
    [Constructible]
    public AlembicHerbTable() : base(0x0B7D)
    {
        Movable = false;
    }

    public override string DefaultName => "the Order's herb table";

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 3))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3E9, 1019045); // I can't reach that.
            return;
        }

        from.SendGump(new HerbTableGump(from));
    }
}

public class HerbTableGump : Gump
{
    public const int BtnStarter = 1;
    public const int BtnDeed = 2;

    public HerbTableGump(Mobile from) : base(100, 80)
    {
        AddBackground(0, 0, 420, 230, 9270);
        RequirementStyle.AddSolidPanel(this, 420, 230);
        AddLabel(20, 16, RequirementStyle.HeadingHue, "The Order's herb table");
        AddHtml(20, 42, 380, 90,
            "<BASEFONT COLOR=#AAAAAA>The Order of the Alembic teaches Herbalism. Forage wild herbs, grow your own in a bowl or a raised bed, " +
            "and work the harvest into reagent bundles. Vendors will still sell to you, of course. They hate this.</BASEFONT>", false, false);

        if (!HerbalismSystem.HadStarter(from))
        {
            AddButton(20, 140, 4011, 4012, BtnStarter);
            AddLabel(50, 142, 999, "Take a bowl and seeds (free, once)");
        }

        AddButton(20, 170, 4011, 4012, BtnDeed);
        AddLabel(50, 172, 999, $"Buy a raised bed deed ({HerbalismSettings.Current.BedDeedGold:N0} gold)");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        string message = null;
        var ok = false;

        switch (info.ButtonID)
        {
            case BtnStarter:
                ok = HerbalismSystem.GiveStarter(from, out message);
                break;
            case BtnDeed:
                ok = HerbalismSystem.BuyBedDeed(from, out message);
                break;
        }

        if (message != null)
        {
            from.SendMessage(ok ? 0x44 : 0x22, message);
        }
    }
}

public class HerbLedgerGump : Gump
{
    public HerbLedgerGump(Mobile from) : base(100, 80)
    {
        var herbs = HerbalismSettings.Current.Herbs;
        var h = 90 + herbs.Count * 22;

        AddBackground(0, 0, 380, h, 9270);
        RequirementStyle.AddSolidPanel(this, 380, h);
        AddLabel(20, 16, RequirementStyle.HeadingHue, "Herb ledger");
        AddLabel(20, 40, 999, $"Herbalism {HerbalLane.Skill(from):F1}");

        var y = 66;

        foreach (var herb in herbs)
        {
            if (herb.Stub)
            {
                continue;
            }

            var found = HerbalLane.Found(from, herb.Key);
            AddLabel(20, y, found > 0 ? 0x44 : 999, herb.Name);
            AddLabel(170, y, 999, found > 0 ? $"{found:N0} found" : "not yet found");
            AddLabel(280, y, 999, $"{HerbalLane.SkillFloor(herb)}+ skill");
            y += 22;
        }
    }
}
