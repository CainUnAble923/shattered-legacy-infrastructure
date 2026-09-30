// cc-P15 (F-9 Decision 2.4): each New Haven skill trainer adds one line naming the guild whose hall it
// stands in. The trainers are pinned's (NewHavenSkillTraining.cs), all declared partial and none
// overriding OnDoubleClick, so this adds a part to each: the stock double-click runs first, unchanged
// (BaseCreature.OnDoubleClick -> MLQuestSystem.OnDoubleClick, BaseCreature.cs:2864-2867), then the line.
// The line itself, and which hall each trainer stands in, are ClusterFGuildStarter's
// (ClusterFGuildStarterPath.cs). No pinned file is patched.
//
// cc-P17 PT-03: the stock double-click runs inside NewHavenQuestBoard.TrainerDoubleClick, so a quest
// taken at the board is turned in to its trainer as well (the board and trainers are interchangeable).

using Server.Engines.MLQuests.Items;

namespace Server.Engines.MLQuests.Definitions;

public partial class Aelorn
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Dimethro
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Churchill
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Robyn
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Recaro
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class AldenArmstrong
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Jockles
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class TylAriadne
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Alefian
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Gustar
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Jillian
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Kaelynna
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Mithneral
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class AmeliaYoungstone
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class AndreasVesalius
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Avicenna
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class SarsmeaSmythe
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Ryuichi
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Chiyo
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Jun
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Walker
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Hamato
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Mulcivikh
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class Morganna
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class JacobWaltz
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}

public partial class GeorgeHephaestus
{
    public override void OnDoubleClick(Mobile from)
    {
        NewHavenQuestBoard.TrainerDoubleClick(this, from, () => base.OnDoubleClick(from));
        ClusterFGuildStarter.TellTrainerLine(this, from);
    }
}
