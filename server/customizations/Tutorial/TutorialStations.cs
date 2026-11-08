// cc-P80: the New Haven tutorial's stations, as data. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Every station Chase listed (2026-10-11) is one row here: the required core, then the optional wings, one trial per
// discipline. A row says which skills the trial covers and how (H hands-on, C covered inside another trial, E explained
// by an NPC), the TazUO features its station teaches, the tool it gives, and where in New Haven it will stand.
//
// The framework never needs to change to fill a trial in: a trial is registered by its key in TutorialStations.Handlers
// (a method that runs the trial and calls ClusterFTutorial.CompleteStation when done). A key with no handler runs the
// coming-soon page. Unbuilt wings remain optional and never claim a completed trial.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Tutorial;

public enum TutorialWing
{
    Core,
    Combat,
    Magic,
    Gathering,
    Crafting,
    Animals,
    Bard,
    Thieving,
    Housing,
    Travel,
    Reputation,
    ShardSystems,
    Client // cc-P718 item 3: client settings, optional at any time
}

/// <summary>One station: a core lesson or a wing trial.</summary>
public sealed record TutorialStation(
    string Key,
    TutorialWing Wing,
    string Title,
    string[] HandsOn,      // skills trained by doing (H)
    string[] Covered,      // skills that gain inside this trial (C)
    string[] Explained,    // skills an NPC explains only (E)
    string[] TazUO,        // TazUO features taught here
    string? Tool,          // the tool the trial gives (null: none)
    string Where           // where it will stand (a placeholder until each trial is placed)
)
{
    public bool IsCore => Wing == TutorialWing.Core;
}

public static class TutorialStations
{
    private static readonly string[] None = [];

    public static readonly TutorialStation[] All =
    [
        // -- The required core (in order). cc-P480: the backpack before the paperdoll, since dressing takes the cloak
        //    out of the backpack (Chase 10-23). Records hold done keys, not positions, so saved progress carries on.
        //    cc-P663: on the Wakening Isle, one line west to east (P481 11.2): the farm joins the core after chat, and
        //    travel, the dock moongate out to New Haven, is last (ClusterFTutorial.IsDone counts an old travel key only
        //    once the rest are done).
        new("core.movement", TutorialWing.Core, "Walk and run", None, None, None,
            ["Options", "Global Scaling", "Hotkeys"], null, "the bright tiles in the road, up from the beach"),
        new("core.backpack", TutorialWing.Core, "Your backpack", None, None, None,
            ["Grid Containers", "Item comparison"], null, "the camp beside the road"),
        new("core.paperdoll", TutorialWing.Core, "Your character window and clothes", None, None, None,
            ["Alternate Paperdoll", "Durability gump"], null, "the clothes rack"),
        new("core.chat", TutorialWing.Core, "Speak and read your Journal", None, None, None,
            ["Journal and filters", "TTF fonts", "Nameplates"], null, "the signpost at the meadow's edge"),
        new("core.farm", TutorialWing.Core, "The farm: pests, then your pet", None, None, None,
            ["Pet scaling", "Follow mode"], null, "the farm gate, with Wren the Farmhand"),
        // cc-P726 item 4: bandaging is its own lesson after the farm, at Pip on his bench in the farmhouse.
        new("core.healing", TutorialWing.Core, "Healing: bandage Pip", None, None, None,
            [], null, "the farmhouse, where Pip sits hurt on a bench"),
        new("core.death", TutorialWing.Core, "Death and resurrection", None, None, None,
            ["Info Bar"], null, "the training yard, beside Sera the healer"),
        new("core.banking", TutorialWing.Core, "The bank", None, None, None,
            ["Grid Containers"], null, "the practice bank in the trades square"),
        new("core.league", TutorialWing.Core, "The League of Extraordinary Citizens", None, None, None,
            [], null, "the League lodge"),
        new("core.loop", TutorialWing.Core, "Watch a loop vision", None, None, None,
            ["Skill progress bar"], null, "the standing stones on the knoll"),
        new("core.travel", TutorialWing.Core, "Getting around", None, None, None,
            ["World Map", "Macros"], null, "the moongate at the end of the dock"),

        // -- Combat wing --
        new("combat.weapons", TutorialWing.Combat, "Pick your weapon",
            ["Swords", "Macing", "Fencing", "Archery", "Throwing", "Wrestling"], ["Tactics", "Anatomy", "Parrying", "Focus"],
            ["Resisting Spells"],
            ["Custom health bars", "Nameplate healthbars", "Damage hues", "Buff and cooldown bars"], "a loaned weapon you choose",
            "the Proving Grounds, the warrior training yard on the east side of town"), // cc-P743 item 1
        new("combat.healing", TutorialWing.Combat, "Bandages", ["Healing"], ["Anatomy"], None,
            ["Auto Bandage"], "loaned bandages", "the training yard, behind the Little Shop of Magic"),

        // -- Magic wing --
        new("magic.magery", TutorialWing.Magic, "Magery", ["Magery"], ["Meditation", "Evaluating Intelligence"], None,
            ["SpellBar", "Quick spell cast", "Spell indicators", "Counter bar"], "a loaned spellbook and reagents (spell ingredients)", "the Haven Magery School, the stone hall at the north end of town"), // cc-P743 item 7
        new("magic.necromancy", TutorialWing.Magic, "Necromancy", ["Necromancy"], ["Spirit Speak"], None,
            ["SpellBar"], "a loaned necromancer spellbook", "the necromancer's shrine at the north-east of the loop road"), // cc-P802 item 8
        new("magic.chivalry", TutorialWing.Magic, "Chivalry", ["Chivalry"], None, None,
            ["SpellBar"], "a loaned Book of Chivalry", "the training yard, behind the Little Shop of Magic"),
        new("magic.bushido", TutorialWing.Magic, "Bushido", ["Bushido"], None, None,
            ["Buff and cooldown bars"], "a loaned Book of Bushido", "the training yard, behind the Little Shop of Magic"),
        new("magic.ninjitsu", TutorialWing.Magic, "Ninjitsu", ["Ninjitsu"], None, None,
            ["Buff and cooldown bars"], "a loaned Book of Ninjitsu", "the training yard, behind the Little Shop of Magic"),
        new("magic.mysticism", TutorialWing.Magic, "Mysticism", ["Mysticism"], None, ["Spellweaving (start with a quest)"],
            ["SpellBar"], "a loaned mystic book", "the Haven Magery School, with its elven adjunct instructor"), // cc-P802 item 3

        // -- Gathering wing --
        new("gathering.mining", TutorialWing.Gathering, "Mining", ["Mining"], None, None,
            ["Counter bar"], "a loaned pickaxe", "the cave on the mountain path south of town"), // cc-P743 item 3
        new("gathering.lumberjacking", TutorialWing.Gathering, "Lumberjacking", ["Lumberjacking"], None, None,
            [], "a loaned hatchet", "the Woodsman's lodge"),
        new("gathering.fishing", TutorialWing.Gathering, "Fishing", ["Fishing"], None, None,
            [], "a loaned fishing pole", "the docks"),
        new("gathering.camping", TutorialWing.Gathering, "Camping", ["Camping"], None, ["Herbalism (the wild garlic in the yard)"],
            [], "loaned kindling", "the camp spot by the road to the east fields"),

        // -- Crafting yard, one item each --
        new("crafting.blacksmithy", TutorialWing.Crafting, "Blacksmithy", ["Blacksmithy"], ["Arms Lore"], ["Imbuing"],
            ["Counter bar", "Grid highlighting by property", "Item database", "Organizer agent"], "a loaned smith's hammer", "the Hammer and Steel smithy"),
        new("crafting.tailoring", TutorialWing.Crafting, "Tailoring", ["Tailoring"], None, None,
            ["Counter bar"], "a loaned sewing kit", "A Stitch in Time"),
        new("crafting.carpentry", TutorialWing.Crafting, "Carpentry", ["Carpentry"], None, None,
            [], "a loaned saw", "Carpenters of Haven"),
        new("crafting.tinkering", TutorialWing.Crafting, "Tinkering", ["Tinkering"], ["Item Identification"], None,
            [], "loaned tinker's tools", "the Haven Clockworks and Tinker Shop"),
        new("crafting.alchemy", TutorialWing.Crafting, "Alchemy", ["Alchemy"], ["Taste Identification"], None,
            [], "a loaned mortar and pestle", "the reagent kitchen behind the mage's shop"),
        new("crafting.fletching", TutorialWing.Crafting, "Bowcraft and Fletching", ["Fletching"], None, None,
            [], "loaned fletcher's tools", "the Bullseye Marksmen archery range"),
        new("crafting.cooking", TutorialWing.Crafting, "Cooking", ["Cooking"], None, None,
            [], "a loaned skillet", "the Albatross kitchen"),
        new("crafting.inscription", TutorialWing.Crafting, "Inscription", ["Inscription"], None, None,
            [], "a loaned scribe's pen", "the Haven Public Library"),
        new("crafting.cartography", TutorialWing.Crafting, "Cartography", ["Cartography"], None, None,
            ["Treasure maps and SOS", "SOS locator"], "a loaned mapmaker's pen", "Mapmakers of Haven"),

        // -- Animals --
        // cc-P743 item 4: heal and tame an injured horse in the stables, and keep it. Herding is explained, not practised.
        new("animals.taming", TutorialWing.Animals, "Animals: heal and tame a horse", ["Veterinary", "Animal Taming"], ["Animal Lore"], ["Herding"],
            ["Pet scaling", "Follow mode"], "loaned bandages if you have none", "the stables beside Uzeraan's mansion, west of the square"),

        // -- Bard (one trial) --
        new("bard.trial", TutorialWing.Bard, "Music and bard skills", ["Musicianship", "Peacemaking", "Provocation", "Discordance"],
            None, None, [], "a loaned lute", "the Bardic Guild's hall, north of the square"), // cc-P743 item 7

        // -- Thieving and stealth (one trial) --
        new("thieving.trial", TutorialWing.Thieving, "Thieving and stealth",
            ["Hiding", "Stealth", "Detecting Hidden", "Lockpicking", "Remove Trap", "Snooping", "Stealing", "Poisoning", "Tracking", "Begging"],
            None, ["Forensic Evaluation"], ["Hidden characters"], "loaned lockpicks", "the Haven Thieves' Guild"),

        // -- Non-skill wings --
        new("housing.basics", TutorialWing.Housing, "Houses", None, None, None,
            ["Transparent houses", "Circle of transparency"], null, "the small cottage east of the square, the field south of it, then the bank"), // cc-P743 item 9
        new("travel.wing", TutorialWing.Travel, "Runes, gates and boats", None, None, None,
            ["World Map"], "a loaned blank recall rune", "the public moongate, then Rook's bench by the Albatross tavern"),
        new("reputation.wing", TutorialWing.Reputation, "Name colors, reputation, and guards", None, None, None,
            [], null, HavenSpots.JailWhere), // cc-P801
        new("shard.systems", TutorialWing.ShardSystems, "Rewards and extra jewelry slots", None, None, None,
            ["Skill progress bar", "Autoloot", "Scavenger", "Nearby loot", "Vendors: advanced shop, trade, auto buy and sell",
             "Legion scripting (advanced)", "Script library browser (advanced)"], null, "Elara Voss's house, the bank, then Rook"),

        // -- cc-P718 item 3: optional at any time, on the isle (Tamsin's window) or with Rook; never needed to graduate --
        new(TutorialClientTips.Key, TutorialWing.Client, "Client tips: always run", None, None, None,
            ["Options"], null, "Tamsin's window or Rook's, at any time"),
    ];

    /// <summary>cc-P718: optional lessons offered at any time, before the core is done too (Tamsin's window shows them).</summary>
    public static bool IsAnytime(string key) => key.Equals(TutorialClientTips.Key, StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<TutorialStation> Anytime
    {
        get
        {
            foreach (var s in All)
            {
                if (IsAnytime(s.Key))
                {
                    yield return s;
                }
            }
        }
    }

    /// <summary>cc-P663: the last core lesson, the dock moongate out to New Haven.</summary>
    public const string LastCore = "core.travel";

    public static TutorialStation? Find(string key) => Array.Find(All, s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<TutorialStation> Core
    {
        get
        {
            foreach (var s in All)
            {
                if (s.IsCore)
                {
                    yield return s;
                }
            }
        }
    }

    /// <summary>
    /// The trials, by station key. A trial runs its lesson and calls ClusterFTutorial.CompleteStation when the player
    /// finishes. Core handlers observe actions; future wing prompts fill the optional keys.
    /// </summary>
    public static readonly Dictionary<string, Action<PlayerMobile, TutorialStation>> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["core.movement"] = TutorialCoreTrials.Start,
        ["core.paperdoll"] = TutorialCoreTrials.Start,
        ["core.backpack"] = TutorialCoreTrials.Start,
        ["core.chat"] = TutorialCoreTrials.Start,
        ["core.farm"] = TutorialCoreTrials.Start,
        ["core.death"] = TutorialCoreTrials.Start,
        ["core.banking"] = TutorialCoreTrials.Start,
        ["core.travel"] = TutorialCoreTrials.Start,
        ["core.league"] = TutorialCoreTrials.Start,
        ["core.loop"] = TutorialCoreTrials.Start,
        [TutorialClientTips.Key] = TutorialClientTips.Start, // cc-P718
    };
}
