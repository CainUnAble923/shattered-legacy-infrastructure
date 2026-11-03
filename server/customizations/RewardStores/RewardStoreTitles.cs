// cc-P760: the titles the reward stores sell. They are League titles (LeagueTitles.All lists these after its own): owned by
// the account, one shown per character, chosen in the Titles window. Hidden until held, so the picker never lists a title
// a player has not bought.

#nullable enable

using System.Collections.Generic;
using Server.Engines.LeagueRewards;

namespace Server.Engines.RewardStores;

public static class RewardStoreTitles
{
    // One per guild, at its top rank (the plan: "its guild title, top rank"). Keyed by the guild key.
    public static readonly Dictionary<string, string> GuildTitleNames = new()
    {
        ["smithing"] = "the Master of the Forge",
        ["arcane"] = "the Sigilwright",
        ["tailoring"] = "the Master Weaver",
        ["tinkers"] = "the Gearmaster",
        ["healers"] = "the Lifebinder",
        ["bards"] = "the Songkeeper",
        ["thieves"] = "the Shadowhand",
        ["warriors"] = "the Blademaster",
        ["mining"] = "the Deep Delver",
        ["rangers"] = "the Pathwarden",
        ["maritime"] = "the Sea Captain",
        ["merchants"] = "the Merchant Prince",
        ["foresters"] = "the Woodwarden",
        ["artificers"] = "the Master Artificer",
        ["custodians"] = "the Keeper of the Streets",
        ["dojo"] = "the Twin Path Master",
        ["keepers"] = "the Keeper of the Last Door",
        ["alembic"] = "the Master Distiller"
    };

    public static string GuildTitleKey(string guildKey) => $"guild.{guildKey}";

    public const string Halloween = "festival.halloween";
    public const string Winter = "festival.winter";
    public const string Spring = "festival.spring";

    public const string Completionist = "ap.completionist";
    public const string Curious = "ap.curious";
    public const string Collector = "ap.collector";
    public const string Explorer = "ap.explorer";

    public const string LeagueChampion = "league-champion";

    public static IEnumerable<LeagueTitles.TitleDef> All()
    {
        foreach (var (guild, display) in GuildTitleNames)
        {
            yield return new LeagueTitles.TitleDef(GuildTitleKey(guild), display, -1, HiddenUntilHeld: true);
        }

        yield return new LeagueTitles.TitleDef(Halloween, "the Hallowed", -1, HiddenUntilHeld: true);
        yield return new LeagueTitles.TitleDef(Winter, "the Winterborn", -1, HiddenUntilHeld: true);
        yield return new LeagueTitles.TitleDef(Spring, "the Springtide", -1, HiddenUntilHeld: true);

        yield return new LeagueTitles.TitleDef(Completionist, "the Completionist", -1, HiddenUntilHeld: true);
        yield return new LeagueTitles.TitleDef(Curious, "the Curious", -1, HiddenUntilHeld: true);
        yield return new LeagueTitles.TitleDef(Collector, "the Collector", -1, HiddenUntilHeld: true);
        yield return new LeagueTitles.TitleDef(Explorer, "the Explorer", -1, HiddenUntilHeld: true);

        yield return new LeagueTitles.TitleDef(LeagueChampion, "the League Champion", -1, HiddenUntilHeld: true);
    }
}
