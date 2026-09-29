// A gargoyle created without a profession starts in a cloth chest and a cloth kilt, and no cloth legs.
// Bug-list D26; see shard-migration/notes/d26-gargoyle-kilt.md.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh.
//
// This drives the real creation path, CharacterCreation.CharacterCreatedEvent with a real Account and a test NetState,
// not AddPants through a seam. AddPants was doing exactly what it was written to do; the defect was what it was
// written to do.
//
// The one thing the test host lacks is tiledata.mul (D27), so every BaseArmor is on Layer.Invalid and the creation
// path's EquipItem packs the whole outfit. For the duration of the fact only, TestTileRows seeds the six gargish cloth
// rows and raises MaxItemValue, then restores both (Route/TestTileRows.cs has the rows and where they came from).
// Nothing about the creation path itself is substituted.
//
// The D26 assertions (no legs anywhere, a kilt present) come first and do not depend on that setup; the worn-and-layer
// assertions after them do.

using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterCreation;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GargoyleStartingClothesVerification
{
    private readonly ITestOutputHelper _out;

    public GargoyleStartingClothesVerification(ITestOutputHelper output) => _out = output;

    private static bool _startupHooksRun;

    // Three startup hooks the test fixture never invokes, each of which the creation path needs:
    // - Accounts.Configure: without it new Account(...) throws on Accounts.NewAccount.
    // - AccountSecurity.Configure: without it CurrentAlgorithm is None and SetPassword throws. It is set directly rather
    //   than read from config because this test never checks a password; PBKDF2 because it is fully managed.
    // - WelcomeTimer.Initialize: the last thing OnCharacterCreated does is new WelcomeTimer(newChar), whose constructor
    //   reads the message table this fills. Without it the event throws after the outfit is on.
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AGargoyleWithNoProfessionStartsInAChestAndAKiltAndNoLegs(bool female)
    {
        EnsureStartupHooks();

        var account = new Account($"d26probe{(female ? "f" : "m")}{Guid.NewGuid():N}"[..16], "d26-test-only");
        using var ns = PacketTestUtilities.CreateTestNetState();
        Mobile m = null;

        // The rows the worn-and-layer assertions rest on: all six the creation path could build, for either sex.
        using var tiles = TestTileRows.Seed(
            TileRows.GargishClothChestType1, TileRows.GargishClothChestType2,
            TileRows.GargishClothKiltType1, TileRows.GargishClothKiltType2,
            TileRows.GargishClothLegsType1, TileRows.GargishClothLegsType2
        );

        try
        {
            // SetStats falls back to 10/10/10 on an invalid spread, and 10 Str is under the cloth pieces' AosStrReq 20,
            // which would stop the equip for a reason unrelated to D26. The valid total depends on the client version.
            var stats = ns.NewCharacterCreation ? new byte[] { 30, 30, 30 } : new byte[] { 30, 25, 25 };
            var city = new CityInfo("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel);

            var args = new CharacterCreatedEventArgs(
                ns, account, "Dtwentysix", female, 0, stats, city, [],
                0, 0, 0, 0, 0, 0,
                0, // no profession
                Race.Gargoyle
            );

            CharacterCreation.CharacterCreatedEvent(args);

            m = args.Mobile;
            Assert.NotNull(m);
            Assert.Equal(Race.Gargoyle, m.Race);
            Assert.Equal(female, m.Female);

            var owned = new List<Item>(m.Items);
            if (m.Backpack != null)
            {
                owned.AddRange(m.Backpack.Items);
            }

            foreach (var item in owned)
            {
                _out.WriteLine(
                    $"{(item.Parent == m ? "worn" : "pack")}  {item.GetType().Name,-26} layer {item.Layer}  0x{item.ItemID:X4}"
                );
            }

            // D26: these two hold or fail whatever layer the pieces landed on.
            var legs = owned.FindAll(i => i is GargishClothLegsType1 or GargishClothLegsType2);
            Assert.True(
                legs.Count == 0,
                $"expected no GargishClothLegsType*, found {string.Join(", ", legs.ConvertAll(i => i.GetType().Name))}"
            );

            var kilt = owned.Find(i => i is GargishClothKiltType1 or GargishClothKiltType2);
            Assert.NotNull(kilt);
            Assert.IsType(female ? typeof(GargishClothKiltType2) : typeof(GargishClothKiltType1), kilt);

            // Worn, not packed, each on its own layer. These rest on the seeded tile rows.
            var chest = m.FindItemOnLayer(Layer.InnerTorso);
            Assert.NotNull(chest);
            Assert.IsType(female ? typeof(GargishClothChestType2) : typeof(GargishClothChestType1), chest);

            Assert.Same(m, kilt.Parent);
            Assert.Same(kilt, m.FindItemOnLayer(kilt.Layer));

            Assert.NotEqual(Layer.Invalid, kilt.Layer);
            Assert.NotEqual(chest.Layer, kilt.Layer);
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }
}
