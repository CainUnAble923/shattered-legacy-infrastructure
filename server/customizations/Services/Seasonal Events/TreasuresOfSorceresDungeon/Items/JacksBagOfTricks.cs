// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Items/JacksBagOfTricks.cs (cc-P408). Jack's Bag of
// Tricks, a Treasures of Sorcerer's Dungeon reward (75 points), and the six oddities it hands out.
//
// The bag: once a day, and only in October (staff any time), double-click gives one item: 10% a Halloween treat
// (pinned HolidaySettings.RandomTreat, the trick-or-treat list), otherwise one of the six below. "October" is
// ServUO's DateTime.Now.Month, the server's local month; here the shard's local date (Core.Now in the container's
// America/Chicago zone, as ClusterFSeasonalSwitch reads it), so the bag keeps ServUO's calendar month and not the
// shard's Halloween window (10-24 to 11-02). Not a deviation: the bag says "the Month of October" (1157632).
//
// The oddities' overhead lines: ServUO's Item.PrivateOverheadMessage sends a localized message from the item to one
// player; pinned's Item has no such method, so PrivateLine below sends the same packet (pinned
// NetState.SendMessageLocalized, which Item.SendLocalizedMessageTo also uses at hue 0x3B2; ServUO's hue is 0).

using System;
using ModernUO.Serialization;
using Server.Events.Halloween;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class JacksBagOfTricks : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextUse;

    [Constructible]
    public JacksBagOfTricks() : base(0x9F85)
    {
    }

    public override int LabelNumber => 1157656; // Jack's Bag of Tricks

    public override void OnDoubleClick(Mobile from)
    {
        if (Core.Now.ToLocalTime().Month == 10 || from.AccessLevel > AccessLevel.Player)
        {
            if (_nextUse < Core.Now)
            {
                Item item;

                if (0.1 > Utility.RandomDouble())
                {
                    item = HolidaySettings.RandomTreat;
                }
                else
                {
                    item = Utility.Random(6) switch
                    {
                        0 => new RancidReindeerMeat(),
                        1 => new GlassyCandyCane(),
                        2 => new NamedSeveredElfEars(),
                        3 => new SuspiciousGiftBox(),
                        4 => new InsultingDoll(),
                        _ => new SpikedEggnog()
                    };
                }

                if (item != null)
                {
                    if (from.Backpack?.TryDropItem(from, item, false) != true)
                    {
                        item.MoveToWorld(from.Location, from.Map);
                    }

                    NextUse = Core.Now + TimeSpan.FromDays(1);
                }
            }
            else
            {
                from.SendLocalizedMessage(1157633); // You have already used this today.
            }
        }
        else
        {
            from.SendLocalizedMessage(1157632); // This only works during the Month of October.
        }
    }

    /// <summary>ServUO Item.PrivateOverheadMessage(MessageType.Regular, 0, number, m.NetState).</summary>
    public static void PrivateLine(Item item, Mobile m, int number) =>
        m.NetState?.SendMessageLocalized(item.Serial, item.ItemID, MessageType.Regular, 0, 3, number, "", "");
}

[Flippable(0x1E90, 0x1E91)]
[SerializationGenerator(0, false)]
public partial class RancidReindeerMeat : Item
{
    [Constructible]
    public RancidReindeerMeat() : base(0x1E90) => Hue = 2707;

    public override int LabelNumber => 1157634; // Rancid Reindeer Meat

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack))
        {
            JacksBagOfTricks.PrivateLine(this, m, 1157635); // *It smells terrible!*
        }
    }
}

[Flippable(0x9DC3, 0x9DC4)]
[SerializationGenerator(0, false)]
public partial class GlassyCandyCane : Item
{
    [Constructible]
    public GlassyCandyCane() : base(0x9DC3) => Hue = 1927;

    public override int LabelNumber => 1157636; // Glass Candy Cane
}

[Flippable(0x312D, 0x312E)]
[SerializationGenerator(0, false)]
public partial class NamedSeveredElfEars : Item
{
    private static readonly string[] _names =
    {
        "Alabaster Snowball", "Pepper Minstix", "Wunorse Openslae", "Sugarplum Mary"
    };

    [SerializableField(0)]
    private string _elfName;

    [Constructible]
    public NamedSeveredElfEars() : base(0x312D) => _elfName = _names[Utility.Random(_names.Length)];

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1157637, _elfName); // The Severed Ears of an Elf Named ~1_NAME~
    }

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack))
        {
            JacksBagOfTricks.PrivateLine(this, m, 1157638); // *You hear the faint jingle of cheery bells...*
        }
    }
}

[SerializationGenerator(0, false)]
public partial class SuspiciousGiftBox : Container
{
    [Constructible]
    public SuspiciousGiftBox() : base(0x232A) => Hue = 1976;

    public override int LabelNumber => 1157639; // Suspicious Gift Box

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack))
        {
            JacksBagOfTricks.PrivateLine(this, m, 1157640); // Uh oh...
        }
    }
}

[Flippable(0x48E2, 0x48E3)]
[SerializationGenerator(0, false)]
public partial class InsultingDoll : Item
{
    [Constructible]
    public InsultingDoll() : base(0x48E2) => Hue = 1933;

    public override int LabelNumber => 1157641; // Insulting Doll

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack))
        {
            JacksBagOfTricks.PrivateLine(this, m, Utility.RandomMinMax(1157642, 1157646));
        }
    }
}

[SerializationGenerator(0, false)]
public partial class SpikedEggnog : Item
{
    [Constructible]
    public SpikedEggnog() : base(0x142A) => Hue = 2711;

    public override int LabelNumber => 1157647; // Spiked Egg Nog
}
