// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Vela.cs (cc-P194).
//
// Vela the sorceress, the Void Pool reward vendor in Cove (placed by [SetupNewCovetous at 2254,1207,0 on both
// facets through a spawner). Double-click within five tiles: the reward gump for your Void Pool points. She keeps
// one of each wearable reward in her pack for the gump to show.
//
// Conversion notes: ServUO's range perception 2 is carried (it is not 10); SetWearable is our extension; the
// version-0 re-dress of ServUO's Deserialize never runs for new content. GargishHephaestus and
// GargishBlightOfTheTundra need pinned bases that do not exist (LargePlateShield; the glaive does, see the
// items) and are not in her pack: notes.

using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.VoidPool;

[SerializationGenerator(0, false)]
public partial class VelaTheSorceress : BaseCreature
{
    [Constructible]
    public VelaTheSorceress() : base(AIType.AI_Vendor, FightMode.None, 2)
    {
        Title = "the sorceress";
        Blessed = true;

        SetStr(110);
        SetDex(100);
        SetInt(1000);

        Hue = Race.Human.RandomSkinHue();
        Body = 0x191;
        HairItemID = 0x203C;
        HairHue = 0x46D;

        this.SetWearable(new FancyShirt(), 1928);
        this.SetWearable(new LeatherLegs(), 1928);
        this.SetWearable(new ThighBoots(), 1917);

        Item item = new BraceletOfProtection();
        item.Movable = false;
        PackItem(item);

        item = new Hephaestus();
        item.Movable = false;
        PackItem(item);

        item = new BlightOfTheTundra();
        item.Movable = false;
        PackItem(item);

        item = new GargishBlightOfTheTundra();
        item.Movable = false;
        PackItem(item);
    }

    public override string DefaultName => "Vela";

    public override bool IsInvulnerable => true;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm && from.InRange(Location, 5))
        {
            from.SendGump(new VoidPoolRewardGump(this, pm));
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1152664); // Covetous Void Pool Reward Vendor
    }
}
