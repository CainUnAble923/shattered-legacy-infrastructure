// cc-P128: ServUO pub57 Scripts/Items/Consumables/DeliciouslyTastyTreat.cs -> ours. The 10% treat: ten minutes on, an
// hour's cooldown.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DeliciouslyTastyTreat : TastyTreat
{
    [Constructible]
    public DeliciouslyTastyTreat(int amount = 1) : base(amount)
    {
    }

    public override int LabelNumber => 1113004; // Deliciously Tasty Treat

    public override double Bonus => 0.10;
    public override TimeSpan Duration => TimeSpan.FromMinutes(10);
    public override TimeSpan CoolDown => TimeSpan.FromMinutes(60);
}
