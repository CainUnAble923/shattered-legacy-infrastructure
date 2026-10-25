// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/ProfessionalFisherQuest.cs (with
// FishMonger.OnTalk and FishQuestHelper.HasFishQuest) -> ours, High Seas, second half. The profession fish order, the
// "Professional Fisher Quest":
//   - Talk to a fish monger with your boat near: he offers an order (one line on a small boat, up to six on a galleon:
//     a fish your Fishing can reach and how many, 5 to 20) for delivery to another port. Accept, and a shipping crate
//     for it is put in your boat's hold.
//   - Catch the fish and put them in the crate.
//   - Sail to the port of delivery and talk to its fish monger with the boat near: a full crate is taken, the fish are
//     used up (anything else in the crate goes to the hold), and you are paid from the Order of the Dragonfish's table
//     (FishQuestHelper.GiveRewards) and gain standing (PlayerFishingEntry).
//   - The monger who gave the order shows it again; an order not yet full shows its progress at the port.
//   - Destroying the crate forfeits the order and costs standing.
// Every text is ServUO's cliloc (1116507-1116519, 1116728-1116735).
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - No quest framework (see ShippingCrate): the order is the crate, and the offer, progress and delivery windows are one
//   gump of our own (FishOrderGump) in the shape of ServUO's MondainQuestGump: title, description, objectives, the fish,
//   the reward, and Accept and Refuse or Continue.
// - Every pinned boat is a small one (no galleons), so an order is always one line, as ServUO's for a classic boat.

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Engines.Quests;

public static class ProfessionalFisherQuest
{
    public const int BoatRange = 100;

    /// <summary>ServUO FishMonger.OnTalk with FishQuestHelper.HasFishQuest.</summary>
    public static void OnTalk(FishMonger monger, PlayerMobile player)
    {
        var boat = FishQuestHelper.GetBoat(player);

        if (boat == null)
        {
            monger.SayTo(player, 1116514); // Bring yer ship around, I might have some work for ye!
            return;
        }

        var inRange = boat.Map == monger.Map && monger.InRange(boat.Location, BoatRange);

        if (HasFishQuest(player, monger, inRange))
        {
            return;
        }

        var turnIn = FishQuestHelper.GetRandomMonger(player, monger);

        if (turnIn == null)
        {
            monger.SayTo(player, "It seems my fellow fish mongers are on vacation.  Try again later, or perhaps another Facet.");
            return;
        }

        var lines = BuildLines(player, boat);
        player.CloseGump<FishOrderGump>();
        player.SendGump(new FishOrderGump(FishOrderGump.Section.Offer, monger, turnIn, boat, GetTitle(monger), lines, null));

        // Every pinned boat is a classic boat.
        monger.SayTo(player, "Such a weak vessle can only catch a weak line.");
    }

    /// <summary>
    /// The player's orders, completed first: the giver shows it again; the port of delivery (the monger, or any monger
    /// in the same port) takes a full one with the boat near, or shows its progress. True when the talk is used up.
    /// </summary>
    public static bool HasFishQuest(PlayerMobile player, FishMonger monger, bool inRange)
    {
        var orders = ShippingCrate.OrdersOf(player);
        orders.Sort((a, b) => b.Completed.CompareTo(a.Completed));

        foreach (var order in orders)
        {
            if (order.Quester == monger)
            {
                player.CloseGump<FishOrderGump>();
                player.SendGump(new FishOrderGump(FishOrderGump.Section.InProgress, order));
                return true;
            }

            if (order.TurnIn == monger || order.TurnIn?.PortName != null && order.TurnIn.PortName == monger.PortName)
            {
                if (!inRange)
                {
                    monger.SayTo(player, 1116519); // I can't find your ship! You need to bring it in closer.
                    return true;
                }

                player.CloseGump<FishOrderGump>();
                player.SendGump(
                    new FishOrderGump(order.Completed ? FishOrderGump.Section.Complete : FishOrderGump.Section.InProgress, order)
                );
                return true;
            }
        }

        if (!inRange)
        {
            monger.SayTo(player, 1116514); // Bring yer ship around, I might have some work for ye!
            return true;
        }

        return false;
    }

    /// <summary>ServUO's order lines: one per line count, a fish not chosen yet and its amount, 5 to 20.</summary>
    public static Dictionary<Type, int> BuildLines(Mobile player, BaseBoat boat)
    {
        var entry = PlayerFishingEntry.GetEntry(player, true);
        var lines = 1; // a classic boat; ServUO: boat.IsClassicBoat ? 1 : entry.CalculateLines()

        var chosen = new List<int>();
        var types = new Dictionary<Type, int>();
        int index = 0, amount = 10;

        for (var i = 0; i < lines; i++)
        {
            entry.GetRandomFish(ref index, ref amount, chosen);
            chosen.Add(index);
            types[FishQuestHelper.GetTypeFromIndex(index)] = Math.Clamp(amount, 5, 20);
        }

        return types;
    }

    /// <summary>ServUO GetTitle: the order's name by the giver's port.</summary>
    public static int GetTitle(FishMonger monger) =>
        monger.PortName switch
        {
            "Britain"                    => 1116728, // Professional Fisher Quest - Britain
            "Trinsic"                    => 1116730,
            "Moonglow"                   => 1116731,
            "Skara Brae"                 => 1116732,
            "Vesper"                     => 1116733,
            "Jhelom" or "Jhelom Islands" => 1116734,
            "Papua"                      => 1116735,
            _                            => 1116507  // Professional Fisher Quest - Floating Emporium
        };

    /// <summary>ServUO OnAccept: the crate goes in the boat's hold. False (and nothing made) without a boat or hold.</summary>
    public static ShippingCrate Accept(
        PlayerMobile player, FishMonger quester, FishMonger turnIn, BaseBoat boat, int title, Dictionary<Type, int> lines
    )
    {
        if (boat?.Deleted != false || boat.Hold?.Deleted != false)
        {
            return null;
        }

        var crate = new ShippingCrate(player, quester, turnIn, boat, title, lines);
        boat.Hold.DropItem(crate);
        return crate;
    }

    /// <summary>ServUO GiveRewards: points and standing for the order, the reward, and the fish handed in.</summary>
    public static void Complete(PlayerMobile player, ShippingCrate order)
    {
        if (order.Deleted || !order.Completed || order.Owner != player)
        {
            return;
        }

        var entry = PlayerFishingEntry.GetEntry(player, true);
        var points = entry.GetPointsAwarded(order);
        entry.OnQuestComplete(order);

        FishQuestHelper.GiveRewards(player, entry, points);
        order.DeleteQuestItems();
    }
}

/// <summary>The order's window, in the shape of ServUO's MondainQuestGump for this quest.</summary>
public class FishOrderGump : Gump
{
    public enum Section
    {
        Offer,
        InProgress,
        Complete
    }

    private readonly BaseBoat _boat;
    private readonly Dictionary<Type, int> _lines;
    private readonly ShippingCrate _order;
    private readonly FishMonger _quester;
    private readonly Section _section;
    private readonly int _title;
    private readonly FishMonger _turnIn;

    public FishOrderGump(Section section, ShippingCrate order) :
        this(section, order.Quester, order.TurnIn, order.Boat, order.Title, null, order)
    {
    }

    public FishOrderGump(
        Section section, FishMonger quester, FishMonger turnIn, BaseBoat boat, int title, Dictionary<Type, int> lines,
        ShippingCrate order
    ) : base(75, 25)
    {
        _section = section;
        _quester = quester;
        _turnIn = turnIn;
        _boat = boat;
        _title = title;
        _lines = lines;
        _order = order;

        AddPage(0);
        AddImageTiled(50, 20, 400, 460, 0x1404);
        AddImageTiled(50, 29, 30, 440, 0x28DC);
        AddImageTiled(34, 140, 17, 279, 0x242F);
        AddImage(48, 135, 0x28AB);
        AddImage(-16, 285, 0x28A2);
        AddImage(0, 10, 0x28B5);
        AddImage(25, 0, 0x28B4);
        AddImageTiled(83, 15, 350, 15, 0x280A);
        AddImage(34, 419, 0x2842);
        AddImage(442, 419, 0x2840);
        AddImageTiled(51, 419, 392, 17, 0x2775);
        AddImageTiled(415, 29, 44, 450, 0xA2D);
        AddImageTiled(415, 29, 30, 390, 0x28DC);
        AddImage(370, 50, 0x589);
        AddImage(379, 60, 0x15A9);
        AddImage(425, 0, 0x28C9);
        AddImage(90, 33, 0x232D);
        AddImageTiled(130, 65, 175, 1, 0x238D);

        AddHtmlLocalized(130, 45, 270, 16, section == Section.Offer ? 1049010 : 1046026, 0xFFFFFF); // Quest Offer / Quest Log
        AddHtmlLocalized(160, 70, 200, 40, _title, 0x2710); // the order's name

        var y = 140;
        AddHtmlLocalized(98, y, 312, 16, 1072202, 0x2710); // Description
        y += 16;

        var text = section switch
        {
            Section.Offer      => 1116508, // the offer
            Section.InProgress => 1116512, // Aye, partner, how goes the fishin? ...
            _                  => 1116513  // Ahoy, sailor! These be a fine batch o' fish ...
        };

        AddHtmlLocalized(98, y, 312, 120, text, 0x15F90, false, true);
        y += 128;

        AddHtmlLocalized(98, y, 312, 16, 1049073, 0x2710); // Objective:
        y += 18;
        AddHtmlLocalized(98, y, 312, 16, 1116509, 0x15F90); // Fill the crate on your ship with the correct fish.
        y += 16;
        AddHtmlLocalized(98, y, 312, 16, 1116518, 0x15F90); // Speak with the fishmonger at the port of delivery.
        y += 20;

        foreach (var (type, have, need) in Lines())
        {
            var idx = FishQuestHelper.GetIndexForType(type);
            AddHtmlLocalized(108, y, 300, 16, 1116453, $"{FishQuestHelper.Labels[idx]:#}\t{have}\t{need}", 0x7FFF); // ~1_val~: ~2_val~/~3_val~
            y += 16;
        }

        y += 4;
        AddHtmlLocalized(98, y, 312, 16, 1072201, 0x2710); // Reward
        AddHtmlLocalized(150, y, 260, 16, 1116510, 0x15F90); // A rare reward from the Order of the Dragonfish.

        switch (section)
        {
            case Section.Offer:
                {
                    AddButton(95, 455, 0x2EE0, 0x2EE2, 1);  // Accept
                    AddButton(313, 455, 0x2EF2, 0x2EF4, 2); // Refuse
                    break;
                }
            case Section.InProgress:
                {
                    AddButton(313, 455, 0x2EEC, 0x2EEE, 0); // Close
                    break;
                }
            case Section.Complete:
                {
                    AddButton(313, 455, 0x2EE9, 0x2EEB, 3); // Continue
                    break;
                }
        }
    }

    private IEnumerable<(Type, int, int)> Lines()
    {
        if (_order != null)
        {
            for (var i = 0; i < _order.LineCount; i++)
            {
                yield return (_order.LineType(i), _order.LineHave(i), _order.LineNeeded(i));
            }
        }
        else if (_lines != null)
        {
            foreach (var (type, amount) in _lines)
            {
                yield return (type, 0, amount);
            }
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile player)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 1 when _section == Section.Offer:
                {
                    if (ProfessionalFisherQuest.Accept(player, _quester, _turnIn, _boat, _title, _lines) == null)
                    {
                        _quester?.SayTo(player, 1116514); // Bring yer ship around, I might have some work for ye!
                    }

                    break;
                }
            case 2 when _section == Section.Offer:
                {
                    _quester?.SayTo(player, 1116511); // As you wish.  If'n ye change yer mind you know where to find me.
                    break;
                }
            case 3 when _section == Section.Complete && _order != null:
                {
                    ProfessionalFisherQuest.Complete(player, _order);
                    break;
                }
        }
    }
}
