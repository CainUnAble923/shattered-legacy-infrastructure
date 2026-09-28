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
// The one thing the test host lacks is tiledata.mul. TileData's static constructor returns early under xUnit, leaving
// MaxItemValue at 0, so Item.ItemData reads ItemTable[itemID & 0], row 0, for every item. Every BaseArmor therefore gets
// Layer.Invalid from ItemData.Quality, Mobile.EquipItem refuses it, and the creation path drops the whole outfit into
// the backpack. So for the duration of the fact only, the six gargish cloth rows are put into TileData.ItemTable and
// MaxItemValue is set to 0xFFFF (its value after a real Load of this file), then both are restored. The row values
// were read from the shard's own client-data/classic-client/tiledata.mul (3,188,736 bytes) by the parser recorded in
// the note: chest 0x405/0x406 quality 13 (InnerTorso), kilt 0x407/0x408 quality 7 (Gloves), legs 0x409/0x40A quality
// 4 (Pants). Nothing about the creation path itself is substituted.
//
// The D26 assertions (no legs anywhere, a kilt present) come first and do not depend on that setup; the worn-and-layer
// assertions after them do.

using System;
using System.Collections.Generic;
using System.Reflection;
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

    // (itemID, name, weight, quality = layer, animation), from the shard's tiledata.mul. Flags 0x404002 on all six.
    private static readonly (int ItemID, string Name, int Weight, int Quality, int Animation)[] GargishClothTiles =
    [
        (0x405, "gargoyle_clothing_ch", 6, 13, 592),
        (0x406, "gargoyle_clothing_ch", 6, 13, 593),
        (0x407, "gargoyle_clothing_ki", 2, 7, 594),
        (0x408, "gargoyle_clothing_ki", 2, 7, 595),
        (0x409, "gargoyle_clothing_le", 4, 4, 596),
        (0x40A, "gargoyle_clothing_le", 4, 4, 597)
    ];

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

    private static readonly MethodInfo SetMaxItemValue =
        typeof(TileData).GetProperty(nameof(TileData.MaxItemValue))!.GetSetMethod(true)!;

    private static int _savedMaxItemValue;

    private static ItemData[] SeedGargishClothTiles()
    {
        _savedMaxItemValue = TileData.MaxItemValue;
        SetMaxItemValue.Invoke(null, [0xFFFF]);

        var saved = new ItemData[GargishClothTiles.Length];

        for (var i = 0; i < GargishClothTiles.Length; i++)
        {
            var (id, name, weight, quality, animation) = GargishClothTiles[i];
            saved[i] = TileData.ItemTable[id];
            TileData.ItemTable[id] = new ItemData(name, (TileFlag)0x404002UL, weight, quality, animation, 0, 0, 1);
        }

        return saved;
    }

    private static void RestoreTiles(ItemData[] saved)
    {
        for (var i = 0; i < GargishClothTiles.Length; i++)
        {
            TileData.ItemTable[GargishClothTiles[i].ItemID] = saved[i];
        }

        SetMaxItemValue.Invoke(null, [_savedMaxItemValue]);
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

        var savedTiles = SeedGargishClothTiles();

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
            RestoreTiles(savedTiles);
        }
    }
}
