// cc-P128: ServUO pub57 Scripts/Items/Consumables/IrresistiblyTastyTreat.cs -> ours. The 15% treat: twenty minutes on,
// two hours' cooldown, and the melee damage bonus (ServUO's TempDamageBonus 10: damage += damage / 10).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class IrresistiblyTastyTreat : TastyTreat
{
    [Constructible]
    public IrresistiblyTastyTreat(int amount = 1) : base(amount)
    {
    }

    public override int LabelNumber => 1113005; // Irresistibly Tasty Treat

    public override double Bonus => 0.15;
    public override TimeSpan Duration => TimeSpan.FromMinutes(20);
    public override TimeSpan CoolDown => TimeSpan.FromMinutes(120);
    public override int DamageBonus => 10;
}
