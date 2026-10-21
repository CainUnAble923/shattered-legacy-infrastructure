// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/KeypunchReader.cs (cc-P195): the machine in the Exodus
// Dungeon that eats fifty punch cards and an Exotic Toolkit and prints a Nexus deed. An ornate wooden chest, hued,
// fixed; ServUO checks on every drop once it holds fifty things.

using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class KeypunchReader : OrnateWoodenChest
{
    public const int CardsNeeded = 50;

    [Constructible]
    public KeypunchReader()
    {
        Weight = 0.0;
        Hue = 2500;
        Movable = false;
    }

    public override int LabelNumber => 1153868; // Keypunch Reader

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (!base.OnDragDrop(from, dropped))
        {
            return false;
        }

        AfterDrop(from, dropped);
        return true;
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p)
    {
        if (!base.OnDragDropInto(from, item, p))
        {
            return false;
        }

        AfterDrop(from, item);
        return true;
    }

    private void AfterDrop(Mobile from, Item dropped)
    {
        if (TotalItems >= CardsNeeded)
        {
            CheckItems(from);
        }

        if (dropped is PunchCard && !dropped.Deleted)
        {
            from.SendLocalizedMessage(1152375); // You feed the punch card into the machine but nothing happens.
        }
    }

    /// <summary>ServUO CheckItems: fifty cards and a kit become a Nexus deed; the cards are spent, the kit stays.</summary>
    public bool CheckItems(Mobile m)
    {
        var cards = Items.Where(x => x is PunchCard).ToList();
        var kits = Items.Count(x => x is ExoticToolkit);

        if (cards.Count < CardsNeeded || kits < 1)
        {
            return false;
        }

        foreach (var card in cards)
        {
            card.Delete();
        }

        DropItem(new NexusAddonDeed());
        m?.SendLocalizedMessage(1152376); // As you feed the punch card into the machine it turns on!
        return true;
    }
}
