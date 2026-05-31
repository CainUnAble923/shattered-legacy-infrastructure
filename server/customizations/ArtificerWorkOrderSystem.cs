using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// ── Requirement ───────────────────────────────────────────────────────────────

/// <summary>A single property requirement within a work order.</summary>
public sealed class ArtificerWorkOrderRequirement
{
    public string PropertyName { get; }
    public int    MinValue     { get; }

    public ArtificerWorkOrderRequirement(string propName, int minValue)
    {
        PropertyName = propName;
        MinValue     = minValue;
    }
}

// ── Order definition ──────────────────────────────────────────────────────────

/// <summary>
/// Defines a single work order / commission.
///
/// Regular orders:  guildmaster hands the player a blank item (ItemFactory != null).
///                  The serial is stored in ClusterFAccountData so we can match on turn-in.
///
/// Combo orders:    ItemFactory is null.  The player must craft the item themselves
///                  (smith membership required) using the specified material.
///                  On turn-in we scan the backpack for matching type + resource.
/// </summary>
public sealed class ArtificerWorkOrderDef
{
    public string   Key             { get; }
    public string   Title           { get; }
    public string   Flavor          { get; }

    public Func<Item>? ItemFactory  { get; }  // null for combo orders
    public string   ItemDescription { get; }

    public Type?         RequiredItemType { get; }  // combo: verify crafted item type
    public CraftResource RequiredResource { get; }  // combo: verify crafted material

    public IReadOnlyList<ArtificerWorkOrderRequirement> Requirements { get; }

    public bool RequiresSmithMembership { get; }

    public int GoldReward           { get; }
    public int ArtificerScripReward { get; }
    public int ArtificerRepReward   { get; }
    public int SmithScripReward     { get; }
    public int SmithRepReward       { get; }

    // Regular order constructor
    public ArtificerWorkOrderDef(
        string key, string title, string flavor,
        Func<Item> factory, string itemDesc,
        int gold, int artificerScrip, int artificerRep,
        params ArtificerWorkOrderRequirement[] reqs)
    {
        Key                     = key;
        Title                   = title;
        Flavor                  = flavor;
        ItemFactory             = factory;
        ItemDescription         = itemDesc;
        GoldReward              = gold;
        ArtificerScripReward    = artificerScrip;
        ArtificerRepReward      = artificerRep;
        Requirements            = reqs;
        RequiresSmithMembership = false;
    }

    // Combo order constructor
    public ArtificerWorkOrderDef(
        string key, string title, string flavor,
        Type itemType, CraftResource resource, string itemDesc,
        int gold, int artificerScrip, int artificerRep, int smithScrip, int smithRep,
        params ArtificerWorkOrderRequirement[] reqs)
    {
        Key                     = key;
        Title                   = title;
        Flavor                  = flavor;
        ItemFactory             = null;
        RequiredItemType        = itemType;
        RequiredResource        = resource;
        ItemDescription         = itemDesc;
        GoldReward              = gold;
        ArtificerScripReward    = artificerScrip;
        ArtificerRepReward      = artificerRep;
        SmithScripReward        = smithScrip;
        SmithRepReward          = smithRep;
        Requirements            = reqs;
        RequiresSmithMembership = true;
    }
}

// ── Catalogue ─────────────────────────────────────────────────────────────────

/// <summary>
/// Fixed pool of 39 commissions across four tiers.
///
/// Availability is gated by mastery — every required property must be mastered
/// before the order appears.  Combo orders additionally require Smiths' Fellowship
/// membership and the player to craft the base item from a specific material.
///
/// Tier breakdown (by required property power / standing):
///   Journeyman  (1,000 standing)  — 3  orders  — Night Sight applications
///   Artificer   (5,000 standing)  — 20 orders  — mage, stat/regen, defense, hit effects
///   Master      (15,000 standing) — 12 orders  — high-end combat + slayers
///   Combo       (15,000 standing) — 4  orders  — smith-crafted + Artificer-enchanted
/// </summary>
public static class ArtificerWorkOrderCatalogue
{
    public static readonly List<ArtificerWorkOrderDef> All = new();

    static ArtificerWorkOrderCatalogue() { Build(); }

    // Shorthand helpers
    private static ArtificerWorkOrderRequirement R(string prop, int min) => new(prop, min);

    // Activator.CreateInstance handles types with optional-param constructors (no new() required)
    private static Item SpawnWeapon<T>(string name) where T : BaseWeapon
    {
        var item = (T)Activator.CreateInstance(typeof(T))!;
        item.Name     = $"{name} (commission)";
        item.LootType = LootType.Regular;
        return item;
    }

    private static Item SpawnArmor<T>(string name) where T : BaseArmor
    {
        var item = (T)Activator.CreateInstance(typeof(T))!;
        item.Name     = $"{name} (commission)";
        item.LootType = LootType.Regular;
        return item;
    }

    private static Item SpawnJewel<T>(string name) where T : BaseJewel
    {
        var item = (T)Activator.CreateInstance(typeof(T))!;
        item.Name     = $"{name} (commission)";
        item.LootType = LootType.Regular;
        return item;
    }

    private static Item SpawnClothing<T>(string name) where T : BaseClothing
    {
        var item = (T)Activator.CreateInstance(typeof(T))!;
        item.Name     = $"{name} (commission)";
        item.LootType = LootType.Regular;
        return item;
    }

    // ── Availability / verification ───────────────────────────────────────────

    /// <summary>
    /// True if the player has mastered every required property and holds any
    /// prerequisite guild memberships.
    /// </summary>
    public static bool IsAvailable(ArtificerWorkOrderDef def, ClusterFAccountData data, bool isSmithMember)
    {
        if (def.RequiresSmithMembership && !isSmithMember) return false;

        foreach (var req in def.Requirements)
        {
            var propDef = ImbueCatalogue.All.Find(
                d => d.Name.Equals(req.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (propDef == null || !data.IsMastered(req.PropertyName, propDef.DiscoveryThreshold))
                return false;
        }
        return true;
    }

    /// <summary>
    /// True if every required property on <paramref name="item"/> meets its minimum value.
    /// </summary>
    public static bool ItemMeetsRequirements(Item item, ArtificerWorkOrderDef def)
    {
        foreach (var req in def.Requirements)
        {
            var propDef = ImbueCatalogue.All.Find(
                d => d.Name.Equals(req.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (propDef == null || propDef.Get(item) < req.MinValue)
                return false;
        }
        return true;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private static void Build()
    {
        // ── Journeyman tier — Night Sight ─────────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "ns_ring", "Nightvision Ring",
            "A sailor commissioned a ring to navigate the dark sea caves south of Nujel'm.",
            () => SpawnJewel<GoldRing>("Nightvision Ring"), "Gold Ring",
            3_000, 10, 50,
            R("Night Sight", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "ns_helm", "Shadowsight Helm",
            "A mine foreman ordered a helm that lets his crew work through the night shift.",
            () => SpawnArmor<PlateHelm>("Shadowsight Helm"), "Plate Helm",
            3_500, 12, 60,
            R("Night Sight", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "ns_robe", "Dusk Walker's Robe",
            "A ranger wanted a robe enchanted for moving unseen through the forest after dark.",
            () => SpawnClothing<Robe>("Dusk Walker's Robe"), "Robe",
            3_000, 10, 50,
            R("Night Sight", 1)));

        // ── Artificer tier — Mage / Caster ────────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "mage_ring", "Ring of Clarity",
            "An elder mage of the Royal Britannian Guard demands sharper casting reflexes.",
            () => SpawnJewel<GoldRing>("Ring of Clarity"), "Gold Ring",
            12_000, 60, 200,
            R("Faster Cast Recovery", 2), R("Mana Regen", 3)));

        All.Add(new ArtificerWorkOrderDef(
            "arcane_robe", "Arcane Scholar's Robe",
            "A mage academy student seeks a robe to push her battle spells to their limit.",
            () => SpawnClothing<Robe>("Arcane Scholar's Robe"), "Robe",
            14_000, 70, 220,
            R("Spell Damage Increase", 8), R("Faster Casting", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "mage_hat", "Enchanter's Circlet",
            "A court wizard needs a hat to speed up the lengthy rituals at the palace.",
            () => SpawnClothing<WizardsHat>("Enchanter's Circlet"), "Wizard's Hat",
            12_000, 60, 200,
            R("Spell Damage Increase", 6), R("Faster Cast Recovery", 2)));

        All.Add(new ArtificerWorkOrderDef(
            "lmc_brace", "Mana Conservator's Bracelet",
            "A traveling mage needs to stretch her mana reserves across multi-day expeditions.",
            () => SpawnJewel<GoldBracelet>("Mana Conservator's Bracelet"), "Gold Bracelet",
            10_000, 50, 175,
            R("Lower Mana Cost", 25)));

        All.Add(new ArtificerWorkOrderDef(
            "spell_chan_staff", "Channeling Gnarled Staff",
            "A battle mage insists on spellcasting while wielding a weapon. Make it possible.",
            () => SpawnWeapon<GnarledStaff>("Channeling Gnarled Staff"), "Gnarled Staff",
            13_000, 65, 210,
            R("Spell Channeling", 1), R("Spell Damage Increase", 8)));

        All.Add(new ArtificerWorkOrderDef(
            "mana_hat", "Inscribed Wizard's Hat",
            "An old mage's reagent pouches are always empty. He wants an alternative.",
            () => SpawnClothing<WizardsHat>("Inscribed Wizard's Hat"), "Wizard's Hat",
            11_000, 55, 185,
            R("Lower Mana Cost", 20), R("Faster Cast Recovery", 1)));

        // ── Artificer tier — Stats / Regen ────────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "str_ring", "Warrior's Band",
            "A pit fighter wants a ring to push his raw strength beyond natural limits.",
            () => SpawnJewel<GoldRing>("Warrior's Band"), "Gold Ring",
            8_000, 40, 150,
            R("Bonus Strength", 6)));

        All.Add(new ArtificerWorkOrderDef(
            "dex_brace", "Quicksilver Bracelet",
            "A thief's guild courier needs to stay nimble and never run dry on stamina.",
            () => SpawnJewel<GoldBracelet>("Quicksilver Bracelet"), "Gold Bracelet",
            10_000, 55, 185,
            R("Bonus Dexterity", 6), R("Stamina Regen", 3)));

        All.Add(new ArtificerWorkOrderDef(
            "int_ring", "Sage's Ring",
            "A court scribe needs sharper mental acuity and better mana flow for his work.",
            () => SpawnJewel<GoldRing>("Sage's Ring"), "Gold Ring",
            10_000, 55, 185,
            R("Bonus Intelligence", 6), R("Mana Regen", 3)));

        All.Add(new ArtificerWorkOrderDef(
            "hp_ring", "Vitality Ring",
            "An ageing gladiator wants to improve his body's natural recovery between bouts.",
            () => SpawnJewel<GoldRing>("Vitality Ring"), "Gold Ring",
            8_000, 40, 150,
            R("Hit Points Regen", 4)));

        All.Add(new ArtificerWorkOrderDef(
            "healer_robe", "Healer's Vestment",
            "A battlefield healer needs a robe suited for prolonged combat triage.",
            () => SpawnClothing<Robe>("Healer's Vestment"), "Robe",
            12_000, 60, 200,
            R("Hit Points Regen", 3), R("Mana Regen", 3)));

        // ── Artificer tier — Defense / Armor ──────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "fire_chest", "Ember Ward Plate",
            "A fire mage's bodyguard needs protection from her employer's errant fireballs.",
            () => SpawnArmor<PlateChest>("Ember Ward Plate"), "Plate Chest",
            10_000, 50, 175,
            R("Fire Resist Bonus", 15)));

        All.Add(new ArtificerWorkOrderDef(
            "cold_chain", "Frostguard Chain",
            "A Far North merchant regularly crosses frozen passes. He needs cold protection.",
            () => SpawnArmor<ChainChest>("Frostguard Chain"), "Chain Chest",
            10_000, 50, 175,
            R("Cold Resist Bonus", 15)));

        All.Add(new ArtificerWorkOrderDef(
            "poison_leather", "Venombane Tunic",
            "A dungeon delver keeps dying to venomous creatures in Dungeon Covetous.",
            () => SpawnArmor<LeatherChest>("Venombane Tunic"), "Leather Chest",
            10_000, 50, 175,
            R("Poison Resist Bonus", 15)));

        All.Add(new ArtificerWorkOrderDef(
            "energy_helm", "Arcstop Helm",
            "An arena combatant keeps drawing energy-mage opponents. She wants a counter.",
            () => SpawnArmor<PlateHelm>("Arcstop Helm"), "Plate Helm",
            10_000, 50, 175,
            R("Energy Resist Bonus", 15)));

        All.Add(new ArtificerWorkOrderDef(
            "mage_plate", "Enchanted Mage Plate",
            "A paladin-mage wants full plate that doesn't interfere with his spellcasting.",
            () => SpawnArmor<PlateChest>("Enchanted Mage Plate"), "Plate Chest",
            15_000, 75, 250,
            R("Mage Armor", 1), R("Cold Resist Bonus", 10)));

        All.Add(new ArtificerWorkOrderDef(
            "reflect_chest", "Mirror Plate",
            "A veteran warrior wants armour that punishes reckless attackers.",
            () => SpawnArmor<PlateChest>("Mirror Plate"), "Plate Chest",
            12_000, 60, 200,
            R("Reflect Physical Damage", 12)));

        All.Add(new ArtificerWorkOrderDef(
            "enhance_brace", "Alchemist's Bracelet",
            "A master alchemist wants to get more out of every potion she brews.",
            () => SpawnJewel<GoldBracelet>("Alchemist's Bracelet"), "Gold Bracelet",
            9_000, 45, 165,
            R("Enhance Potions", 20)));

        // ── Artificer tier — Weapons: hit effects ─────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "lightning_sword", "Storm Cleaver",
            "A thunderstruck paladin wants a blade that crackles with lightning on every blow.",
            () => SpawnWeapon<Longsword>("Storm Cleaver"), "Longsword",
            11_000, 55, 185,
            R("Hit Lightning", 35)));

        All.Add(new ArtificerWorkOrderDef(
            "fireball_katana", "Pyre Katana",
            "A duelist from the volcanic isles of Fire Island seeks a blade of living flame.",
            () => SpawnWeapon<Katana>("Pyre Katana"), "Katana",
            11_000, 55, 185,
            R("Hit Fireball", 35)));

        All.Add(new ArtificerWorkOrderDef(
            "cold_kryss", "Winter's Edge",
            "A northern assassin wants a blade that sends a chill through all nearby enemies.",
            () => SpawnWeapon<Kryss>("Winter's Edge"), "Kryss",
            11_000, 55, 185,
            R("Hit Cold Area", 35)));

        All.Add(new ArtificerWorkOrderDef(
            "dispel_staff", "Banishment Staff",
            "A mage hunter needs a staff that unmakes daemonic summoning on contact.",
            () => SpawnWeapon<GnarledStaff>("Banishment Staff"), "Gnarled Staff",
            14_000, 70, 220,
            R("Hit Dispel", 35), R("Lower Mana Cost", 20)));

        // ── Master tier — High-end combat ─────────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "berserk_sword", "Berserker's Blade",
            "A champion gladiator wants the fastest, hardest-hitting sword money can buy.",
            () => SpawnWeapon<Longsword>("Berserker's Blade"), "Longsword",
            28_000, 150, 500,
            R("Damage Increase", 35), R("Swing Speed Increase", 35)));

        All.Add(new ArtificerWorkOrderDef(
            "duel_katana", "Duelist's Katana",
            "An assassin demands a blade that strikes first, strikes true, and strikes often.",
            () => SpawnWeapon<Katana>("Duelist's Katana"), "Katana",
            28_000, 150, 500,
            R("Hit Chance Increase", 30), R("Swing Speed Increase", 40)));

        All.Add(new ArtificerWorkOrderDef(
            "cripple_axe", "Crippling War Axe",
            "A mercenary captain wants an axe that both cripples opponents and crushes them.",
            () => SpawnWeapon<WarAxe>("Crippling War Axe"), "War Axe",
            25_000, 130, 450,
            R("Hit Lower Attack", 40), R("Damage Increase", 30)));

        All.Add(new ArtificerWorkOrderDef(
            "vampire_kryss", "Vampiric Kryss",
            "A secretive brotherhood wants a blade that feeds on the wounds it inflicts.",
            () => SpawnWeapon<Kryss>("Vampiric Kryss"), "Kryss",
            22_000, 110, 400,
            R("Hit Life Leech", 40)));

        All.Add(new ArtificerWorkOrderDef(
            "mana_bow", "Mana Thief's Bow",
            "A ranger who hunts mages needs a bow that strips them of their power.",
            () => SpawnWeapon<Bow>("Mana Thief's Bow"), "Bow",
            22_000, 110, 400,
            R("Hit Mana Leech", 40)));

        All.Add(new ArtificerWorkOrderDef(
            "dci_ring", "Deflector's Band",
            "A dueling master wants a ring that makes him nearly impossible to land a hit on.",
            () => SpawnJewel<GoldRing>("Deflector's Band"), "Gold Ring",
            20_000, 100, 380,
            R("Defense Chance Increase", 30)));

        All.Add(new ArtificerWorkOrderDef(
            "lrc_ring", "Reagent Saver's Ring",
            "A wealthy arch-mage flat refuses to spend another copper on reagents.",
            () => SpawnJewel<GoldRing>("Reagent Saver's Ring"), "Gold Ring",
            22_000, 110, 400,
            R("Lower Reagent Cost", 75)));

        // ── Slayer orders ─────────────────────────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "slayer_undead", "Undead Hunter's Blade",
            "A necromancer hunter needs a sword blessed specifically against the walking dead.",
            () => SpawnWeapon<Longsword>("Undead Hunter's Blade"), "Longsword",
            30_000, 160, 550,
            R("Slayer: Silver (Undead)", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "slayer_daemon", "Daemon Bane",
            "The City Guard traced a daemon incursion to the sewers. They need a blade for it.",
            () => SpawnWeapon<Katana>("Daemon Bane"), "Katana",
            35_000, 180, 600,
            R("Slayer: Daemon Dismissal", 1), R("Hit Dispel", 30)));

        All.Add(new ArtificerWorkOrderDef(
            "slayer_dragon", "Dragonheart Cleaver",
            "A bold adventurer plans to challenge the ancient wyrm in Destard.",
            () => SpawnWeapon<WarAxe>("Dragonheart Cleaver"), "War Axe",
            30_000, 160, 550,
            R("Slayer: Dragon Slaying", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "slayer_snake", "Serpent's End",
            "A merchant's caravan was ambushed by ophidians outside Ter Mur. He wants revenge.",
            () => SpawnWeapon<Kryss>("Serpent's End"), "Kryss",
            30_000, 160, 550,
            R("Slayer: Snake's Bane", 1)));

        All.Add(new ArtificerWorkOrderDef(
            "slayer_spider", "Spider's Ruin",
            "A miners' guild leader lost three men to a giant spider nest in Dungeon Wrong.",
            () => SpawnWeapon<WarAxe>("Spider's Ruin"), "War Axe",
            30_000, 160, 550,
            R("Slayer: Arachnid Doom", 1)));

        // ── Combo orders (Smith + Artificer) ──────────────────────────────────

        All.Add(new ArtificerWorkOrderDef(
            "combo_gold_berserker", "Gold Berserker's Blade",
            "A warlord insists only a gold-forged blade can hold the enchantments he demands. " +
            "Forge a gold longsword yourself, then bring it enchanted to specification.",
            typeof(Longsword), CraftResource.Gold, "Gold Longsword (self-crafted)",
            40_000, 200, 700, 100, 300,
            R("Damage Increase", 35), R("Swing Speed Increase", 30)));

        All.Add(new ArtificerWorkOrderDef(
            "combo_agapite_duelist", "Agapite Duelist's Edge",
            "Agapite's natural edge holds enchantments better than common steel. " +
            "Craft an agapite katana, then bring it enchanted to specification.",
            typeof(Katana), CraftResource.Agapite, "Agapite Katana (self-crafted)",
            42_000, 210, 750, 100, 300,
            R("Hit Chance Increase", 30), R("Hit Lower Defense", 35)));

        All.Add(new ArtificerWorkOrderDef(
            "combo_verite_dragon", "Verite Dragonslayer",
            "Dragon scales yield only to verite. Forge a verite war axe and imbue it " +
            "with properties that dragons fear.",
            typeof(WarAxe), CraftResource.Verite, "Verite War Axe (self-crafted)",
            55_000, 250, 900, 150, 400,
            R("Slayer: Dragon Slaying", 1), R("Hit Fire Area", 30)));

        All.Add(new ArtificerWorkOrderDef(
            "combo_valorite_daemon", "Valorite Daemon Bane",
            "The ultimate commission: a valorite blade tuned to banish daemon-kind. " +
            "Forge the blade, then enchant it.",
            typeof(Longsword), CraftResource.Valorite, "Valorite Longsword (self-crafted)",
            60_000, 280, 1_000, 150, 400,
            R("Slayer: Daemon Dismissal", 1), R("Hit Dispel", 40)));
    }
}

// ── Gump ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Work order board for the Artificers' Order.
///
/// Stages:
///   List   — shows current active order (if any) + available orders
///   Detail — shows one order's full spec; Accept / Turn In button
/// </summary>
public sealed class ArtificerWorkOrderGump : Gump
{
    public enum Stage { List, Detail }

    private readonly PlayerMobile _pm;
    private readonly Stage        _stage;
    private readonly int          _detailIdx;   // index into ArtificerWorkOrderCatalogue.All
    private readonly int          _page;

    private const int W         = 580;
    private const int H         = 520;
    private const int BgId      = 9270;
    private const int RowsPerPg = 6;

    public ArtificerWorkOrderGump(
        PlayerMobile pm,
        Stage stage    = Stage.List,
        int detailIdx  = -1,
        int page       = 0)
        : base(60, 40)
    {
        _pm        = pm;
        _stage     = stage;
        _detailIdx = detailIdx;
        _page      = page;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);
        AddLabel(W / 2 - 100, 12, 1153, "Artificers' Work Orders");
        AddImageTiled(10, 32, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct)
            : new ClusterFAccountData();

        var isSmithMember = data.JoinedGuilds.Contains("smithing");

        switch (_stage)
        {
            case Stage.List:   DrawList(data, isSmithMember);   break;
            case Stage.Detail: DrawDetail(data, isSmithMember); break;
        }

        AddImageTiled(10, H - 42, W - 20, 2, 9304);

        if (_stage == Stage.Detail)
        {
            AddButton(18, H - 32, 4014, 4015, 1);
            AddLabel(40, H - 30, 999, "Back");
        }

        AddButton(W - 50, H - 32, 4023, 4025, 0);
        AddLabel(W - 28, H - 30, 1153, "X");
    }

    // ── List view ─────────────────────────────────────────────────────────────

    private void DrawList(ClusterFAccountData data, bool isSmithMember)
    {
        var y = 40;

        // ── Active order panel ────────────────────────────────────────────────
        if (data.HasActiveArtificerOrder)
        {
            var active = ArtificerWorkOrderCatalogue.All
                .Find(d => d.Key == data.ActiveArtificerOrderKey);

            if (active != null)
            {
                AddLabel(18, y, 1153, $"Active Commission: {active.Title}");
                y += 18;

                // Requirements summary
                var reqLine = string.Join(", ", active.Requirements.Select(r => $"{r.PropertyName} ≥ {r.MinValue}"));
                AddLabel(28, y, 999, $"Requires: {reqLine}");
                y += 16;

                // Item location status
                var ready = IsReadyToTurnIn(active, data, out _);
                if (active.ItemFactory != null)
                {
                    var itemInPack = FindItemBySerial(data.ActiveArtificerItemSerial);
                    var itemHue    = itemInPack != null ? 0x44 : 0x22;
                    var itemLabel  = itemInPack != null
                        ? $"Item in pack: {active.ItemDescription}"
                        : $"Item MISSING from pack: {active.ItemDescription}";
                    AddLabel(28, y, itemHue, itemLabel);
                }
                else
                {
                    AddLabel(28, y, ready ? 0x44 : 0x3B2,
                        ready ? $"Crafted item found and enchanted" : $"Craft + enchant a {active.ItemDescription}");
                }
                y += 16;

                if (ready)
                {
                    AddButton(28, y, 4023, 4025, 900);
                    AddLabel(54, y + 2, 0x44, "Turn In →");
                }
                else
                {
                    AddLabel(28, y, 0x3B2, "(Enchant the item to specification first)");
                }

                // Abandon sits top-right of the active panel
                AddButton(W - 100, 40, 4011, 4012, 901);
                AddLabel(W - 74, 42, 0x22, "Abandon");

                y += 26;
                AddImageTiled(10, y, W - 20, 1, 9304);
                y += 8;
            }
        }

        // ── Available orders ──────────────────────────────────────────────────
        AddLabel(18, y, 1153, "Available Commissions:");
        y += 18;
        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 6;

        var available = ArtificerWorkOrderCatalogue.All
            .Select((def, catalogueIdx) => (def, catalogueIdx))
            .Where(x => ArtificerWorkOrderCatalogue.IsAvailable(x.def, data, isSmithMember))
            .ToList();

        if (available.Count == 0)
        {
            AddLabel(28, y, 0x3B2,
                "No commissions available yet — master more properties to unlock them.");
        }
        else
        {
            var startIdx = _page * RowsPerPg;
            var rowH     = 26;

            for (var listI = startIdx; listI < available.Count && listI < startIdx + RowsPerPg; listI++)
            {
                var (def, _) = available[listI];
                var isActive = data.HasActiveArtificerOrder
                               && data.ActiveArtificerOrderKey == def.Key;
                var comboTag = def.RequiresSmithMembership ? " [S+A]" : "";
                var hue      = isActive ? 0x44 : (def.RequiresSmithMembership ? 1154 : 999);

                var rowLabel = isActive
                    ? $"[ACTIVE] {def.Title}"
                    : $"{def.Title}{comboTag}  — {def.GoldReward:N0}g + {def.ArtificerScripReward} shards";

                if (!isActive && !data.HasActiveArtificerOrder)
                {
                    AddButton(18, y, 4011, 4012, 100 + listI);
                    AddLabel(46, y + 2, hue, rowLabel);
                }
                else
                {
                    AddLabel(28, y + 2, hue, rowLabel);
                }
                y += rowH;
            }

            // Pagination
            var footY = H - 72;
            if (_page > 0)
            {
                AddButton(18, footY, 4014, 4015, 2);
                AddLabel(40, footY + 2, 999, "Prev");
            }
            if (startIdx + RowsPerPg < available.Count)
            {
                AddButton(120, footY, 4005, 4006, 3);
                AddLabel(142, footY + 2, 999, "Next");
            }
        }

        if (data.HasActiveArtificerOrder && available.Count > 0)
            AddLabel(18, H - 48, 0x3B2, "Complete or abandon your active commission to accept another.");
    }

    // ── Detail view ───────────────────────────────────────────────────────────

    private void DrawDetail(ClusterFAccountData data, bool isSmithMember)
    {
        if (_detailIdx < 0 || _detailIdx >= ArtificerWorkOrderCatalogue.All.Count)
        {
            AddLabel(18, 40, 0x22, "Invalid commission reference.");
            return;
        }

        var def      = ArtificerWorkOrderCatalogue.All[_detailIdx];
        var isActive = data.HasActiveArtificerOrder
                       && data.ActiveArtificerOrderKey == def.Key;
        var y        = 40;

        // Title + type tag
        AddLabel(18, y, 1153, def.Title);
        if (def.RequiresSmithMembership)
            AddLabel(350, y, 1154, "[Smith + Artificer Commission]");
        y += 22;

        // Flavour text
        AddHtml(16, y, W - 32, 52,
            $"<BASEFONT COLOR=#AAAAAA>{def.Flavor}</BASEFONT>", false, false);
        y += 58;

        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // Spec
        AddLabel(18, y, 1153, "Commission Specification:");
        y += 18;

        if (def.ItemFactory != null)
        {
            AddLabel(28, y, 999, $"Item provided by the Order:  {def.ItemDescription}");
        }
        else
        {
            AddLabel(28, y, 1154,
                $"You must craft:  {def.ItemDescription}  (requires Smiths' Fellowship)");
        }
        y += 18;

        foreach (var req in def.Requirements)
        {
            AddLabel(28, y, 999, $"•  {req.PropertyName}  ≥  {req.MinValue}");
            y += 16;
        }

        y += 8;
        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // Rewards
        AddLabel(18, y, 1153, "Reward:");
        y += 18;
        AddLabel(28, y, 0x44, $"•  {def.GoldReward:N0} Gold");                           y += 16;
        AddLabel(28, y, 0x44, $"•  {def.ArtificerScripReward} Essence Shards");          y += 16;
        AddLabel(28, y, 0x44, $"•  {def.ArtificerRepReward} Artificers' Standing");      y += 16;
        if (def.RequiresSmithMembership)
        {
            AddLabel(28, y, 1154,
                $"•  {def.SmithScripReward} Smiths' Scrip  +  {def.SmithRepReward} Smiths' Standing");
            y += 16;
        }

        y += 8;
        AddImageTiled(10, y, W - 20, 1, 9304);
        y += 8;

        // Action
        if (isActive)
        {
            var ready = IsReadyToTurnIn(def, data, out _);
            if (ready)
            {
                AddButton(18, y, 4023, 4025, 900);
                AddLabel(44, y + 2, 0x44, "Turn In Commission");
            }
            else
            {
                AddLabel(18, y, 0x3B2,
                    "Commission in progress — enchant the item to spec, then return to turn it in.");
            }
        }
        else if (!data.HasActiveArtificerOrder)
        {
            if (ArtificerWorkOrderCatalogue.IsAvailable(def, data, isSmithMember))
            {
                AddButton(18, y, 4023, 4025, 800);
                AddLabel(44, y + 2, 0x44, "Accept Commission");
            }
            else
            {
                AddLabel(18, y, 0x22, "You do not meet the mastery requirements for this commission.");
            }
        }
        else
        {
            AddLabel(18, y, 0x3B2, "Complete or abandon your active commission before accepting another.");
        }
    }

    // ── Turn-in check ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if an item meeting the order requirements is in the player's backpack.
    /// For regular orders the item must match the stored serial.
    /// For combo orders any matching type + resource combination is accepted.
    /// </summary>
    private bool IsReadyToTurnIn(
        ArtificerWorkOrderDef def, ClusterFAccountData data, out Item? foundItem)
    {
        foundItem = null;
        if (_pm.Backpack == null) return false;

        if (def.ItemFactory != null)
        {
            // Regular order — must be the specific item we handed out
            foundItem = FindItemBySerial(data.ActiveArtificerItemSerial);
            return foundItem != null
                   && ArtificerWorkOrderCatalogue.ItemMeetsRequirements(foundItem, def);
        }

        // Combo order — player-crafted; match by type + resource + enchantments
        foreach (var item in _pm.Backpack.Items)
        {
            if (def.RequiredItemType != null && item.GetType() != def.RequiredItemType) continue;
            if (item is not BaseWeapon bw || bw.Resource != def.RequiredResource)       continue;
            if (!ArtificerWorkOrderCatalogue.ItemMeetsRequirements(item, def))          continue;
            foundItem = item;
            return true;
        }
        return false;
    }

    private Item? FindItemBySerial(uint serial)
    {
        if (_pm.Backpack == null) return null;
        foreach (var item in _pm.Backpack.Items)
            if ((uint)item.Serial == serial) return item;
        return null;
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        var acct = _pm.Account as IAccount;
        if (acct == null) return;

        var data          = ClusterFAccountPersistence.GetOrCreate(acct);
        var isSmithMember = data.JoinedGuilds.Contains("smithing");

        switch (info.ButtonID)
        {
            case 1:   // Back to list (from detail)
                _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.List, -1, _page));
                return;

            case 2:   // Prev page
                _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.List, -1, Math.Max(0, _page - 1)));
                return;

            case 3:   // Next page
                _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.List, -1, _page + 1));
                return;

            case 800: // Accept (from detail view)
                HandleAccept(data, isSmithMember);
                return;

            case 900: // Turn in
                HandleTurnIn(data, isSmithMember);
                return;

            case 901: // Abandon
                HandleAbandon(data);
                return;
        }

        // Order row click (list view): open detail
        if (info.ButtonID >= 100)
        {
            var listI = info.ButtonID - 100;
            var available = ArtificerWorkOrderCatalogue.All
                .Select((def, catalogueIdx) => (def, catalogueIdx))
                .Where(x => ArtificerWorkOrderCatalogue.IsAvailable(x.def, data, isSmithMember))
                .ToList();

            if (listI >= 0 && listI < available.Count)
            {
                var (_, catalogueIdx) = available[listI];
                _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.Detail, catalogueIdx, _page));
            }
        }
    }

    // ── Action handlers ───────────────────────────────────────────────────────

    private void HandleAccept(ClusterFAccountData data, bool isSmithMember)
    {
        if (_detailIdx < 0 || _detailIdx >= ArtificerWorkOrderCatalogue.All.Count) return;
        var def = ArtificerWorkOrderCatalogue.All[_detailIdx];

        if (data.HasActiveArtificerOrder)
        {
            _pm.SendMessage(0x22, "You already have an active commission. Complete or abandon it first.");
            _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.Detail, _detailIdx, _page));
            return;
        }

        if (!ArtificerWorkOrderCatalogue.IsAvailable(def, data, isSmithMember))
        {
            _pm.SendMessage(0x22, "You do not meet the requirements for this commission.");
            _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.Detail, _detailIdx, _page));
            return;
        }

        uint itemSerial = 0;

        if (def.ItemFactory != null)
        {
            var item = def.ItemFactory();
            if (!_pm.AddToBackpack(item))
            {
                item.Delete();
                _pm.SendMessage(0x22, "Your backpack is full. Clear some space and try again.");
                _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.Detail, _detailIdx, _page));
                return;
            }
            itemSerial = (uint)item.Serial;
            _pm.SendMessage(0x44, $"Commission accepted: {def.Title}.");
            _pm.SendMessage(0x44, $"A blank {def.ItemDescription} has been placed in your pack. Enchant it to specification.");
        }
        else
        {
            _pm.SendMessage(0x44, $"Commission accepted: {def.Title}.");
            _pm.SendMessage(0x44, $"Craft a {def.ItemDescription} using your smithing skills, then enchant it to specification.");
        }

        _pm.SendMessage(999, "Return here when the item is ready to turn it in.");
        data.AcceptArtificerOrder(def.Key, itemSerial);

        _pm.SendGump(new ArtificerWorkOrderGump(_pm, Stage.List, -1, 0));
    }

    private void HandleTurnIn(ClusterFAccountData data, bool isSmithMember)
    {
        if (!data.HasActiveArtificerOrder) return;

        var def = ArtificerWorkOrderCatalogue.All
            .Find(d => d.Key == data.ActiveArtificerOrderKey);

        if (def == null)
        {
            data.ClearArtificerOrder();
            _pm.SendMessage(0x22, "Commission data was invalid — order cleared.");
            _pm.SendGump(new ArtificerWorkOrderGump(_pm));
            return;
        }

        if (!IsReadyToTurnIn(def, data, out var turnInItem))
        {
            _pm.SendMessage(0x22, "The item does not yet meet the commission requirements:");

            // Report missing requirements
            Item? checkItem = null;
            if (def.ItemFactory != null)
            {
                checkItem = FindItemBySerial(data.ActiveArtificerItemSerial);
            }
            else if (_pm.Backpack != null)
            {
                foreach (var candidate in _pm.Backpack.Items)
                {
                    if (def.RequiredItemType != null && candidate.GetType() != def.RequiredItemType) continue;
                    if (candidate is not BaseWeapon bw2 || bw2.Resource != def.RequiredResource)    continue;
                    checkItem = candidate;
                    break;
                }
            }

            if (checkItem != null)
            {
                foreach (var req in def.Requirements)
                {
                    var propDef = ImbueCatalogue.All.Find(
                        d => d.Name.Equals(req.PropertyName, StringComparison.OrdinalIgnoreCase));
                    if (propDef == null) continue;
                    var cur = propDef.Get(checkItem);
                    if (cur < req.MinValue)
                        _pm.SendMessage(0x22, $"  {req.PropertyName}: {cur} (need ≥ {req.MinValue})");
                }
            }

            _pm.SendGump(new ArtificerWorkOrderGump(_pm,
                _stage == Stage.Detail ? Stage.Detail : Stage.List,
                _detailIdx, _page));
            return;
        }

        // ── Success ────────────────────────────────────────────────────────────
        turnInItem!.Delete();
        data.ClearArtificerOrder();

        data.AddReputation("artificers", def.ArtificerRepReward);
        data.AddCurrency("artificers",   def.ArtificerScripReward);
        _pm.Backpack?.DropItem(new Gold(def.GoldReward));

        if (def.RequiresSmithMembership && def.SmithScripReward > 0)
        {
            data.AddReputation("smithing", def.SmithRepReward);
            data.AddCurrency("smithing",   def.SmithScripReward);
            _pm.SendMessage(1154,
                $"Smiths' reward: +{def.SmithRepReward} standing, +{def.SmithScripReward} scrip.");
        }

        _pm.SendMessage(0x44, $"Commission complete: {def.Title}!");
        _pm.SendMessage(0x44,
            $"Reward: {def.GoldReward:N0} gold  +  {def.ArtificerScripReward} Essence Shards  " +
            $"+  {def.ArtificerRepReward} Artificers' Standing.");
        _pm.PlaySound(0x3D);

        _pm.SendGump(new ArtificerWorkOrderGump(_pm));
    }

    private void HandleAbandon(ClusterFAccountData data)
    {
        if (!data.HasActiveArtificerOrder) return;

        var def = ArtificerWorkOrderCatalogue.All
            .Find(d => d.Key == data.ActiveArtificerOrderKey);

        // Delete the commission item if it's still in the player's pack
        if (def?.ItemFactory != null && data.ActiveArtificerItemSerial != 0)
        {
            var item = FindItemBySerial(data.ActiveArtificerItemSerial);
            item?.Delete();
        }

        data.ClearArtificerOrder();

        _pm.SendMessage(0x22, "Commission abandoned. No reward issued.");
        _pm.SendGump(new ArtificerWorkOrderGump(_pm));
    }
}
