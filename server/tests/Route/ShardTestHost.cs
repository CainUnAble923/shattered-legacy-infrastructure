// ShardTestHost.cs
//
// cc-P46 Part F (bug-list D56). The test host runs only the Configure and Initialize calls that pinned's
// TestServerInitializer and our UOContentFixture-npc-speeds.patch make, so a fact that needs anything else must set it up
// itself, or it passes or fails by which fact happened to run first. Chase's console rebuild of 2026-10-03 09:40 failed
// SmithBODPathsVerification with a NullReferenceException at pinned Skills/SkillCheck.cs:72: the fixture patch installs
// SkillCheck's handlers, but nothing configured AntiMacroSystem (AntiMacroSystem.cs:82-94) unless CraftXVerification or
// MakeXRepeatVerification had run. The fixture patch now configures it too; these helpers are what a fact calls for the
// rest, and for the skill checks it reaches, so its needs are written where it runs.

using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Misc;

namespace ShatteredLegacy.Tests;

public static class ShardTestHost
{
    private static bool _accounts;

    /// <summary>new Account(...) needs a password algorithm (and Accounts' persistence, as SmithSealCatalogVerification).</summary>
    public static void EnsureAccounts()
    {
        if (!_accounts)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _accounts = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    /// <summary>
    /// A real Mobile.CheckSkill(skill, min, max): the handler pinned's SkillCheck.Initialize installs (SkillCheck.cs:35),
    /// and the AntiMacroSystem settings SkillCheck.CheckLocation reads (SkillCheck.cs:72). The fixture patch does both
    /// since cc-P46; this keeps a fact that rolls skill checks correct whatever the fixture does.
    /// </summary>
    public static void EnsureSkillChecks()
    {
        if (AntiMacroSystem.Settings == null)
        {
            AntiMacroSystem.Configure();
        }

        Mobile.SkillCheckLocationHandler ??= SkillCheck.Mobile_SkillCheckLocation;
    }

    /// <summary>
    /// Every craft system with our registrations and extended resources, as at ServerStarted
    /// (CleanUpCraftHost.EnsureCraftSystems; DefBlacksmithy.CraftSystem is null until something builds it).
    /// </summary>
    public static void EnsureCraftSystems() => CleanUpCraftHost.EnsureCraftSystems();
}
