// cc-P80: the New Haven tutorial's stations, as data. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Every station Chase listed (2026-10-11) is one row here: the required core, then the optional wings, one trial per
// discipline. A row says which skills the trial covers and how (H hands-on, C covered inside another trial, E explained
// by an NPC), the TazUO features its station teaches, the tool it gives, and where in New Haven it will stand.
//
// The framework never needs to change to fill a trial in: a trial is registered by its key in TutorialStations.Handlers
// (a method that runs the trial and calls ClusterFTutorial.CompleteStation when done). A key with no handler runs the
// stub, which shows the station's plan in a gump and marks it done, so the whole path can be walked today. Every stub is
// a TODO for the trial prompts that follow.

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
    ShardSystems
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
        // -- The required core (in order) --
        new("core.movement", TutorialWing.Core, "Walking, running and the camera", None, None, None,
            ["Options", "Global Scaling", "Hotkeys"], null, "New Haven plaza"),
        new("core.paperdoll", TutorialWing.Core, "Your paperdoll", None, None, None,
            ["Alternate Paperdoll", "Durability gump"], null, "New Haven plaza"),
        new("core.backpack", TutorialWing.Core, "Your backpack", None, None, None,
            ["Grid Containers", "Item comparison"], null, "New Haven plaza"),
        new("core.chat", TutorialWing.Core, "Speaking and the journal", None, None, None,
            ["Journal and filters", "TTF fonts", "Nameplates"], null, "New Haven plaza"),
        new("core.death", TutorialWing.Core, "Death and resurrection", None, None, None,
            ["Info Bar"], null, "the Healer's Hall and the graveyard"),
        new("core.banking", TutorialWing.Core, "The bank", None, None, None,
            ["Grid Containers"], null, "New Haven Bank"),
        new("core.travel", TutorialWing.Core, "Getting around", None, None, None,
            ["World Map", "Macros"], null, "the moongate"),
        new("core.league", TutorialWing.Core, "The League of Extraordinary Citizens", None, None, None,
            [], null, "the League field office"),
        new("core.loop", TutorialWing.Core, "The loop (a vision)", None, None, None,
            ["Skill progress bar"], null, "a vision near the sword fragment"),

        // -- Combat wing --
        new("combat.weapons", TutorialWing.Combat, "Pick your weapon",
            ["Swords", "Macing", "Fencing", "Archery", "Throwing", "Wrestling"], ["Tactics", "Anatomy", "Parrying", "Focus"],
            ["Resisting Spells"],
            ["Custom health bars", "Nameplate healthbars", "Damage hues", "Buff and cooldown bars"], "a starter weapon of your pick",
            "the combat yard"),
        new("combat.healing", TutorialWing.Combat, "Bandages", ["Healing"], ["Anatomy"], None,
            ["Auto Bandage"], "bandages", "the combat yard"),

        // -- Magic wing --
        new("magic.magery", TutorialWing.Magic, "Magery", ["Magery"], ["Meditation", "Eval Int"], None,
            ["SpellBar", "Quick spell cast", "Spell indicators", "Counter bar"], "a spellbook and reagents", "the Magery school"),
        new("magic.necromancy", TutorialWing.Magic, "Necromancy", ["Necromancy"], ["Spirit Speak"], None,
            ["SpellBar"], "a necromancer spellbook", "the Necromancers Guild Hall"),
        new("magic.chivalry", TutorialWing.Magic, "Chivalry", ["Chivalry"], None, None,
            ["SpellBar"], "a book of chivalry", "the Warrior's Guild Hall"),
        new("magic.bushido", TutorialWing.Magic, "Bushido", ["Bushido"], None, None,
            ["Buff and cooldown bars"], "a book of bushido", "the Ninja Dojo"),
        new("magic.ninjitsu", TutorialWing.Magic, "Ninjitsu", ["Ninjitsu"], None, None,
            ["Buff and cooldown bars"], "a book of ninjitsu", "the Ninja Dojo"),
        new("magic.mysticism", TutorialWing.Magic, "Mysticism", ["Mysticism"], None, ["Spellweaving (its quest)"],
            ["SpellBar"], "a mysticism spellbook", "the Magery school"),

        // -- Gathering wing --
        new("gathering.mining", TutorialWing.Gathering, "Mining", ["Mining"], None, None,
            ["Counter bar"], "a pickaxe", "the mine camp"),
        new("gathering.lumberjacking", TutorialWing.Gathering, "Lumberjacking", ["Lumberjacking"], None, None,
            [], "a hatchet", "the tutorial farm's edge"),
        new("gathering.fishing", TutorialWing.Gathering, "Fishing", ["Fishing"], None, None,
            [], "a fishing pole", "New Haven docks"),
        new("gathering.camping", TutorialWing.Gathering, "Camping", ["Camping"], None, ["Herbalism (Order of the Alembic, not built)"],
            [], "kindling", "the tutorial farm"),

        // -- Crafting yard, one item each --
        new("crafting.blacksmithy", TutorialWing.Crafting, "Blacksmithy", ["Blacksmithy"], ["Arms Lore"], ["Imbuing"],
            ["Counter bar", "Grid highlighting by property", "Item database", "Organizer agent"], "a smith's hammer", "Forge and Anvil"),
        new("crafting.tailoring", TutorialWing.Crafting, "Tailoring", ["Tailoring"], None, None,
            ["Counter bar"], "a sewing kit", "A Stitch In Time"),
        new("crafting.carpentry", TutorialWing.Crafting, "Carpentry", ["Carpentry"], None, None,
            [], "a saw", "Carpenters of New Haven"),
        new("crafting.tinkering", TutorialWing.Crafting, "Tinkering", ["Tinkering"], ["Item ID"], None,
            [], "tinker's tools", "Springs N Things"),
        new("crafting.alchemy", TutorialWing.Crafting, "Alchemy", ["Alchemy"], ["Taste ID"], None,
            [], "a mortar and pestle", "the Little Shop of Alchemy"),
        new("crafting.fletching", TutorialWing.Crafting, "Bowcraft and Fletching", ["Fletching"], None, None,
            [], "fletcher's tools", "the Bowyer"),
        new("crafting.cooking", TutorialWing.Crafting, "Cooking", ["Cooking"], None, None,
            [], "a skillet", "the Bountiful Harvest Inn"),
        new("crafting.inscription", TutorialWing.Crafting, "Inscription", ["Inscription"], None, None,
            [], "a scribe's pen", "the Magery school"),
        new("crafting.cartography", TutorialWing.Crafting, "Cartography", ["Cartography"], None, None,
            ["Treasure maps and SOS", "SOS locator"], "a mapmaker's pen", "the Mapmaker"),

        // -- Animals --
        new("animals.taming", TutorialWing.Animals, "Animals", ["Animal Taming", "Animal Lore", "Veterinary", "Herding"], None, None,
            ["Pet scaling", "Follow mode"], "a shepherd's crook", "the tutorial farm pen"),

        // -- Bard (one trial) --
        new("bard.trial", TutorialWing.Bard, "The bard's trial", ["Musicianship", "Peacemaking", "Provocation", "Discordance"],
            None, None, [], "a lute", "the Bardic Guild"),

        // -- Thieving and stealth (one trial) --
        new("thieving.trial", TutorialWing.Thieving, "The thief's trial",
            ["Hiding", "Stealth", "Detecting Hidden", "Lockpicking", "Remove Trap", "Snooping", "Stealing", "Poisoning", "Tracking", "Begging"],
            None, ["Forensic Evaluation"], ["Hidden characters"], "lockpicks", "the Bountiful Harvest Inn, back room"),

        // -- Non-skill wings --
        new("housing.basics", TutorialWing.Housing, "Houses", None, None, None,
            ["Transparent houses", "Circle of transparency"], null, "a model house"),
        new("travel.wing", TutorialWing.Travel, "Runes, gates and boats", None, None, None,
            ["World Map"], "a recall rune", "the moongate"),
        new("reputation.wing", TutorialWing.Reputation, "Karma, fame, flagging and guards", None, None, None,
            [], null, "the town guard post"),
        new("shard.systems", TutorialWing.ShardSystems, "Levels, loops, loot boxes, achievements, ring slots", None, None, None,
            ["Skill progress bar", "Autoloot", "Scavenger", "Nearby loot", "Vendors: advanced shop, trade, auto buy and sell",
             "Legion scripting (advanced)", "Script library browser (advanced)"], null, "the League field office"),
    ];

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
    /// finishes. None is written yet: every station runs the stub (TODO: one trial prompt per wing).
    /// </summary>
    public static readonly Dictionary<string, Action<PlayerMobile, TutorialStation>> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        // The loop vision is the one core step built in this pass: it must never touch the real character.
        ["core.loop"] = (pm, station) => TutorialVision.RunLoopDemo(pm),
    };
}
