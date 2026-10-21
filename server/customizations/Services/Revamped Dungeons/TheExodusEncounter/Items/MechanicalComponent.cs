// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/MechanicalComponent.cs (cc-P195): dropped by one Exodus
// minion in five (ExodusEncounter.OnCreatureDeath); an 80 Tinkering tinker turns it into an Exotic Toolkit. Three days.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MechanicalComponent : BaseDecayingItem
{
    [Constructible]
    public MechanicalComponent() : base(0x2DD7)
    {
        Hue = 2500;
        Weight = 1;
    }

    public override int Lifespan => 259200;
    public override bool UseSeconds => false;

    public override int LabelNumber => 1153865; // Mechanical Component

    public override void OnDoubleClick(Mobile from)
    {
        if (from.Skills[SkillName.Tinkering].Value >= 80.0)
        {
            from.AddToBackpack(new ExoticToolkit());
            Delete();
            from.SendLocalizedMessage(1152369); // You successfully convert the component into an exotic tool kit.
        }
        else
        {
            from.SendLocalizedMessage(1152370); // Only an Adept Tinker would know what to do with this.
        }
    }
}
