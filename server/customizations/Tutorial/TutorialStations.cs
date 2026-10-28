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
        new("core.farm", TutorialWing.Core, "The farm: your pet, pests and bandages", None, None, None,
            ["Pet scaling", "Follow mode"], null, "the farm gate, with Wren the Farmhand"),
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
            "the training yard, behind the Little Shop of Magic"),
        new("combat.healing", TutorialWing.Combat, "Bandages", ["Healing"], ["Anatomy"], None,
            ["Auto Bandage"], "loaned bandages", "the training yard, behind the Little Shop of Magic"),

        // -- Magic wing --
        new("magic.magery", TutorialWing.Magic, "Magery", ["Magery"], ["Meditation", "Evaluating Intelligence"], None,
            ["SpellBar", "Quick spell cast", "Spell indicators", "Counter bar"], "a loaned spellbook and reagents (spell ingredients)", "the training yard, behind the Little Shop of Magic"),
        new("magic.necromancy", TutorialWing.Magic, "Necromancy", ["Necromancy"], ["Spirit Speak"], None,
            ["SpellBar"], "a loaned necromancer spellbook", "the training yard, behind the Little Shop of Magic"),
        new("magic.chivalry", TutorialWing.Magic, "Chivalry", ["Chivalry"], None, None,
            ["SpellBar"], "a loaned Book of Chivalry", "the training yard, behind the Little Shop of Magic"),
        new("magic.bushido", TutorialWing.Magic, "Bushido", ["Bushido"], None, None,
            ["Buff and cooldown bars"], "a loaned Book of Bushido", "the training yard, behind the Little Shop of Magic"),
        new("magic.ninjitsu", TutorialWing.Magic, "Ninjitsu", ["Ninjitsu"], None, None,
            ["Buff and cooldown bars"], "a loaned Book of Ninjitsu", "the training yard, behind the Little Shop of Magic"),
        new("magic.mysticism", TutorialWing.Magic, "Mysticism", ["Mysticism"], None, ["Spellweaving (start with a quest)"],
            ["SpellBar"], "a loaned mystic book", "the training yard, behind the Little Shop of Magic"),

        // -- Gathering wing --
        new("gathering.mining", TutorialWing.Gathering, "Mining", ["Mining"], None, None,
            ["Counter bar"], "a loaned pickaxe", "the trades yard, west of the training yard"),
        new("gathering.lumberjacking", TutorialWing.Gathering, "Lumberjacking", ["Lumberjacking"], None, None,
            [], "a loaned hatchet", "the trades yard, west of the training yard"),
        new("gathering.fishing", TutorialWing.Gathering, "Fishing", ["Fishing"], None, None,
            [], "a loaned fishing pole", "the trades yard, west of the training yard"),
        new("gathering.camping", TutorialWing.Gathering, "Camping", ["Camping"], None, ["Herbalism (not available yet)"],
            [], "loaned kindling", "the trades yard, west of the training yard"),

        // -- Crafting yard, one item each --
        new("crafting.blacksmithy", TutorialWing.Crafting, "Blacksmithy", ["Blacksmithy"], ["Arms Lore"], ["Imbuing"],
            ["Counter bar", "Grid highlighting by property", "Item database", "Organizer agent"], "a loaned smith's hammer", "the trades yard, west of the training yard"),
        new("crafting.tailoring", TutorialWing.Crafting, "Tailoring", ["Tailoring"], None, None,
            ["Counter bar"], "a loaned sewing kit", "the trades yard, west of the training yard"),
        new("crafting.carpentry", TutorialWing.Crafting, "Carpentry", ["Carpentry"], None, None,
            [], "a loaned saw", "the trades yard, west of the training yard"),
        new("crafting.tinkering", TutorialWing.Crafting, "Tinkering", ["Tinkering"], ["Item Identification"], None,
            [], "loaned tinker's tools", "the trades yard, west of the training yard"),
        new("crafting.alchemy", TutorialWing.Crafting, "Alchemy", ["Alchemy"], ["Taste Identification"], None,
            [], "a loaned mortar and pestle", "the trades yard, west of the training yard"),
        new("crafting.fletching", TutorialWing.Crafting, "Bowcraft and Fletching", ["Fletching"], None, None,
            [], "loaned fletcher's tools", "the trades yard, west of the training yard"),
        new("crafting.cooking", TutorialWing.Crafting, "Cooking", ["Cooking"], None, None,
            [], "a loaned skillet", "the trades yard, west of the training yard"),
        new("crafting.inscription", TutorialWing.Crafting, "Inscription", ["Inscription"], None, None,
            [], "a loaned scribe's pen", "the trades yard, west of the training yard"),
        new("crafting.cartography", TutorialWing.Crafting, "Cartography", ["Cartography"], None, None,
            ["Treasure maps and SOS", "SOS locator"], "a loaned mapmaker's pen", "the trades yard, west of the training yard"),

        // -- Animals --
        new("animals.taming", TutorialWing.Animals, "Animals", ["Animal Taming", "Animal Lore", "Veterinary", "Herding"], None, None,
            ["Pet scaling", "Follow mode"], "a loaned shepherd's crook", "the farm's animal pen, through the gate"),

        // -- Bard (one trial) --
        new("bard.trial", TutorialWing.Bard, "Music and bard skills", ["Musicianship", "Peacemaking", "Provocation", "Discordance"],
            None, None, [], "a loaned lute", "the trades yard, west of the training yard"),

        // -- Thieving and stealth (one trial) --
        new("thieving.trial", TutorialWing.Thieving, "Thieving and stealth",
            ["Hiding", "Stealth", "Detecting Hidden", "Lockpicking", "Remove Trap", "Snooping", "Stealing", "Poisoning", "Tracking", "Begging"],
            None, ["Forensic Evaluation"], ["Hidden characters"], "loaned lockpicks", "the trades yard, west of the training yard"),

        // -- Non-skill wings --
        new("housing.basics", TutorialWing.Housing, "Houses", None, None, None,
            ["Transparent houses", "Circle of transparency"], null, "the farmhouse porch, then the bank"),
        new("travel.wing", TutorialWing.Travel, "Runes, gates and boats", None, None, None,
            ["World Map"], "a loaned blank recall rune", "the farm gate, then the New Haven plaza"),
        new("reputation.wing", TutorialWing.Reputation, "Name colors, reputation, and guards", None, None, None,
            [], null, "the training yard, Sera the healer, then the plaza"),
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
