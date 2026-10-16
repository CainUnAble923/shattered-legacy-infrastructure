// cc-P130: ServUO Mobiles/NPCs/AnimalTrainer.cs:66-96, the Pet Training additions, on pinned's AnimalTrainer through a
// partial declaration (pinned's is `public partial class AnimalTrainer : BaseVendor`, AnimalTrainer.cs:13, with no
// OnMovement or GetProperties override of its own): the Quest Giver property line and the greeting a player hears on
// walking up (1157526, half the time, at most once per fifteen seconds per trainer). ServUO's four ML quests
// (TamingPetQuest, UsingAnimalLoreQuest, LeadingIntoBattleQuest, TeachingSomethingNewQuest, :98-166) and their
// OnDoubleClick are not ported: pinned has no ML quest system (notes/cc-P130-pet-training.md, "Deviations").

using System;

namespace Server.Mobiles;

public partial class AnimalTrainer
{
    private DateTime _nextTalk;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (PetTrainingHelper.Enabled)
        {
            list.Add(1072269); // Quest Giver
        }
    }

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);

        if (PetTrainingHelper.Enabled && m.Alive && !m.Hidden && m is PlayerMobile &&
            InLOS(m) && InRange(m, 8) && !InRange(oldLocation, 8) && Core.Now >= _nextTalk)
        {
            if (Utility.Random(100) < 50)
            {
                Say(1157526); // Such an exciting time to be an Animal Trainer! New taming techniques have been discovered!
            }

            _nextTalk = Core.Now + TimeSpan.FromSeconds(15);
        }
    }
}
