// cc-P82: the eleven custom imbue properties (design: "Custom properties"), stored per item and run where their system
// is. Values and ingredients: Configuration/imbuing.json (custom, effects). At most one custom property per item.
//
// Storage: an item's custom property and value live here, by item serial, in their own save file (Saves/
// ImbueCustomProperties, pinned's GenericPersistence), so no item class changes. A deleted item's entry is dropped at the
// next save. The tooltip line comes from the Item.ExtraProperties hook (patch Imbue-custom-property-hooks.patch).
//
// Where each one runs (what a character wears counts; a companion counts its master's):
//   Bountiful        mining and lumberjacking yield (CompactOreSatchelRoutingHook.Give, ClusterFLumberjackingExtension.Give)
//   Prospector       a rare find on the same two gathers (effects.prospectorMining / prospectorLumber)
//   Butcher's Eye    carving yield (the Corpse.OnItemAdded partial) and a kill's monster parts (the creature death
//                    event); a small chance at a rare part
//   Mastercraft      exceptional chance (CraftItem.GetExceptionalChance, patched)
//   Time Slip        a weapon hit that ran a special move: refunds its mana and the 3 s double-cost window (BaseWeapon.OnHit
//                    and WeaponAbility, patched)
//   Shatter          a weapon hit: lowers the target's highest resist briefly (BaseWeapon.OnHit, patched)
//   Bond             a companion's melee damage (BaseWeapon.OnHit, patched) and an evo pet's XP (EvoPet.OnCreatureDeath)
//   Spell Leech      spell damage dealt heals the caster (SpellHelper.Damage, patched)
//   Fortune's Favor  loot box roll quality only (LootBoxRoller.Roll): a chance the contents roll one tier higher; the
//                    number of items, gold and Renown stay the box's own tier's
//   Echo of the Loop HOOK ONLY: EchoOfTheLoopPercent. No XmlLevelItems and no player levels exist yet (F-12).
//   Renowned         HOOK ONLY: RenownedAmount. Nothing pays Renown for League commissions or hunts yet
//                    (RenownSource.LeagueJob and .Commission have no callers).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;
using Server.Items;
using Server.Mobiles;

namespace Server;

public class ImbueCustomStore : GenericPersistence
{
    // 0: cc-P82.
    public const int CurrentVersion = 0;

    private static ImbueCustomStore? _instance;

    private static readonly Dictionary<Serial, (string Name, int Value)> _props = new();

    public ImbueCustomStore() : base("ImbueCustomProperties", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ImbueCustomStore();
        Item.ExtraProperties += AddTooltip;
    }

    public static (string Name, int Value)? Of(Item item) =>
        item != null && _props.TryGetValue(item.Serial, out var p) ? p : null;

    public static int Get(Item item, string name) =>
        Of(item) is { } p && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ? p.Value : 0;

    /// <summary>Sets this custom property (0 removes it). The caller checks the one-per-item rule.</summary>
    public static void Set(Item item, string name, int value)
    {
        if (value <= 0)
        {
            if (Of(item) is { } p && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                _props.Remove(item.Serial);
            }
        }
        else
        {
            _props[item.Serial] = (name, value);
        }

        item.InvalidateProperties();
    }

    private static void AddTooltip(Item item, IPropertyList list)
    {
        if (Of(item) is { } p)
        {
            list.Add(1049644, $"{p.Name} {p.Value}%"); // [~1_stuff~]
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<Serial, (string Name, int Value)>>();
        foreach (var pair in _props)
        {
            if (World.FindItem(pair.Key) is { Deleted: false })
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(live.Count);
        foreach (var (serial, (name, value)) in live)
        {
            writer.Write(serial);
            writer.Write(name);
            writer.WriteEncodedInt(value);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"ImbueCustomStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        _props.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var name = reader.ReadString();
            var value = reader.ReadEncodedInt();
            _props[serial] = (name, value);
        }
    }
}

public static class ClusterFImbueEffects
{
    // When each item last went on a character (the Fortune's Favor achievements' 60 seconds). Memory only.
    private static readonly Dictionary<Serial, DateTime> _equippedAt = new();

    private static readonly Dictionary<Mobile, ResistanceMod> _shattered = new();

    private static bool _inLuckyNotify;

    // -- What a character wears ----------------------------------------------------------------------------------

    /// <summary>The sum of a custom property over what this character wears (a companion: its master's).</summary>
    public static int Worn(Mobile? m, string name)
    {
        if (m is BaseCreature { Controlled: true, ControlMaster: { } master })
        {
            m = master;
        }

        if (m == null || m.Items == null)
        {
            return 0;
        }

        var total = 0;
        foreach (var item in m.Items)
        {
            if (item is not Container)
            {
                total += ImbueCustomStore.Get(item, name);
            }
        }

        return total;
    }

    // Tests set the roll: 0.0 makes every chance above 0 happen, 0.9999 makes none happen; null is the server's own roll.
    public static double? RollOverride { get; set; }

    private static bool Roll(int percent) =>
        percent > 0 && (RollOverride ?? Utility.RandomDouble()) * 100.0 < percent;

    // -- Equip tracking (patched PlayerMobile.OnItemAdded) -------------------------------------------------------------

    public static void OnEquipped(Mobile m, Item item)
    {
        if (ImbueCustomStore.Of(item) is { } p && p.Name == "Fortune's Favor")
        {
            _equippedAt[item.Serial] = Core.Now;
        }
    }

    public static void SetEquippedAt(Item item, DateTime when) => _equippedAt[item.Serial] = when;

    /// <summary>Whether this character wears Fortune's Favor gear put on within the window (config) before now.</summary>
    public static bool LuckGearJustEquipped(PlayerMobile pm)
    {
        var window = TimeSpan.FromSeconds(ImbuingTable.Current.Effects.FortuneEquipWindowSeconds);
        foreach (var item in pm.Items)
        {
            if (ImbueCustomStore.Get(item, "Fortune's Favor") > 0 && _equippedAt.TryGetValue(item.Serial, out var at) &&
                Core.Now - at <= window)
            {
                return true;
            }
        }

        return false;
    }

    // -- Fortune's Favor (LootBoxRoller.Roll, ClusterFLootBoxes.Award) -------------------------------------------------

    public static int FortunePercent(PlayerMobile pm) =>
        Math.Min(ImbuingTable.Current.Effects.FortuneTotalCap, Worn(pm, "Fortune's Favor"));

    /// <summary>The tier a box's contents roll at: one higher on a Fortune's Favor roll (at most 6). Never the item count.</summary>
    public static int FortuneQualityTier(PlayerMobile pm, int tier) =>
        tier < 6 && Roll(FortunePercent(pm)) ? tier + 1 : tier;

    /// <summary>A box was earned (ClusterFLootBoxes.Award): the hidden achievements' count, when luck gear just went on.</summary>
    public static void OnLootBoxEarned(PlayerMobile pm, string source)
    {
        if (_inLuckyNotify || source.StartsWith($"ach:{ClusterFAchievementSystem.DressedKey}", StringComparison.Ordinal) ||
            source.StartsWith($"ach:{ClusterFAchievementSystem.HabitKey}", StringComparison.Ordinal) || !LuckGearJustEquipped(pm))
        {
            return;
        }

        _inLuckyNotify = true;
        try
        {
            ClusterFAchievementSystem.NotifyLuckyGearBox(pm);
        }
        finally
        {
            _inLuckyNotify = false;
        }
    }

    // -- Gathering (the mining and lumber Give overrides) --------------------------------------------------------------

    /// <summary>Bountiful: the extra units on a gathered stack (whole percent, the remainder by chance).</summary>
    public static int BountifulExtra(Mobile m, int amount)
    {
        var pct = Worn(m, "Bountiful");
        if (pct <= 0 || amount <= 0)
        {
            return 0;
        }

        var scaled = amount * pct;
        return scaled / 100 + (Roll(scaled % 100) ? 1 : 0);
    }

    /// <summary>Prospector: a rare find on a gather, or null. kind: "mining" or "lumber".</summary>
    public static Item? ProspectorFind(Mobile m, string kind)
    {
        var e = ImbuingTable.Current.Effects;
        var list = kind == "mining" ? e.ProspectorMining : e.ProspectorLumber;
        if (list.Length == 0 || !Roll(Worn(m, "Prospector")))
        {
            return null;
        }

        var type = AssemblyHandler.FindTypeByName(list[Utility.Random(list.Length)]);
        return type != null ? Activator.CreateInstance(type) as Item : null;
    }

    /// <summary>Called by the gathering hooks after the stack is made: Bountiful on it, Prospector beside it.</summary>
    public static void OnGathered(Mobile m, Item item, string kind)
    {
        if (item.Stackable)
        {
            item.Amount += BountifulExtra(m, item.Amount);
        }

        if (ProspectorFind(m, kind) is { } find)
        {
            m.SendMessage(0x44, "Your eye for the rare turns something up.");
            if (!m.PlaceInBackpack(find))
            {
                find.MoveToWorld(m.Location, m.Map);
            }
        }
    }

    // -- Butcher's Eye --------------------------------------------------------------------------------------------------

    private static bool _inCarve;

    /// <summary>Carving (the Corpse.OnItemAdded partial): a carved stack grows by the carver's Butcher's Eye.</summary>
    public static void OnCarvedItem(Corpse corpse, Item item)
    {
        if (_inCarve || !corpse.Carved || !item.Stackable || corpse.Map == null)
        {
            return;
        }

        foreach (var mob in corpse.Map.GetMobilesInRange(corpse.Location, 2))
        {
            if (mob is PlayerMobile pm && Worn(pm, "Butcher's Eye") > 0)
            {
                ButcherOn(pm, corpse, item);
                return;
            }
        }
    }

    /// <summary>A kill: the monster parts (effects.butchersEyeRareParts types) on the corpse grow by the killer's Butcher's Eye.</summary>
    public static void OnKill(PlayerMobile pm, Container? corpse)
    {
        if (corpse == null || corpse.Deleted || Worn(pm, "Butcher's Eye") <= 0)
        {
            return;
        }

        var parts = ImbuingTable.Current.Effects.ButchersEyeRareParts;
        foreach (var item in new List<Item>(corpse.Items))
        {
            if (item.Stackable && Array.Exists(parts, n => n == item.GetType().Name))
            {
                ButcherOn(pm, corpse, item);
            }
        }
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (!bc.Controlled && !bc.Summoned && bc.LastKiller is PlayerMobile pm)
        {
            OnKill(pm, bc.Corpse);
        }
    }

    private static void ButcherOn(PlayerMobile pm, Container corpse, Item item)
    {
        var pct = Worn(pm, "Butcher's Eye");
        item.Amount += item.Amount * pct / 100 + (Roll(item.Amount * pct % 100) ? 1 : 0);

        var e = ImbuingTable.Current.Effects;
        var full = ImbuingTable.Current.CustomOf("Butcher's Eye")?.Full ?? 20;
        if (e.ButchersEyeRareParts.Length > 0 && Roll(e.ButchersEyeRareChance * pct / Math.Max(1, full)) &&
            AssemblyHandler.FindTypeByName(e.ButchersEyeRareParts[Utility.Random(e.ButchersEyeRareParts.Length)]) is { } type &&
            Activator.CreateInstance(type) is Item rare)
        {
            _inCarve = true;
            try
            {
                corpse.DropItem(rare);
            }
            finally
            {
                _inCarve = false;
            }

            pm.SendMessage(0x44, "A practiced cut finds something rare.");
        }
    }

    // -- Crafting (patched CraftItem.GetExceptionalChance) --------------------------------------------------------------

    public static double MastercraftBonus(Mobile from) => Worn(from, "Mastercraft") / 100.0;

    // -- Combat (patched BaseWeapon.OnHit, WeaponAbility, SpellHelper.Damage) -----------------------------------------

    /// <summary>Bond: a companion's melee damage, by its master's Bond.</summary>
    public static void AlterMeleeDamage(Mobile attacker, Mobile defender, ref int damage)
    {
        if (attacker is BaseCreature { Controlled: true } && damage > 0)
        {
            damage += damage * Worn(attacker, "Bond") / 100;
        }
    }

    /// <summary>After a weapon hit's special move ran: Time Slip (refund) and Shatter (the defender's highest resist).</summary>
    public static void OnWeaponHit(Mobile attacker, Mobile defender, WeaponAbility? ability, int manaBefore)
    {
        if (attacker is not PlayerMobile)
        {
            return;
        }

        if (ability != null && attacker.Mana < manaBefore && Roll(Worn(attacker, "Time Slip")))
        {
            attacker.Mana = manaBefore;
            WeaponAbility.ClearCooldown(attacker);
            attacker.SendMessage(0x44, "Time slips: your special move costs you nothing.");
        }

        if (Roll(Worn(attacker, "Shatter")))
        {
            Shatter(defender);
        }
    }

    public static void Shatter(Mobile defender)
    {
        var e = ImbuingTable.Current.Effects;
        if (defender.Deleted || !defender.Alive || e.ShatterResistDrop <= 0)
        {
            return;
        }

        if (_shattered.Remove(defender, out var old))
        {
            defender.RemoveResistanceMod(old);
        }

        var highest = ResistanceType.Physical;
        foreach (var type in new[] { ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Poison, ResistanceType.Energy })
        {
            if (defender.GetResistance(type) > defender.GetResistance(highest))
            {
                highest = type;
            }
        }

        var mod = new ResistanceMod(highest, "ImbueShatter", -e.ShatterResistDrop);
        defender.AddResistanceMod(mod);
        _shattered[defender] = mod;
        defender.FixedEffect(0x37B9, 10, 5);

        Timer.StartTimer(TimeSpan.FromSeconds(e.ShatterSeconds), () =>
        {
            if (_shattered.TryGetValue(defender, out var current) && current == mod)
            {
                _shattered.Remove(defender);
                defender.RemoveResistanceMod(mod);
            }
        });
    }

    /// <summary>Spell Leech: a share of the spell damage the caster dealt comes back as hits.</summary>
    public static void OnSpellDamage(Mobile? from, Mobile target, int damageGiven)
    {
        if (from is not PlayerMobile || damageGiven <= 0 || !from.Alive || from == target)
        {
            return;
        }

        var heal = damageGiven * Worn(from, "Spell Leech") / 100;
        if (heal > 0)
        {
            from.Hits += heal;
        }
    }

    // -- Companions (EvoPet.OnCreatureDeath) -------------------------------------------------------------------------

    public static int BondXp(Mobile pet, int xp) => xp + xp * Worn(pet, "Bond") / 100;

    // -- Hooks for systems that do not exist yet ---------------------------------------------------------------------

    /// <summary>
    /// TODO(F-12, XmlLevelItems): Echo of the Loop. The percent more XP a levelable item or a player level should take.
    /// No caller: neither XmlLevelItems nor player levels exist in this build.
    /// </summary>
    public static int EchoOfTheLoopPercent(Mobile m) => Worn(m, "Echo of the Loop");

    /// <summary>
    /// TODO(League commissions and hunts): Renowned. What a Renown payout from a League commission or hunt should become.
    /// No caller: nothing pays RenownSource.LeagueJob or .Commission yet.
    /// </summary>
    public static int RenownedAmount(Mobile m, int amount) => amount + amount * Worn(m, "Renowned") / 100;
}
