// cc-P627: item identification, stage 1 of shard-migration notes/cc-P607-item-identification-design.md (Chase 2026-10-25,
// option B: item-wide discovery, so once appraised an item shows its stats to everyone; Appraisal reveals magic stats,
// Arms Lore reveals resists; crafted items known; what is visible today stays visible).
//
// Stage 1 is the design's first build step: the disclosure policy, its registry, and a red leak test, kept off by default
// ([ItemKnowledge on, test shard only) until the whole supported surface is covered. It covers weapons and armor:
//
//   - A tooltip seam (server/patches/Item-property-list-policy.patch): before an item's full property builder runs, the
//     policy may supply the whole list. For a managed item it builds the real list privately, then passes on only the
//     lines its catalog gives an owner and that owner's track has reached. Lines no catalog entry names are dropped:
//     an unknown property stays concealed until someone reviews it. The name is the item's plain art name.
//   - Two tracks per item, shared by everyone: magic (Unidentified, Named, Values), Appraisal's; arms (Uninspected,
//     Assessed, Measured), Arms Lore's. When both are complete the item shows its ordinary full tooltip.
//   - Only items stamped as unknown are managed: magic weapons and armor made by monster loot (LootPack, one call in
//     server/patches/LootPack-unidentified-magic.patch) while the policy is on, and staff's [ItemKnowledge conceal.
//     Everything else, everything that exists today and everything crafted, is never stamped and shows as it does now.
//   - Appraisal on a managed item (server/patches/ItemIdentification-item-knowledge.patch): 30 names, 60 values, one
//     difficulty-based check per use. Arms Lore's successful inspect (server/patches/ArmsLore-item-knowledge.patch):
//     30 assessed, 60 measured. Stage 1 gives no skill gain from either on a managed item (the gain rules are stage 3).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Commands;
using Server.Items;
using Server.Targeting;

namespace Server;

public enum MagicKnowledge : byte
{
    Unidentified,
    Named,
    Values
}

public enum ArmsKnowledge : byte
{
    Uninspected,
    Assessed,
    Measured
}

public sealed class ItemKnowledgeRow
{
    public MagicKnowledge Magic;
    public ArmsKnowledge Arms;

    public bool Complete => Magic == MagicKnowledge.Values && Arms == ArmsKnowledge.Measured;
}

public class ItemKnowledgeStore : GenericPersistence
{
    // 0: cc-P627.
    public const int CurrentVersion = 0;

    private static ItemKnowledgeStore _instance;

    internal static readonly Dictionary<Serial, ItemKnowledgeRow> Rows = new();
    internal static bool Enabled;

    public ItemKnowledgeStore() : base("ItemKnowledge", 10)
    {
    }

    public static ItemKnowledgeStore Instance => _instance ??= new ItemKnowledgeStore();

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<Serial, ItemKnowledgeRow>>();
        foreach (var pair in Rows)
        {
            if (World.FindItem(pair.Key) is { Deleted: false })
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.Write(Enabled);
        writer.WriteEncodedInt(live.Count);
        foreach (var (serial, row) in live)
        {
            writer.Write(serial);
            writer.Write((byte)row.Magic);
            writer.Write((byte)row.Arms);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"ItemKnowledgeStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Enabled = reader.ReadBool();
        Rows.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var magic = (MagicKnowledge)reader.ReadByte();
            var arms = (ArmsKnowledge)reader.ReadByte();
            Rows[serial] = new ItemKnowledgeRow { Magic = magic, Arms = arms };
        }
    }
}

public static class ClusterFItemKnowledge
{
    public const string UnidentifiedLine = "Unidentified";
    public const string MagicValuesUnknownLine = "Magic values unknown";
    public const string UninspectedLine = "Resistance and condition: uninspected";

    public const double NamesFloor = 30.0;
    public const double ValuesFloor = 60.0;
    public const double AssessedFloor = 30.0;
    public const double MeasuredFloor = 60.0;
    public static readonly TimeSpan AppraiseDelay = TimeSpan.FromSeconds(1.0);

    // Every line said to a player passes through here as well, so the shard's tests can read them.
    public static Action<Mobile, string> Heard { get; set; }

    private static readonly Dictionary<Mobile, DateTime> _nextAppraise = new();

    public static void Configure()
    {
        _ = ItemKnowledgeStore.Instance;
        Item.PropertyListPolicy = Render;
        CommandSystem.Register("ItemKnowledge", AccessLevel.Administrator, OnCommand);
    }

    private static void Tell(Mobile m, int hue, string text)
    {
        m.SendMessage(hue, text);
        Heard?.Invoke(m, text);
    }

    // -- State --------------------------------------------------------------------------------------------------------

    public static bool Enabled
    {
        get => ItemKnowledgeStore.Enabled;
        set
        {
            if (ItemKnowledgeStore.Enabled == value)
            {
                return;
            }

            ItemKnowledgeStore.Enabled = value;
            foreach (var serial in new List<Serial>(ItemKnowledgeStore.Rows.Keys))
            {
                World.FindItem(serial)?.InvalidateProperties();
            }
        }
    }

    public static ItemKnowledgeRow Of(Item item) =>
        item != null && ItemKnowledgeStore.Rows.TryGetValue(item.Serial, out var row) ? row : null;

    /// <summary>True when the policy is on and this item is still partly unknown.</summary>
    public static bool IsConcealed(Item item) => Enabled && Of(item) is { Complete: false };

    public static bool IsSupported(Item item) => item is BaseWeapon or BaseArmor;

    /// <summary>Marks an item unknown on both tracks (a fresh find). Its tooltip is rebuilt at once.</summary>
    public static void Conceal(Item item)
    {
        if (item == null || item.Deleted || !IsSupported(item))
        {
            return;
        }

        ItemKnowledgeStore.Rows[item.Serial] = new ItemKnowledgeRow();
        if (item is IIdentifiable identifiable)
        {
            identifiable.Identified = false;
        }

        item.InvalidateProperties();
    }

    /// <summary>LootPack's AOS magic branch, after the attributes are rolled: a new magic weapon or armor starts unknown.</summary>
    public static void OnLootMagic(Item item)
    {
        if (Enabled && IsSupported(item))
        {
            Conceal(item);
        }
    }

    /// <summary>Tests only: forget every row and switch the policy off.</summary>
    public static void ResetForTests()
    {
        ItemKnowledgeStore.Enabled = false;
        ItemKnowledgeStore.Rows.Clear();
        _nextAppraise.Clear();
    }

    // -- The catalog --------------------------------------------------------------------------------------------------

    public enum Owner
    {
        Unknown,
        Public,
        Appraisal,
        ArmsLore
    }

    // Each cliloc the stock weapon and armor builders emit (pinned BaseWeapon.GetProperties :2910-3123, BaseArmor :1265-1323,
    // AOS.cs AosAttributes :496-617, AosWeaponAttributes :1013-1104, AosArmorAttributes :1201-1212, AosSkillBonuses :1301-1307,
    // Item.AddResistanceProperties :1977-2011), its owner, and the name Appraisal or Arms Lore gives it before its value.
    private static readonly Dictionary<int, (Owner Owner, string Name)> _catalog = Build();

    private static Dictionary<int, (Owner, string)> Build()
    {
        var c = new Dictionary<int, (Owner, string)>();

        void P(int n) => c[n] = (Owner.Public, null);
        void A(int n, string name) => c[n] = (Owner.Appraisal, name);
        void L(int n, string name) => c[n] = (Owner.ArmsLore, name);

        // Public: who may use it and how it is held.
        P(1072788); P(1072789); // weight
        P(1041350); // faction item
        P(1075086); // elves only
        P(1111709); // gargoyles only
        P(1060636); // exceptional
        P(1050043); // crafted by
        P(1062613); // engraving (player text)
        P(1061171); // two-handed weapon
        P(1061824); // one-handed weapon
        P(1061172); P(1061173); P(1061174); P(1061175); P(1112075); // skill required
        P(1061170); // strength requirement (Appraisal's while a lower-requirements enchantment is unknown)

        // Appraisal: the magic.
        A(1060401, "damage increase");
        A(1060408, "defense chance increase");
        A(1060409, "dexterity bonus");
        A(1060411, "enhance potions");
        A(1060412, "faster cast recovery");
        A(1060413, "faster casting");
        A(1060415, "hit chance increase");
        A(1060431, "hit point increase");
        A(1060432, "intelligence bonus");
        A(1060433, "lower mana cost");
        A(1060434, "lower reagent cost");
        A(1060436, "luck");
        A(1060439, "mana increase");
        A(1060440, "mana regeneration");
        A(1060441, "night sight");
        A(1060443, "stamina regeneration");
        A(1060444, "hit point regeneration");
        A(1060482, "spell channeling");
        A(1060483, "spell damage increase");
        A(1060484, "stamina increase");
        A(1060485, "strength bonus");
        A(1060486, "swing speed increase");
        A(1075210, "increased karma loss");
        A(1060400, "use best weapon skill");
        A(1060416, "hit cold area");
        A(1060417, "hit dispel");
        A(1060418, "hit energy area");
        A(1060419, "hit fire area");
        A(1060420, "hit fireball");
        A(1060421, "hit harm");
        A(1060422, "hit life leech");
        A(1060423, "hit lightning");
        A(1060424, "hit lower attack");
        A(1060425, "hit lower defense");
        A(1060426, "hit magic arrow");
        A(1060427, "hit mana leech");
        A(1060428, "hit physical area");
        A(1060429, "hit poison area");
        A(1060430, "hit stamina leech");
        A(1060438, "mage weapon");
        A(1060450, "self repair");
        A(1060437, "mage armor");
        A(1060451, "skill bonus"); A(1060452, "skill bonus"); A(1060453, "skill bonus"); A(1060454, "skill bonus");
        A(1060455, "skill bonus");
        A(1060410, "durability bonus");
        A(1060435, "lower requirements");
        A(1072792, "balanced");
        A(1072793, "velocity");
        A(1111917, "immolated");
        A(1061078, "artifact rarity");
        A(1060584, "charges");

        // Arms Lore: resistances, damage types, the weapon's numbers and its condition.
        L(1060403, "physical damage");
        L(1060404, "cold damage");
        L(1060405, "fire damage");
        L(1060406, "poison damage");
        L(1060407, "energy damage");
        L(1072846, "chaos damage");
        L(1079978, "direct damage");
        L(1061168, "weapon damage");
        L(1061167, "weapon speed");
        L(1061169, "range");
        L(1060639, "durability");
        L(1060442, "reflect physical damage");
        L(1060448, "physical resist");
        L(1060447, "fire resist");
        L(1060445, "cold resist");
        L(1060449, "poison resist");
        L(1060446, "energy resist");
        for (var level = 0; level <= 8; level++)
        {
            L(1062412 + level, "poison charges");
        }

        return c;
    }

    private static HashSet<int> _slayerTitles;

    private static HashSet<int> SlayerTitles
    {
        get
        {
            if (_slayerTitles == null)
            {
                var titles = new HashSet<int>();
                foreach (var name in Enum.GetValues<SlayerName>())
                {
                    if (name != SlayerName.None && SlayerGroup.GetEntryByName(name) is { } entry)
                    {
                        titles.Add(entry.Title);
                    }
                }

                _slayerTitles = titles;
            }

            return _slayerTitles;
        }
    }

    public static Owner OwnerOf(int number, bool lowerRequirements)
    {
        if (number == 1061170 && lowerRequirements)
        {
            return Owner.Appraisal;
        }

        if (_catalog.TryGetValue(number, out var e))
        {
            return e.Owner;
        }

        return SlayerTitles.Contains(number) ? Owner.Appraisal : Owner.Unknown;
    }

    private static string NameOf(int number) =>
        _catalog.TryGetValue(number, out var e) && e.Name != null ? e.Name : SlayerTitles.Contains(number) ? "slayer" : "property";

    // -- The tooltip ----------------------------------------------------------------------------------------------------

    private const int OplHeaderLength = 15;

    /// <summary>The item's full stock list, built privately and read back as (cliloc, argument) pairs.</summary>
    public static List<(int Number, string Argument)> FullEntries(Item item)
    {
        var opl = new ObjectPropertyList(item);
        item.GetProperties(opl);
        opl.Terminate();

        var buffer = opl.Buffer;
        var entries = new List<(int, string)>();
        var pos = OplHeaderLength;
        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add((number, Encoding.Unicode.GetString(buffer, pos + 6, length)));
            pos += 6 + length;
        }

        opl.Dispose();
        return entries;
    }

    /// <summary>The item's plain art name, never its Name, label or type: an artifact's or a magic title stays unknown.</summary>
    public static int SafeLabel(Item item) => item.ItemID < 0x4000 ? 1020000 + item.ItemID : 1078872 + item.ItemID;

    /// <summary>Item.PropertyListPolicy: the whole safe list for a concealed item, or false to let the stock builder run.</summary>
    public static bool Render(Item item, IPropertyList list)
    {
        if (!IsConcealed(item))
        {
            return false;
        }

        var row = Of(item);
        var entries = FullEntries(item);
        var lowerRequirements = entries.Exists(e => e.Number == 1060435);

        list.Add(SafeLabel(item));

        var magic = new List<(int Number, string Argument)>();
        var arms = new List<(int Number, string Argument)>();

        // The first entry is the stock name line (AddNameProperties runs first); the safe label replaces it.
        for (var i = 1; i < entries.Count; i++)
        {
            var (number, argument) = entries[i];
            switch (OwnerOf(number, lowerRequirements))
            {
                case Owner.Public:
                    list.Add(number, argument);
                    break;
                case Owner.Appraisal:
                    magic.Add((number, argument));
                    break;
                case Owner.ArmsLore:
                    arms.Add((number, argument));
                    break;
            }
        }

        switch (row.Magic)
        {
            case MagicKnowledge.Unidentified:
                list.Add(UnidentifiedLine);
                break;
            case MagicKnowledge.Named:
                list.Add(magic.Count > 0
                    ? $"Appraised traits: {Names(magic)}"
                    : "Appraised: its magic concerns resistance; Arms Lore can measure it");
                if (magic.Count > 0)
                {
                    list.Add(MagicValuesUnknownLine);
                }

                break;
            default:
                foreach (var (number, argument) in magic)
                {
                    list.Add(number, argument);
                }

                break;
        }

        switch (row.Arms)
        {
            case ArmsKnowledge.Uninspected:
                list.Add(UninspectedLine);
                break;
            case ArmsKnowledge.Assessed:
                list.Add($"Inspected: {Names(arms)}; values not measured");
                break;
            default:
                foreach (var (number, argument) in arms)
                {
                    list.Add(number, argument);
                }

                break;
        }

        item.AppendChildProperties(list); // the parent's own lines (a vendor's price), as the stock path adds them
        return true;
    }

    private static string Names(List<(int Number, string Argument)> lines)
    {
        var names = new List<string>();
        foreach (var (number, _) in lines)
        {
            var name = NameOf(number);
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        return string.Join(", ", names);
    }

    // -- Appraisal ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The design's first model: D = clamp(15 x properties + 0.75 x the strongest property's intensity, 10, 175), with the
    /// shard's own property count and intensity (the Appraiser's and the imbuing catalogue's readers).
    /// </summary>
    public static int Difficulty(Item item)
    {
        var count = RequisitionProperties.PropertyCount(item);
        var strongest = 0;
        foreach (var def in ImbueCatalogue.All)
        {
            strongest = Math.Max(strongest, RequisitionProperties.IntensityOf(item, def.Name));
        }

        strongest = Math.Clamp(strongest, 0, 100);
        return (int)Math.Clamp(Math.Round(15.0 * count + 0.75 * strongest), 10, 175);
    }

    public static int StageDifficulty(int d, MagicKnowledge stage) => stage == MagicKnowledge.Named ? Math.Max(0, d - 60) : d;

    /// <summary>The chance of reaching a stage: a window 25 either side of its difficulty.</summary>
    public static double Chance(double skill, int difficulty) => Math.Clamp((skill - (difficulty - 25)) / 50.0, 0.0, 1.0);

    /// <summary>ItemIdentification's item target: true when the item is managed here (the stock route does not run).</summary>
    public static bool TryAppraise(Mobile from, Item item)
    {
        if (!Enabled || Of(item) is not { } row)
        {
            return false;
        }

        if (_nextAppraise.TryGetValue(from, out var next) && Core.Now < next)
        {
            Tell(from, 0x3B2, "You must wait a moment before appraising another item.");
            return true;
        }

        _nextAppraise[from] = Core.Now + AppraiseDelay;

        if (row.Magic == MagicKnowledge.Values)
        {
            Tell(from, 0x3B2, "Its magic is already known.");
            return true;
        }

        var skill = Math.Clamp(from.Skills[SkillName.ItemID].Value, 0.0, 200.0);
        var reach = skill >= ValuesFloor ? MagicKnowledge.Values : skill >= NamesFloor ? MagicKnowledge.Named : MagicKnowledge.Unidentified;

        if (reach == MagicKnowledge.Unidentified)
        {
            Tell(from, 0x3B2, "You sense magic, but cannot identify it.");
            return true;
        }

        if (reach <= row.Magic)
        {
            Tell(from, 0x3B2, "You cannot determine more.");
            return true;
        }

        // One check per use: a single roll, read against each stage this skill can reach.
        var d = Difficulty(item);
        var roll = Utility.RandomDouble();
        var learned = row.Magic;
        for (var stage = row.Magic + 1; stage <= reach; stage++)
        {
            if (roll < Chance(skill, StageDifficulty(d, stage)))
            {
                learned = stage;
            }
        }

        if (learned == row.Magic)
        {
            Tell(from, 0x3B2, "You cannot determine more.");
            return true;
        }

        row.Magic = learned;
        if (learned == MagicKnowledge.Values && item is IIdentifiable identifiable)
        {
            identifiable.Identified = true; // the stock flag follows, for legacy labels
        }

        item.InvalidateProperties();
        Tell(from, 0x59, learned == MagicKnowledge.Values ? "You identify its magic." : "You name its magic, but not its strength.");
        return true;
    }

    // -- Arms Lore ------------------------------------------------------------------------------------------------------

    /// <summary>After pinned ArmsLore's successful inspect of a weapon or armor: assessed from 30, measured from 60.</summary>
    public static void OnArmsLore(Mobile from, Item item)
    {
        if (!Enabled || Of(item) is not { } row)
        {
            return;
        }

        var skill = from.Skills[SkillName.ArmsLore].Value;
        var reach = skill >= MeasuredFloor ? ArmsKnowledge.Measured : skill >= AssessedFloor ? ArmsKnowledge.Assessed : ArmsKnowledge.Uninspected;
        if (reach <= row.Arms)
        {
            return;
        }

        row.Arms = reach;
        item.InvalidateProperties();
        Tell(from, 0x59, reach == ArmsKnowledge.Measured
            ? "You measure its resistances and condition."
            : "You make out its resistances and condition, but not their measure.");
    }

    // -- Staff --------------------------------------------------------------------------------------------------------

    [Usage("ItemKnowledge [status|on|off|conceal|forget]")]
    [Description("Item identification (cc-P627, test shard only): status, switch the policy on or off, mark a targeted weapon or armor as an unknown find, or forget what is known about it.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var arg = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";

        switch (arg)
        {
            case "on":
                Enabled = true;
                from.SendMessage($"Item identification is on. {ItemKnowledgeStore.Rows.Count} item(s) have records.");
                break;
            case "off":
                Enabled = false;
                from.SendMessage("Item identification is off: every item shows its ordinary tooltip. Records are kept.");
                break;
            case "conceal":
            case "forget":
                from.SendMessage("Target a weapon or armor.");
                from.Target = new StaffTarget();
                break;
            default:
                from.SendMessage($"Item identification is {(Enabled ? "on" : "off")}. {ItemKnowledgeStore.Rows.Count} item(s) have records.");
                break;
        }
    }

    private sealed class StaffTarget : Target
    {
        public StaffTarget() : base(12, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Item item || !IsSupported(item))
            {
                from.SendMessage("That is not a weapon or armor.");
                return;
            }

            Conceal(item);
            from.SendMessage($"Marked as an unknown find (difficulty {Difficulty(item)}).");
        }
    }
}
