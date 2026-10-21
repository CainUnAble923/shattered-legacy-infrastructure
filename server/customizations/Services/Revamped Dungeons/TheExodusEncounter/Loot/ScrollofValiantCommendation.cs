// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/ScrollofValiantCommendation.cs (cc-P195): the tenth
// artifact, a scroll that raises its reader's stat cap by five, once per character; it then carries the reader's name.
// ServUO keeps the once-only flag on PlayerMobile (HasValiantStatReward); here ExodusEncounter keeps the set of
// rewarded players (saved), since pinned's PlayerMobile is not patched for one flag. The owner's name is saved.

using ModernUO.Serialization;
using Server.Engines.Exodus;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ScrollofValiantCommendation : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _owner;

    [Constructible]
    public ScrollofValiantCommendation() : base(0x46AE)
    {
        Weight = 1;
    }

    public override int LabelNumber => 1153521; // Scroll of Valiant Commendation [Replica]

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        if (string.IsNullOrEmpty(_owner))
        {
            list.Add(1152708); // Double click to permanently gain +5 to your maximum stats
        }
        else
        {
            list.Add(1152706, _owner); // Rewarded to ~1_name~
            list.Add(1152707); // Presented for Exceptional Bravery During the Siege of Exodus City.
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (ExodusEncounter.HasValiantStatReward(from))
        {
            from.SendLocalizedMessage(1049510); // Your stats are too high for this power scroll.
        }
        else if (string.IsNullOrEmpty(_owner))
        {
            from.SendLocalizedMessage(1049512); // You feel a surge of magic as the scroll enhances your powers!

            ExodusEncounter.SetValiantStatReward(from);
            from.StatCap += 5;
            Owner = from.Name;
            InvalidateProperties();

            Effects.SendLocationParticles(EffectItem.Create(from.Location, from.Map, EffectItem.DefaultDuration), 0, 0, 0, 0, 0, 5060, 0);
            Effects.PlaySound(from.Location, from.Map, 0x243);

            Effects.SendMovingParticles(new Entity(Serial.Zero, new Point3D(from.X - 6, from.Y - 6, from.Z + 15), from.Map), from, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100);
            Effects.SendMovingParticles(new Entity(Serial.Zero, new Point3D(from.X - 4, from.Y - 6, from.Z + 15), from.Map), from, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100);
            Effects.SendMovingParticles(new Entity(Serial.Zero, new Point3D(from.X - 6, from.Y - 4, from.Z + 15), from.Map), from, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100);

            Effects.SendTargetParticles(from, 0x375A, 35, 90, 0x00, 0x00, 9502, (EffectLayer)255, 0x100);
        }
    }
}
