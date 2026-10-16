// cc-P130: ServUO Scripts/Services/Pet Training/PetTrainingGate.cs -> ours, the three staff items, each by the port
// recipe (SerializationGenerator, Constructible, no hand-written Serialize): PetTrainingGate (a pet in training that
// walks over it with its master near is trained to its progress cap, and the lore gump refreshes or opens),
// PetBondRemoveGate (bonds a pet at once) and PowerScrollGiver (a bag of 120 scrolls for every trainable skill).

using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PetTrainingGate : Item
{
    public override bool ForceShowProperties => true;

    public override string DefaultName =>
        "Pet Training Gate - You Must Start Pet Training Before Bringing Your Pet Through The Gate!";

    [Constructible]
    public PetTrainingGate() : base(3948)
    {
        Hue = 1918;
        Movable = false;
    }

    public override bool OnMoveOver(Mobile m)
    {
        if (m is BaseCreature bc)
        {
            var profile = PetTrainingHelper.GetTrainingProfile(bc);

            if (bc.Controlled && bc.ControlMaster != null && bc.ControlMaster.InRange(bc.Location, 25) &&
                profile is { HasBegunTraining: true } && profile.TrainingProgress < profile.TrainingProgressMax)
            {
                profile.TrainingProgress = profile.TrainingProgressMax;
                bc.FixedEffect(0x375A, 10, 30);

                if (bc.ControlMaster is PlayerMobile pm)
                {
                    var gump = pm.FindGump<NewAnimalLoreGump>();

                    if (gump != null)
                    {
                        gump.Refresh();
                    }
                    else
                    {
                        pm.SendGump(new NewAnimalLoreGump(pm, bc));
                    }
                }
            }
        }

        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class PetBondRemoveGate : Item
{
    public override bool ForceShowProperties => true;

    public override string DefaultName => "Pet Bond Timer Remover";

    [Constructible]
    public PetBondRemoveGate() : base(3948)
    {
        Hue = 1911;
        Movable = false;
    }

    public override bool OnMoveOver(Mobile m)
    {
        if (m is BaseCreature { Controlled: true, IsBonded: false } bc && bc.ControlMaster != null &&
            bc.ControlMaster.InRange(bc.Location, 25))
        {
            bc.IsBonded = true;
            bc.FixedEffect(0x375A, 10, 30);
        }

        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class PowerScrollGiver : Item
{
    public override bool ForceShowProperties => true;

    public override string DefaultName => "+20 Power Scrolls";

    [Constructible]
    public PowerScrollGiver() : base(0x1183)
    {
        Hue = 2214;
        Movable = false;
    }

    public override void OnDoubleClick(Mobile m)
    {
        if (m.InRange(Location, 3))
        {
            var bag = new Bag();

            foreach (var sk in PetTrainingHelper.MagicSkills)
            {
                bag.DropItem(new PowerScroll(sk, 120));
            }

            foreach (var sk in PetTrainingHelper.CombatSkills)
            {
                bag.DropItem(new PowerScroll(sk, 120));
            }

            m.AddToBackpack(bag);
        }
    }
}
