// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/WrongPrisoner.cs (cc-P194).
//
// The prisoner in the Wrong cells, an escortable in plate who offers one quest: EscortToWrongEntrance (take him
// to the dungeon entrance; the brigands will try to kill him). Invulnerable until someone takes him.
//
// Conversion notes: ServUO's BaseEscort is its Mondain quester escortable; pinned's is BaseEscortable on the ML
// quest engine, which picks a town escort at random unless the quest list is fixed, so ConstructQuestList is
// overridden to the one quest. IsPrisoner is BaseCreature's flag on both (pinned EscortObjective awards the
// prisoner's 400 compassion by it). InitOutfit is empty as on ServUO: the plate is the outfit.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class WrongPrisoner : BaseEscortable
{
    [Constructible]
    public WrongPrisoner()
    {
        Title = "the prisoner";
        IsPrisoner = true;
        Female = false;
        Body = 0x190;
        Hue = 33802;
        Name = NameList.RandomName("male");

        this.SetWearable(new PlateChest());
        this.SetWearable(new PlateArms());
        this.SetWearable(new PlateGloves());
        this.SetWearable(new PlateLegs());
    }

    public override void InitOutfit()
    {
    }

    public override bool IsInvulnerable => !Controlled;

    // Pinned BaseCreature.IsPrisoner is a plain property (BaseCreature.cs:914), not a saved field: set it again
    // after a load, or a reloaded prisoner would pay 200 compassion instead of 400.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        IsPrisoner = true;
    }

    protected override List<MLQuest> ConstructQuestList()
    {
        var quest = MLQuestSystem.FindQuest(typeof(EscortToWrongEntrance));

        return quest == null ? null : [quest];
    }
}
