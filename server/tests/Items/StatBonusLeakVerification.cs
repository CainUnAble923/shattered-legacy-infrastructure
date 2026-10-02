// cc-P29 Part A, D44: item stat bonuses carried by AosAttributes must come off on unequip.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// Pinned AosAttributes.AddStatBonuses named its StatMods after the attributes object's default
// hash code and RemoveStatBonuses removed them by the owning item's serial, so the names never
// matched and every equip stacked another copy (pinned UOContent/Misc/AOS.cs:598-602 vs :620-622).
// Talismans (BaseTalisman.cs:356, :379), quivers (BaseQuiver.cs:206, :214) and our PetMimic
// (PetMimic.cs OnAdded/OnRemoved) all take that path. server/patches/AOS-stat-bonus-names.patch
// makes the add path use the serial. See shard-migration/notes/cc-P29-defect-batch-2.md, Part A.

using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StatBonusLeakVerification
{
    private const int Cycles = 3;

    private readonly ITestOutputHelper _out;

    public StatBonusLeakVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer()
    {
        var m = new PlayerMobile();
        m.RawStr = 50;
        m.RawDex = 50;
        m.RawInt = 50;
        return m;
    }

    private void Cycle(PlayerMobile m, Item item, Layer layer, StatType stat, int bonus)
    {
        for (var i = 1; i <= Cycles; i++)
        {
            item.Layer = layer;
            m.AddItem(item);
            _out.WriteLine($"{item.GetType().Name} cycle {i} worn:    {stat} offset {m.GetStatOffset(stat)}");
            Assert.Equal(bonus, m.GetStatOffset(stat));

            m.RemoveItem(item);
            _out.WriteLine($"{item.GetType().Name} cycle {i} removed: {stat} offset {m.GetStatOffset(stat)}");
        }
    }

    [Fact]
    public void ATalismansStrDexIntBonusesComeOffEveryTime()
    {
        var m = NewPlayer();
        var talisman = new BaseTalisman(0x2F58);
        talisman.Attributes.BonusStr = 10;
        talisman.Attributes.BonusDex = 7;
        talisman.Attributes.BonusInt = 5;

        Cycle(m, talisman, Layer.Talisman, StatType.Str, 10);

        Assert.Equal(50, m.RawStr);
        Assert.Equal(50, m.RawDex);
        Assert.Equal(50, m.RawInt);
        Assert.Equal(0, m.GetStatOffset(StatType.Str));
        Assert.Equal(0, m.GetStatOffset(StatType.Dex));
        Assert.Equal(0, m.GetStatOffset(StatType.Int));
        Assert.Equal(50, m.Str);

        talisman.Delete();
    }

    [Fact]
    public void AQuiversStrBonusComesOffEveryTime()
    {
        var m = NewPlayer();
        var quiver = new ElvenQuiver();
        quiver.Attributes.BonusStr = 10;

        Cycle(m, quiver, Layer.Cloak, StatType.Str, 10);

        Assert.Equal(50, m.RawStr);
        Assert.Equal(0, m.GetStatOffset(StatType.Str));

        quiver.Delete();
    }

    [Fact]
    public void APetMimicsStrBonusComesOffEveryTime()
    {
        var m = NewPlayer();
        var mimic = new PetMimic();
        mimic.AccStrBonus = 10;

        Cycle(m, mimic, Layer.Talisman, StatType.Str, 10);

        Assert.Equal(50, m.RawStr);
        Assert.Equal(0, m.GetStatOffset(StatType.Str));

        mimic.Delete();
    }
}
