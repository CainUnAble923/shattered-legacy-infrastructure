// ServUO: Services/Revamped Dungeons/WrongDungeon/Quests/EscortToWrongEntrance.cs (cc-P194).
//
// "We Who Are About To Die": take the prisoner to the entrance of Wrong Dungeon so he can escape. Rewards:
// Compassion (BaseEscort's HumanInNeed award, 400 for a prisoner) and 500 gold.
//
// ServUO's BaseQuest/EscortObjective/BaseReward are its Mondain quest framework; pinned's ML quest engine has the
// same three pieces (BaseEscort, EscortObjective over a QuestArea, ItemReward), so the quest is a definition on it.
// The destination is the stock "Wrong Entrance" region of regions.json (both facets, 1939,215 to 2073,352), the
// region ServUO's EscortObjective("Wrong Entrance") names. Registered against WrongPrisoner at Configure, as the
// Athenaeum Isle quest is (Engines/ML Quests/Definitions/AthenaeumIsle.cs).

using Server.Engines.MLQuests.Objectives;
using Server.Engines.MLQuests.Rewards;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.MLQuests.Definitions;

public class EscortToWrongEntrance : BaseEscort
{
    public EscortToWrongEntrance()
    {
        Activated = true;
        Title = 1152096; // We Who Are About To Die
        Description = 1152097; // Take the prisoner to the entrance of Wrong Dungeon so they can escape. ...
        RefusalMessage = 1152099; // I am surely doomed....
        InProgressMessage = 1152100; // I couldn't see when they brought me here, how much farther to the entrance of this horrid place?
        CompletionMessage = 1152101; // I am indebted to you, friend. Please, take this as a token of my thanks. ...

        Objectives.Add(new EscortObjective(new QuestArea("Wrong Entrance", "Wrong Entrance")));

        Rewards.Add(new ItemReward(1062577, typeof(Gold), 500)); // 500 Gold
    }
}

public static class WrongEscortQuests
{
    public static void Configure()
    {
        MLQuestSystem.Register(new EscortToWrongEntrance(), typeof(WrongPrisoner));
    }
}
