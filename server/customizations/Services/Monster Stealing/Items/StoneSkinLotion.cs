// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/StoneSkinLotion.cs -> ours. The three mods are recorded
// on the effect so RemoveEffect takes exactly them off (BaseThieveConsumable's note).

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.StoneSkinLotion")]
[SerializationGenerator(0, false)]
public partial class StoneSkinLotion : BaseBalmOrLotion
{
    [Constructible]
    public StoneSkinLotion() : base(0xEFD)
    {
        EffectType = ThieveConsumableEffect.StoneSkinLotionEffect;
    }

    public override int LabelNumber => 1094944; // Stone Skin Lotion

    protected override void ApplyEffect(PlayerMobile pm)
    {
        var mods = new List<ResistanceMod>
        {
            new(ResistanceType.Cold, "StoneSkinCold", -5),
            new(ResistanceType.Fire, "StoneSkinFire", -5),
            new(ResistanceType.Physical, "StoneSkinPhysical", 30)
        };

        foreach (var mod in mods)
        {
            pm.AddResistanceMod(mod);
        }

        base.ApplyEffect(pm);
        RecordResistanceMods(pm, mods);
    }
}
