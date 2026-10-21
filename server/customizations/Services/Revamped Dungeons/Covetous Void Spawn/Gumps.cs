// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Gumps.cs (cc-P194).
//
// VoidPoolGump: "The Void Pool" (the context-menu entry inside the region): the facet, the current wave or the
// minutes to the next battle, the three scoreboards, the best wave, the two help texts. ScoresGump: a ranked
// list, twenty a page. VoidPoolRewardGump: Vela's reward list over BaseRewardGump (Gumps/BaseRewardGump.cs).
//
// Conversion notes: pinned's OnResponse takes an "in RelayInfo"; AddHtmlLocalized with a color exists in both.
// A cauldron deed is built with its CraftResource when its type has such a constructor (ServUO decides by index
// 0-23, the resource-hued rewards; the trimmed list here has fewer of them, so the constructor decides).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.VoidPool;

public enum ScoreType
{
    Current,
    BestSingle,
    OverallTotal,
    BestWave
}

public class VoidPoolGump : Gump
{
    public const int Red = 0x4800;
    public const int Orange = 0xB104;

    public VoidPoolGump(VoidPoolController controller, PlayerMobile pm) : base(50, 50)
    {
        Controller = controller;
        User = pm;

        AddGumpLayout();
    }

    public PlayerMobile User { get; }
    public VoidPoolController Controller { get; }

    public void AddGumpLayout()
    {
        AddBackground(0, 0, 400, 565, 9350);

        AddHtmlLocalized(10, 10, 200, 16, 1152531, Red); // The Void Pool
        AddHtmlLocalized(10, 30, 200, 16, Controller.Map == Map.Felucca ? 1012001 : 1012000, Red); // Felucca / Trammel

        if (Controller.OnGoing)
        {
            AddHtmlLocalized(10, 50, 200, 16, 1152914, Orange); // Current Battle:
            AddHtmlLocalized(180, 50, 200, 16, 1152915, Controller.Wave.ToString(), Orange); // Wave ~1_WAVE~
        }
        else
        {
            AddHtmlLocalized(10, 50, 200, 16, 1152916, Orange); // Next Battle:

            if (Controller.NextStart > Core.Now)
            {
                var minutes = (int)(Controller.NextStart - Core.Now).TotalMinutes;
                AddHtmlLocalized(180, 50, 200, 16, 1152917, minutes.ToString(), Orange); // Starts in ~1_MIN~ min.
            }
        }

        AddButton(140, 70, 4005, 4006, 1);
        AddHtmlLocalized(180, 70, 200, 16, 1152535, Orange); // Current Battle Scoreboard

        AddButton(140, 90, 4005, 4006, 2);
        AddHtmlLocalized(180, 90, 200, 16, 1152536, Orange); // Best Single Battle Scoreboard

        AddButton(140, 110, 4005, 4006, 3);
        AddHtmlLocalized(180, 110, 200, 16, 1152537, Orange); // Overall Total Scores

        var stats = VoidPoolStats.GetStats(Controller);

        if (stats.BestWave != null)
        {
            AddButton(140, 130, 4005, 4006, 4);
            AddHtml(180, 130, 200, 16, $"<basefont color=#A52A2A>Best Wave: {stats.BestWave.Waves}");
        }

        AddHtmlLocalized(10, 150, 400, 16, 1152552, Orange); // See Loyalty Menu for Reward Points
        AddHtmlLocalized(10, 170, 400, 16, 1152553, Orange); // See Vela in Cove for rewards

        AddHtmlLocalized(10, 190, 380, 175, 1152533, Orange, true, true);
        AddHtmlLocalized(10, 375, 380, 175, 1152534, Orange, true, true);
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        var stats = VoidPoolStats.GetStats(Controller);

        switch (info.ButtonID)
        {
            case 1:
                User.SendGump(new ScoresGump(Controller, User, ScoreType.Current));
                break;
            case 2:
                User.SendGump(new ScoresGump(Controller, User, ScoreType.BestSingle));
                break;
            case 3:
                User.SendGump(new ScoresGump(Controller, User, ScoreType.OverallTotal));
                break;
            case 4:
                if (stats.BestWave != null)
                {
                    User.SendGump(new ScoresGump(Controller, User, ScoreType.BestWave));
                }

                break;
        }
    }
}

public class ScoresGump : Gump
{
    public const int Red = 0x4800;
    public const int Orange = 0xB104;

    public ScoresGump(VoidPoolController controller, PlayerMobile pm, ScoreType type) : base(50, 50)
    {
        Controller = controller;
        User = pm;
        ScoreType = type;

        AddGumpLayout();
    }

    public PlayerMobile User { get; }
    public VoidPoolController Controller { get; }
    public Dictionary<Mobile, long> Score { get; private set; }
    public ScoreType ScoreType { get; }

    public void AddGumpLayout()
    {
        var page = 0;
        AddBackground(0, 0, 500, 620, 9350);
        AddPage(page);

        if (Controller == null)
        {
            return;
        }

        var stats = VoidPoolStats.GetStats(Controller);

        if (ScoreType == ScoreType.BestWave && stats.BestWave == null)
        {
            return;
        }

        int loc;
        string locText = null;

        switch (ScoreType)
        {
            default:
                Score = Controller.CurrentScore;
                loc = 1152535;
                break;
            case ScoreType.BestSingle:
                Score = stats.BestSingle;
                loc = 1152536;
                break;
            case ScoreType.OverallTotal:
                Score = stats.OverallTotal;
                loc = 1152537;
                break;
            case ScoreType.BestWave:
                Score = stats.BestWave.Score;
                loc = 0;
                locText = "Best Wave Scoreboard";
                break;
        }

        AddHtmlLocalized(10, 10, 200, 16, 1152531, Red); // The Void Pool
        AddHtmlLocalized(10, 30, 200, 16, Controller.Map == Map.Felucca ? 1012001 : 1012000, Red); // Felucca / Trammel

        if (loc > 0)
        {
            AddHtmlLocalized(10, 50, 200, 16, loc, Red);
        }
        else if (locText != null)
        {
            AddHtml(10, 50, 200, 16, $"<basefont color=#8B0000>{locText}");
        }

        if (ScoreType == ScoreType.BestWave)
        {
            AddHtml(200, 30, 200, 16, $"<basefont color=#8B0000>Total Waves: {stats.BestWave.Waves}");
            AddHtml(200, 50, 200, 16, $"<basefont color=#8B0000>Total Score: {stats.BestWave.TotalScore}");
        }

        AddHtmlLocalized(10, 90, 100, 16, 1152541, Orange); // RANK
        AddHtmlLocalized(100, 90, 300, 16, 1152542, Orange); // PLAYER
        AddHtmlLocalized(400, 90, 100, 16, 1152543, Orange); // SCORE

        page++;
        AddPage(page);

        if (Score == null)
        {
            return;
        }

        var index = 0;
        var yOffset = 0;

        foreach (var table in Score.OrderBy(kvp => -kvp.Value))
        {
            AddHtml(10, 130 + 20 * yOffset, 50, 16, (index + 1).ToString());
            AddHtml(100, 130 + 20 * yOffset, 300, 16, table.Key.Name);
            AddHtml(400, 130 + 20 * yOffset, 100, 16, table.Value.ToString());

            index++;
            yOffset++;

            if (index > 0 && index % 19 == 0 && index < Score.Count - 1)
            {
                page++;
                yOffset = 0;

                AddHtmlLocalized(320, 570, 100, 16, 1044045, Orange); // NEXT PAGE
                AddButton(404, 570, 4005, 4006, 0, GumpButtonType.Page, page);

                AddPage(page);

                AddHtmlLocalized(120, 570, 100, 16, 1044044, Orange); // PREV PAGE
                AddButton(80, 570, 4014, 4015, 0, GumpButtonType.Page, page - 1);
            }
        }
    }
}

public class VoidPoolRewardGump : BaseRewardGump
{
    public VoidPoolRewardGump(Mobile owner, PlayerMobile user) : base(owner, user, VoidPoolRewards.Rewards, 1152531)
    {
    }

    public override double GetPoints(Mobile m) => Engines.Points.VoidPool.Instance.GetPoints(m);

    public override int GetItemHue(Item i, CollectionItem item)
    {
        int hue;

        if (i != null)
        {
            hue = Points >= item.Points ? i.Hue : 0x3E9;
        }
        else
        {
            hue = Points >= item.Points ? CraftResources.GetHue((CraftResource)item.Hue) : 0x3E9;
        }

        return hue;
    }

    public override void OnConfirmed(CollectionItem citem, int index)
    {
        Item item;

        if (citem.Type.GetConstructor([typeof(CraftResource)]) != null)
        {
            item = Activator.CreateInstance(citem.Type, (CraftResource)citem.Hue) as Item;
        }
        else
        {
            item = Activator.CreateInstance(citem.Type) as Item;
        }

        if (item == null)
        {
            return;
        }

        if (item is BaseAddonDeed deed && citem.Type.GetConstructor([typeof(CraftResource)]) != null)
        {
            deed.Resource = (CraftResource)citem.Hue;
        }

        if (User.Backpack == null || !User.Backpack.TryDropItem(User, item, false))
        {
            User.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            item.Delete();
        }
        else
        {
            Engines.Points.VoidPool.Instance.DeductPoints(User, citem.Points);

            User.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
            User.PlaySound(0x5A7);
        }
    }
}
