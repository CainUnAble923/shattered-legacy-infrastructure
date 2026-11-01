// cc-P762 (Chase, 2026-11-01: "humans should be only race choosable for now"): the race lock on the real creation path,
// CharacterCreation.CharacterCreatedEvent with a real Account and a test NetState, as GargoyleStartingClothesVerification
// drives it. A player account asking for an Elf or a Gargoyle gets a Human (the human body, 400 or 401); the setting
// newCharacters.races reopens a race. Notes: shard-migration notes/cc-P762-new-character-lockdown.md.

using System;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterCreation;
using Server.Misc;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewCharacterRaceLockVerification
{
    private static bool _startupHooksRun;

    // The same three startup hooks GargoyleStartingClothesVerification needs, for the same reasons.
    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private static Mobile Make(Race race, bool female, string races, out Account account)
    {
        EnsureStartupHooks();
        account = new Account($"p762race{Guid.NewGuid():N}"[..16], "p762-test-only");
        var ns = PacketTestUtilities.CreateTestNetState();
        var kept = ServerConfiguration.GetSetting(NewCharacterRules.RacesKey, (string)null);
        ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, races);
        try
        {
            var stats = ns.NewCharacterCreation ? new byte[] { 30, 30, 30 } : new byte[] { 30, 25, 25 };
            var city = new CityInfo("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel);
            var args = new CharacterCreatedEventArgs(ns, account, "Racelock", female, 0, stats, city, [], 0, 0, 0, 0, 0, 0, 0, race);
            CharacterCreation.CharacterCreatedEvent(args);
            return args.Mobile;
        }
        finally
        {
            ServerConfiguration.SetSetting(NewCharacterRules.RacesKey, kept ?? NewCharacterRules.DefaultRaces);
        }
    }

    [Theory]
    [InlineData("Elf", false)]
    [InlineData("Elf", true)]
    [InlineData("Gargoyle", false)]
    [InlineData("Gargoyle", true)]
    public void APlayerAskingForAnotherRaceIsMadeHuman(string asked, bool female)
    {
        var m = Make(asked == "Elf" ? Race.Elf : Race.Gargoyle, female, NewCharacterRules.DefaultRaces, out var account);
        try
        {
            Assert.NotNull(m);
            Assert.Same(Race.Human, m.Race);
            Assert.Equal(female ? 0x191 : 0x190, m.Body.BodyID);
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void AHumanIsMadeHuman()
    {
        var m = Make(Race.Human, false, NewCharacterRules.DefaultRaces, out var account);
        try
        {
            Assert.Same(Race.Human, m.Race);
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void TheSwitchReopensARace()
    {
        var m = Make(Race.Elf, true, "Human, Elf", out var account);
        try
        {
            Assert.Same(Race.Elf, m.Race);
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }
}
