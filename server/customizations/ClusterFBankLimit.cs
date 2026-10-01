// ClusterFBankLimit.cs
//
// F-19 (cc-P22): the bank box holds 1,000 items, more with League rank. Ours, a deviation from OSI's
// 125-item bank (pinned Container.GlobalMaxItems, Server/Items/Container.cs:159).
//
// How it reaches the bank: pinned's Container.MaxItems is per container and saved
// (Server/Items/Container.cs:85-103), and BankBox (Server/Items/Containers.cs:7) is a core type, so a
// partial from UOContent cannot override its DefaultMaxItems. Rather than patch the core, the limit is
// written to the bank's MaxItems at world load (every player whose bank exists) and at login (creating
// the bank if the character has none yet). GlobalMaxItems is never changed, so every other container,
// backpacks and house containers included, keeps 125.
//
// GetMaxItems is the one function that decides the number. When a character's League rank changes,
// call Apply on them so the bank follows without waiting for the next login.

using ModernUO.CodeGeneratedEvents;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class ClusterFBankLimit
{
    /// <summary>Items a bank box holds before any League bonus (Chase, 2026-09-30).</summary>
    public const int BaseItems = 1000;

    /// <summary>Extra items per League rank (Chase, 2026-09-30). Not reachable until ranks exist.</summary>
    public const int ItemsPerLeagueRank = 100;

    public static void Configure()
    {
        EventSink.WorldLoad += ApplyToLoadedPlayers;
    }

    /// <summary>The bank limit for this owner: base plus the League bonus.</summary>
    public static int GetMaxItems(Mobile owner) => BaseItems + LeagueRankBonus(owner);

    /// <summary>
    /// Items added for the owner's League rank. Always 0 today: League ranks do not exist yet
    /// (ClusterFLeagueSystem has only citizen status, and F-12's metal ranks are undecided). When F-12
    /// defines them, return ItemsPerLeagueRank times the owner's rank here (F-19).
    /// </summary>
    public static int LeagueRankBonus(Mobile owner) => 0;

    /// <summary>Writes the limit to the owner's bank box. Creates the bank only when asked to.</summary>
    public static void Apply(Mobile m, bool createBank)
    {
        if (m == null || m.Deleted)
        {
            return;
        }

        var bank = createBank ? m.BankBox : m.FindItemOnLayer<BankBox>(Layer.Bank);

        if (bank == null)
        {
            return;
        }

        var limit = GetMaxItems(m);

        if (bank.MaxItems != limit)
        {
            bank.MaxItems = limit;
        }
    }

    public static void ApplyToLoadedPlayers()
    {
        foreach (var m in World.Mobiles.Values)
        {
            if (m is PlayerMobile)
            {
                Apply(m, false);
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => Apply(pm, true);
}
