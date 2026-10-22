// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Items/JackInTheBox.cs (cc-P408). The Jack in the Box
// toy, a Treasures of Sorcerer's Dungeon reward (20 points): double-click winds it (sound 1661), the jack pops out
// nine seconds later (0x9F65) and goes back in five seconds after that; once per 20 seconds.
//
// The last use is a plain field in ServUO, not saved, and stays unsaved here. DateTime.UtcNow is Core.Now.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class JackInTheBox : Item
{
    private DateTime _lastUse;

    [Constructible]
    public JackInTheBox() : base(0x9F64)
    {
    }

    public override int LabelNumber => 1157655; // Jack in the Box

    public override void OnDoubleClick(Mobile from)
    {
        if (_lastUse + TimeSpan.FromSeconds(20) < Core.Now)
        {
            ItemID = 0x9F64;

            Effects.PlaySound(GetWorldLocation(), Map, 1661);

            Timer.StartTimer(TimeSpan.FromSeconds(9), () =>
            {
                ItemID = 0x9F65;

                Timer.StartTimer(TimeSpan.FromSeconds(5), () => ItemID = 0x9F64);
            });

            _lastUse = Core.Now;
        }
        else
        {
            from.SendLocalizedMessage(501789); // You must wait before trying again.
        }
    }
}
