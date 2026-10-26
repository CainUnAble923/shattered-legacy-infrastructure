// cc-P633: weapon procs bought at the levelable gear milestone, stage 1. Design: shard-migration
// notes/cc-P569-design-procs-levelable-milestone.md; build notes notes/cc-P633-build-procs-milestone.md. Chase (10-25):
// unlock at item level 100 for 100 points, one PvE-only proc per weapon, cooldowns shared by a player and their companions.
//
// A sealed crafted weapon at level 100 may buy one of two effects, Stormstrike (12 energy) or Emberstrike (12 fire), for
// 100 of its points and one property slot. On a damaging weapon hit against a wild creature it has a 5% chance (one
// Random(10000) per eligible strike) to deal that extra damage; a success starts a 10 second cooldown on the owner (a
// player, shared with their companions) and on the weapon itself, so swapping or handing the weapon over clears nothing.
//
// The one combat call is BaseWeapon-level-gear-proc-hit.patch, at the end of OnHit's damageGiven > 0 branch. The choice
// is saved on the weapon's LevelGearStore row (store version 1); the two cooldown maps in their own save file
// (Saves/LevelGearProcs), as absolute UTC end times.
//
// Stage 1 limits (stages 2 and up in the build notes): clusterf.levelGear.procs.enabled is false, and only the test
// shard's [LevelGearProcs on turns it on until restart. There is no acting-level dormancy yet (the F-12 player level
// service it needs is not built): the saved required level is kept for that stage. The imbuing gump does not yet count
// the milestone's slot (the shared budget stage).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Server.Commands;
using Server.Companions;
using Server.Items;
using Server.Mobiles;

namespace Server;

/// <summary>The milestone proc settings (Configuration/modernuo.json), clusterf.levelGear.procs.*.</summary>
public static class LevelGearProcSettings
{
    private const string Prefix = "clusterf.levelGear.procs.";

    /// <summary>The test shard's in-memory switch ([LevelGearProcs on|off), never written to the settings file.</summary>
    public static bool? EnabledOverride { get; set; }

    public static bool Enabled => (EnabledOverride ?? ServerConfiguration.GetOrUpdateSetting(Prefix + "enabled", false)) &&
                                  LevelGearSettings.Enabled && Problem() == null;

    public static int UnlockLevel => ServerConfiguration.GetOrUpdateSetting(Prefix + "unlockLevel", 100);
    public static int PointCost => ServerConfiguration.GetOrUpdateSetting(Prefix + "pointCost", 100);
    public static int ChanceBasisPoints => ServerConfiguration.GetOrUpdateSetting(Prefix + "chanceBasisPoints", 500);
    public static int CooldownSeconds => ServerConfiguration.GetOrUpdateSetting(Prefix + "cooldownSeconds", 10);
    public static int CompanionOwnerRange => ServerConfiguration.GetOrUpdateSetting(Prefix + "companionOwnerRange", 18);

    public static bool ChoiceEnabled(LevelGearProc p) =>
        ServerConfiguration.GetOrUpdateSetting($"{Prefix}{p.Key}.enabled", true);

    public static int RawDamage(LevelGearProc p) =>
        ServerConfiguration.GetOrUpdateSetting($"{Prefix}{p.Key}.rawDamage", 12);

    /// <summary>Why the settings are unusable (the feature is then off), or null.</summary>
    public static string? Problem()
    {
        if (UnlockLevel < 1 || UnlockLevel > LevelGearSettings.MaxLevel)
        {
            return $"unlockLevel {UnlockLevel} is outside 1-{LevelGearSettings.MaxLevel}";
        }

        if (PointCost is < 1 or > 10000)
        {
            return $"pointCost {PointCost} is outside 1-10000";
        }

        if (ChanceBasisPoints is < 0 or > 10000)
        {
            return $"chanceBasisPoints {ChanceBasisPoints} is outside 0-10000";
        }

        if (CooldownSeconds is < 1 or > 3600)
        {
            return $"cooldownSeconds {CooldownSeconds} is outside 1-3600";
        }

        if (CompanionOwnerRange is < 1 or > 24)
        {
            return $"companionOwnerRange {CompanionOwnerRange} is outside 1-24";
        }

        foreach (var p in LevelGearProc.All)
        {
            if (RawDamage(p) is < 1 or > 100)
            {
                return $"{p.Key}.rawDamage {RawDamage(p)} is outside 1-100";
            }
        }

        return null;
    }

    // Not named Configure: the server calls every type's Configure, and ClusterFLevelGearProcs.Configure calls this.
    public static void Materialize()
    {
        _ = UnlockLevel; _ = PointCost; _ = ChanceBasisPoints; _ = CooldownSeconds; _ = CompanionOwnerRange;
        _ = ServerConfiguration.GetOrUpdateSetting(Prefix + "enabled", false);
        foreach (var p in LevelGearProc.All)
        {
            _ = ChoiceEnabled(p);
            _ = RawDamage(p);
        }

        if (Problem() is { } why)
        {
            Console.WriteLine($"Level gear procs: off, the settings are invalid ({why}).");
        }
    }
}

/// <summary>A milestone effect a weapon may buy. Keys are saved; never rename one.</summary>
public sealed class LevelGearProc
{
    public string Key { get; }
    public string Name { get; }
    public ResistanceType Element { get; }

    private LevelGearProc(string key, string name, ResistanceType element)
    {
        Key = key;
        Name = name;
        Element = element;
    }

    public static readonly LevelGearProc Stormstrike = new("stormstrike", "Stormstrike", ResistanceType.Energy);
    public static readonly LevelGearProc Emberstrike = new("emberstrike", "Emberstrike", ResistanceType.Fire);

    public static readonly LevelGearProc[] All = [Stormstrike, Emberstrike];

    public static LevelGearProc? ByKey(string? key) =>
        key == null ? null : Array.Find(All, p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public string ElementName => Element == ResistanceType.Energy ? "energy" : "fire";
}

/// <summary>A weapon's bought milestone effect, as saved when it was bought.</summary>
public sealed record LevelGearProcPurchase(
    string Key, int RequiredLevel, int PurchaseLevel, DateTime PurchasedAt, int PaidCost
);

/// <summary>The owner and weapon cooldowns: absolute UTC end times, in their own save file.</summary>
public class LevelGearProcCooldowns : GenericPersistence
{
    // 0: cc-P633.
    public const int CurrentVersion = 0;

    private static LevelGearProcCooldowns? _instance;

    internal static readonly Dictionary<Serial, DateTime> Owners = new();
    internal static readonly Dictionary<Serial, DateTime> Weapons = new();

    public LevelGearProcCooldowns() : base("LevelGearProcs", 10)
    {
    }

    public static LevelGearProcCooldowns Instance => _instance ??= new LevelGearProcCooldowns();

    public override void Serialize(IGenericWriter writer)
    {
        var now = ClusterFLevelGearProcs.Now;
        writer.WriteEncodedInt(CurrentVersion);
        Write(writer, Owners, now, s => World.FindMobile(s) is { Deleted: false });
        Write(writer, Weapons, now, s => World.FindItem(s) is { Deleted: false });
    }

    private static void Write(IGenericWriter writer, Dictionary<Serial, DateTime> map, DateTime now, Func<Serial, bool> exists)
    {
        var live = new List<KeyValuePair<Serial, DateTime>>();
        foreach (var pair in map)
        {
            if (pair.Value > now && exists(pair.Key))
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(live.Count);
        foreach (var (serial, until) in live)
        {
            writer.Write(serial);
            writer.Write(until);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"LevelGearProcCooldowns version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Read(reader, Owners);
        Read(reader, Weapons);
    }

    private static void Read(IGenericReader reader, Dictionary<Serial, DateTime> map)
    {
        map.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            map[serial] = reader.ReadDateTime();
        }
    }
}

public static class ClusterFLevelGearProcs
{
    // Test seams: the clock and the one roll per eligible strike. Code only; no command reaches them.
    internal static Func<DateTime>? NowOverride;
    internal static Func<int, int> Roll = Utility.Random;

    public static DateTime Now => NowOverride?.Invoke() ?? Core.Now;

    private static bool _inProc;

    public static void Configure()
    {
        _ = LevelGearProcCooldowns.Instance;
        LevelGearProcSettings.Materialize();
        CommandSystem.Register("LevelGearProcs", AccessLevel.GameMaster, OnCommand);
    }

    public static LevelGearProcPurchase? PurchaseOf(Item? item) => LevelGearStore.Of(item)?.Proc;

    // -- Buying -------------------------------------------------------------------------------------------------------

    /// <summary>Whether this item kind may ever take a milestone effect: a weapon, not bare hands.</summary>
    public static bool IsProcWeapon(Item item) => item is BaseWeapon and not Fists;

    /// <summary>Why this player cannot buy this effect on this item now, or null. Every rule is read again here.</summary>
    public static string? WhyNotBuy(Mobile from, Item item, LevelGearProc? proc)
    {
        if (!LevelGearProcSettings.Enabled)
        {
            return "Weapon milestones are not available.";
        }

        if (from is not PlayerMobile { Deleted: false, Alive: true })
        {
            return "Only a living player can bind a milestone.";
        }

        if (!ClusterFLevelGear.Holds(from, item))
        {
            return "You must carry or wear the weapon.";
        }

        if (LevelGearStore.Of(item) is not { } s || !IsProcWeapon(item))
        {
            return "Only a levelable weapon has a milestone.";
        }

        if (!item.PlayerConstructed || item is BaseWeapon { ArtifactRarity: > 0 })
        {
            return "Only crafted weapons have a milestone.";
        }

        if (s.Proc != null)
        {
            return "This weapon already has its milestone effect.";
        }

        if (proc == null || !LevelGearProcSettings.ChoiceEnabled(proc))
        {
            return "That effect is not available.";
        }

        var unlock = LevelGearProcSettings.UnlockLevel;
        if (s.Level < unlock)
        {
            return $"Reach item level {unlock}.";
        }

        var cost = LevelGearProcSettings.PointCost;
        if (s.Points < cost)
        {
            return $"The milestone costs {cost} points; the weapon has {s.Points}.";
        }

        if (ClusterFLevelGear.SlotsUsed(item) >= ClusterFLevelGear.SlotLimit)
        {
            return $"The weapon already has {ClusterFLevelGear.SlotLimit} properties; the milestone needs a free one.";
        }

        return null;
    }

    /// <summary>Binds the effect, paying once. Returns why not, or null when bound.</summary>
    public static string? Buy(Mobile from, Item item, LevelGearProc? proc)
    {
        if (WhyNotBuy(from, item, proc) is { } why)
        {
            return why;
        }

        var s = LevelGearStore.Of(item)!;
        var cost = LevelGearProcSettings.PointCost;
        s.Points -= cost;
        s.Proc = new LevelGearProcPurchase(proc!.Key, LevelGearProcSettings.UnlockLevel, s.Level, Now, cost);
        item.InvalidateProperties();
        return null;
    }

    // -- The hit (BaseWeapon-level-gear-proc-hit.patch) ----------------------------------------------------------------

    /// <summary>
    /// BaseWeapon.OnHit's call, inside its damageGiven &gt; 0 branch after the weapon's own hit effects. Tries this weapon's
    /// milestone once. Returns the extra damage dealt (0 for no proc).
    /// </summary>
    public static int OnWeaponHit(BaseWeapon weapon, Mobile attacker, Mobile defender, int damageGiven)
    {
        if (_inProc || damageGiven <= 0 || LevelGearStore.Rows.Count == 0 || !LevelGearProcSettings.Enabled)
        {
            return 0;
        }

        if (PurchaseOf(weapon) is not { } bought || LevelGearProc.ByKey(bought.Key) is not { } proc ||
            !LevelGearProcSettings.ChoiceEnabled(proc))
        {
            return 0;
        }

        if (!WeaponReady(weapon, attacker) || OwnerOf(attacker) is not { } owner || !IsTarget(attacker, defender, weapon))
        {
            return 0;
        }

        var now = Now;
        if (LevelGearProcCooldowns.Owners.TryGetValue(owner.Serial, out var ownerUntil) && now < ownerUntil ||
            LevelGearProcCooldowns.Weapons.TryGetValue(weapon.Serial, out var weaponUntil) && now < weaponUntil)
        {
            return 0;
        }

        if (Roll(10000) >= LevelGearProcSettings.ChanceBasisPoints)
        {
            return 0;
        }

        var until = now + TimeSpan.FromSeconds(LevelGearProcSettings.CooldownSeconds);
        LevelGearProcCooldowns.Owners[owner.Serial] = until;
        LevelGearProcCooldowns.Weapons[weapon.Serial] = until;

        _inProc = true;
        try
        {
            attacker.DoHarmful(defender);
            Visual(proc, attacker, defender);
            var raw = LevelGearProcSettings.RawDamage(proc);
            return proc.Element == ResistanceType.Energy
                ? AOS.Damage(defender, attacker, raw, 0, 0, 0, 0, 100)
                : AOS.Damage(defender, attacker, raw, 0, 100, 0, 0, 0);
        }
        finally
        {
            _inProc = false;
        }
    }

    private static void Visual(LevelGearProc proc, Mobile attacker, Mobile defender)
    {
        if (proc.Element == ResistanceType.Energy)
        {
            defender.BoltEffect(0);
        }
        else
        {
            attacker.MovingParticles(defender, 0x36D4, 7, 0, false, true, 9502, 4019, 0x160);
            attacker.PlaySound(0x15E);
        }
    }

    // The weapon is the attacker's own, worn and current, and still a crafted non-artifact weapon.
    private static bool WeaponReady(BaseWeapon weapon, Mobile attacker) =>
        !weapon.Deleted && !attacker.Deleted && attacker.Alive && weapon.Parent == attacker && attacker.Weapon == weapon &&
        IsProcWeapon(weapon) && weapon.PlayerConstructed && weapon.ArtifactRarity == 0;

    /// <summary>
    /// Whose cooldown a strike uses: a player's own, or a companion's player owner when that owner is online, alive, on
    /// the same map and in range. Other pets, summons and creatures have none.
    /// </summary>
    public static PlayerMobile? OwnerOf(Mobile attacker)
    {
        if (attacker is PlayerMobile pm)
        {
            return pm;
        }

        if (attacker is BaseCompanion { Controlled: true, Summoned: false, ControlMaster: PlayerMobile master } &&
            !master.Deleted && master.Alive && master.NetState != null && master.Map == attacker.Map &&
            master.InRange(attacker, LevelGearProcSettings.CompanionOwnerRange))
        {
            return master;
        }

        return null;
    }

    /// <summary>PvE only: a living wild creature the attacker is fighting and may harm, in range and sight.</summary>
    public static bool IsTarget(Mobile attacker, Mobile defender, BaseWeapon weapon) =>
        defender is BaseCreature { Deleted: false, Alive: true, Controlled: false, Summoned: false, Blessed: false,
            NoKillAwards: false } bc &&
        bc.GetMaster() == null && attacker.Combatant == defender && attacker.Map == defender.Map &&
        attacker.InRange(defender, Math.Max(1, weapon.MaxRange)) && attacker.InLOS(defender) &&
        attacker.CanBeHarmful(defender, false);

    /// <summary>The owner's cooldown left, or zero.</summary>
    public static TimeSpan OwnerCooldown(Mobile m) =>
        LevelGearProcCooldowns.Owners.TryGetValue(m.Serial, out var until) && until > Now ? until - Now : TimeSpan.Zero;

    // -- Tooltip (owner-neutral: no wearer or ticking value) --------------------------------------------------------------

    internal static void AddTooltip(Item item, IPropertyList list)
    {
        if (PurchaseOf(item) is not { } bought)
        {
            return;
        }

        var name = LevelGearProc.ByKey(bought.Key)?.Name ?? bought.Key;
        list.Add(1060847, $"Milestone: {name}\t(PvE)"); // ~1_val~ ~2_val~
    }

    // -- Staff (test shard) ---------------------------------------------------------------------------------------

    [Usage("LevelGearProcs [status|on|off]")]
    [Description("Weapon milestone procs (stage 1, test shard): shows the switch and your shared cooldown, or turns the milestone on or off until restart.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        if (verb == "on")
        {
            LevelGearProcSettings.EnabledOverride = true;
        }
        else if (verb == "off")
        {
            LevelGearProcSettings.EnabledOverride = false;
        }

        var problem = LevelGearProcSettings.Problem();
        e.Mobile.SendMessage(
            $"Weapon milestones: {(LevelGearProcSettings.Enabled ? "on" : "off")}; level {LevelGearProcSettings.UnlockLevel}, " +
            $"{LevelGearProcSettings.PointCost} points, {LevelGearProcSettings.ChanceBasisPoints / 100.0:0.##}%, " +
            $"{LevelGearProcSettings.CooldownSeconds}s; your cooldown {OwnerCooldown(e.Mobile).TotalSeconds:0.#}s" +
            (problem == null ? "" : $"; settings invalid: {problem}")
        );
    }
}
