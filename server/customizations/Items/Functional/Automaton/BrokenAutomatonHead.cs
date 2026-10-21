// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/BrokenAutomatonHead.cs. "~1_NAME~'s Broken ~2_TYPE~
// Automaton Head" (art 0x9DB1/0x9DB2, blessed, weight 1): what a dead owned KotlAutomaton leaves in its owner's pack
// (KotlAutomaton.OnDeath). It cannot be dropped on the ground or traded, only moved between the pack and the bank.
// TryRepair spends 75 of the automaton's metal (crystalline blackrock for a blackrock automaton) from the owner's pack,
// deletes the head and brings the automaton back resurrected at full hits at the owner's feet.
//
// Differences from ServUO:
//   - Weight = 1.0 in the constructor is DefaultWeight => 1.0, pinned's idiom.
//   - TryRepair has no caller on this shard: ServUO reaches it from a tinker's repair tool (Services/Craft/Core/
//     Repair.cs:233-238). Pinned's Repair.cs has no such branch; the hook is in the report. ServUO's Veterinarian also
//     lists the head's automaton among the dead pets (Veterinarian.cs:47-53); that is a second hook.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x9DB1, 0x9DB2)]
[SerializationGenerator(0, false)]
public partial class BrokenAutomatonHead : Item
{
    public static int RepairAmount = 75;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private KotlAutomaton _automaton;

    public BrokenAutomatonHead(KotlAutomaton automaton) : base(0x9DB1)
    {
        _automaton = automaton;

        LootType = LootType.Blessed;
    }

    public override double DefaultWeight => 1.0;

    [CommandProperty(AccessLevel.GameMaster)]
    public virtual Type RepairResource => _automaton == null ? typeof(IronIngot) : _automaton.RepairResource;

    public override bool Nontransferable => true;

    public override void AddNameProperty(IPropertyList list)
    {
        if (_automaton != null)
        {
            if (_automaton is BlackrockAutomaton)
            {
                list.Add(1157046, $"{_automaton.Name}\t#1157178"); // ~1_NAME~'s Broken ~2_TYPE~ Automaton Head
            }
            else
            {
                // ~1_NAME~'s Broken ~2_TYPE~ Automaton Head
                list.Add(1157046, $"{_automaton.Name}\t#{CraftResources.GetLocalizationNumber(_automaton.Resource)}");
            }
        }
        else
        {
            base.AddNameProperty(list);
        }
    }

    public virtual bool TryRepair(Mobile from)
    {
        if (from.Backpack != null && IsChildOf(from.Backpack))
        {
            var res = RepairResource;

            if (_automaton == null || _automaton.ControlMaster != from)
            {
                from.SendLocalizedMessage(1157051); // You must be the owner of that pet to have it repair.
            }
            else if (from.Backpack.GetAmount(res) < RepairAmount)
            {
                if (res == typeof(CrystallineBlackrock))
                {
                    from.SendLocalizedMessage(1157179, $"\t{_automaton.Name}");
                }
                else
                {
                    // You need 75 ~1_MATERIAL~ ingots to repair the ~2_CREATURE~.
                    from.SendLocalizedMessage(
                        1157050,
                        $"#{CraftResources.GetLocalizationNumber(_automaton.Resource)}\t{_automaton.Name}"
                    );
                }
            }
            else
            {
                from.Backpack.ConsumeTotal(res, RepairAmount);

                return RetrieveAutomaton(from);
            }
        }

        return false;
    }

    public virtual bool RetrieveAutomaton(Mobile from)
    {
        var automaton = _automaton;

        Delete();

        if (automaton?.Deleted == false)
        {
            automaton.ResurrectPet();
            automaton.Hits = automaton.HitsMax;

            automaton.MoveToWorld(from.Location, from.Map);
            from.PlaySound(0x23B);
        }
        else
        {
            from.SendMessage("An error has occured, your automaton has been deleted!");
            return false;
        }

        return true;
    }

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p)
    {
        if (target is BankBox)
        {
            return true;
        }

        return base.OnDroppedInto(from, target, p);
    }

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        from.SendLocalizedMessage(1076254); // That item cannot be dropped.
        return false;
    }

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted)
    {
        from.SendLocalizedMessage(1076256); // That item cannot be traded.
        return false;
    }
}
