// Pins the test host's tile data, which is none, and what that does to every item a fact builds.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// THE TRAP THIS EXISTS FOR (D27, found by D26):
//
//   TileData's static constructor returns early under xUnit (Projects/Server/TileData.cs:293-301),
//   and only Load sets MaxItemValue (:382). So in this host MaxItemValue is 0, and
//   Item.ItemData, which is ItemTable[itemID & MaxItemValue] (Projects/Server/Items/Item.cs:732),
//   reads row 0 for EVERY item whatever its item ID. Seeding ItemTable rows alone changes nothing,
//   because the mask still sends every lookup to row 0. That is the part that is easy to miss.
//
//   Two constructors take their layer from that row: BaseArmor (UOContent/Items/Armor/
//   BaseArmor.cs:166) and BaseWeapon (UOContent/Items/Weapons/BaseWeapon.cs:199). Every piece of
//   armour, every shield, and every weapon that does not set its own layer (one-handers mostly;
//   Bow and most two-handers set theirs) is therefore on Layer.Invalid here. Item.CanEquip refuses
//   Invalid (Item.cs:2150), so Mobile.EquipItem returns false and a caller such as
//   CharacterCreation's EquipItem drops the piece into the backpack without complaint.
//   Mobile.AddItem does not look at the layer at all (Mobile.cs:6616), so an item added that way
//   is "worn" on Invalid, and FindItemOnLayer finds it on no real layer.
//
//   Item.DefaultWeight returns 0 for any item ID above MaxItemValue (Item.cs:448-466), so every
//   item that does not set or override its weight weighs 0 here, and no container weight limit
//   ever refuses anything.
//
// WHAT THAT MEANS FOR A FACT:
//
//   - Asserting the layer of a BaseArmor or BaseWeapon that does not set its own asserts
//     Layer.Invalid. Asserting where an EquipItem path put one asserts "the backpack".
//   - Setting item.Layer by hand before AddItem (the armour-set facts' Wear helpers) is not a
//     workaround for anything those facts assert: SetHelper walks Mobile.Items and never reads a
//     layer. What it does do is hide the item's REAL layer. That is how BestialArms sat on
//     InnerTorso unnoticed while the Bestial fact wore it on Arms by hand (D28, fixed).
//   - A fact that needs real layers or weights opens a TestTileRows.Seed scope naming the rows it
//     needs (TestTileRows.cs): it seeds them AND raises MaxItemValue for the fact's duration, and
//     restores both. Do not read tiledata.mul at test time: client-data/ is not in the build context.
//
// If a change to the fixture ever loads real tile data (or sets MaxItemValue globally), this fact
// goes red, on purpose. That change alters what every other fact is measuring, and it should be
// made as its own decision and not discovered afterwards. When it goes red: re-run the suite, look
// at every fact that equips, weighs or reads a layer, and retire TestTileRows and its callers.
// notes/d27-worn-item-facts.md has the audit to start from.

using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TestHostTileDataVerification
{
    private readonly ITestOutputHelper _out;

    public TestHostTileDataVerification(ITestOutputHelper output) => _out = output;

    [Fact]
    public void TheHostHasNoItemTileDataSoArmourAndWeaponsAreOnNoLayer()
    {
        var maxItemValue = TileData.MaxItemValue;

        var chest = new LeatherChest(); // BaseArmor, 0x13CC: InnerTorso in the shard's tiledata.mul
        var dagger = new Dagger();      // BaseWeapon, 0xF52: OneHanded there; Dagger sets no layer of its own
        var plain = new Item(0x13CC);   // Item, no weight of its own: 6 stones there
        var pm = new PlayerMobile();

        try
        {
            var equipped = pm.EquipItem(chest);

            _out.WriteLine(
                $"MaxItemValue={maxItemValue}; LeatherChest layer={chest.Layer}, EquipItem={equipped}, " +
                $"parent={(chest.Parent == pm ? "the mobile" : chest.Parent?.ToString() ?? "none")}; " +
                $"Dagger layer={dagger.Layer}; plain item weight={plain.Weight}"
            );

            Assert.Equal(0, maxItemValue);
            Assert.Equal(Layer.Invalid, chest.Layer);
            Assert.Equal(Layer.Invalid, dagger.Layer);

            // The consequence that makes a worn-item assertion vacuous: the equip is refused, silently.
            Assert.False(equipped);
            Assert.Null(chest.Parent);

            Assert.Equal(0.0, plain.Weight);
        }
        finally
        {
            chest.Delete();
            dagger.Delete();
            plain.Delete();
            pm.Delete();
        }
    }
}
