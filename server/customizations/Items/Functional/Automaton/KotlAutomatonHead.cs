// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/KotlAutomatonHead.cs. The crafted "Rebuilt ~1_MATTYPE~
// Automaton Head" (art 0x9DB1/0x9DB2, blessed): a Grandmaster tinker double-clicks it, and three seconds later it
// becomes a bonded KotlAutomaton of the head's metal at the tinker's feet, or nothing if the tinker has no follower
// room. Made by ServUO's tinkering recipe 458 (DefTinkering.cs:651-656: 300 iron ingots, an AutomatonActuator, a
// StasisChamberPowerCore and an InoperativeAutomatonHead); the head takes its metal from the ingots in OnCraft.
//
// Differences from ServUO:
//   - Resource is saved. ServUO's Serialize writes only a version (KotlAutomatonHead.cs:96-101), so after a restart a
//     crafted head reads CraftResource.None and builds an automaton with no metal (no hue, no resist or damage bonus,
//     KotlAutomaton.OnResourceChanged returns early). Saving it keeps what the tinker made. The "activating" flag is
//     not saved, as in ServUO.
//   - ICraftable.OnCraft takes pinned's BaseTool where ServUO has ITool, and reads craftItem.Resources[0] where
//     ServUO calls Resources.GetAt(0) (pinned CraftItem.cs:200 is a List<CraftRes>).

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[Flippable(0x9DB1, 0x9DB2)]
[SerializationGenerator(0, false)]
public partial class KotlAutomatonHead : Item, ICraftable
{
    private bool _activated;

    [Constructible]
    public KotlAutomatonHead() : base(0x9DB1)
    {
        LootType = LootType.Blessed;

        Resource = CraftResource.Iron;
    }

    // Loaded straight into the field (useField): the setter's Hue would undo a blackrock head's hue 1175 on every load.
    private CraftResource _resource;

    [SerializableProperty(0, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            _resource = value;
            Hue = CraftResources.GetHue(_resource);
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1157022, $"#{CraftResources.GetLocalizationNumber(_resource)}"); // Rebuilt ~1_MATTYPE~ Automaton Head
    }

    public override void OnDoubleClick(Mobile from)
    {
        var skill = (int)from.Skills[SkillName.Tinkering].Base;

        if (skill < 100.0)
        {
            from.SendLocalizedMessage(1157006); // You must be a Grandmaster Tinker to activate an Automaton.
        }
        else if (_activated)
        {
            from.SendLocalizedMessage(1157007); // The Automaton is already being activated.
        }
        else
        {
            _activated = true;

            Timer.StartTimer(TimeSpan.FromSeconds(3), () =>
                {
                    _activated = false;

                    var automaton = GetAutomaton(from);

                    if (automaton.SetControlMaster(from))
                    {
                        automaton.IsBonded = true;

                        Delete();

                        automaton.MoveToWorld(from.Location, from.Map);
                        from.PlaySound(0x23B);
                    }
                    else
                    {
                        automaton.Delete();
                    }
                }
            );
        }
    }

    public virtual KotlAutomaton GetAutomaton(Mobile master)
    {
        var automaton = new KotlAutomaton();
        automaton.Resource = _resource;

        return automaton;
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        if (typeRes == null)
        {
            typeRes = craftItem.Resources[0].ItemType;
        }

        Resource = CraftResources.GetFromType(typeRes);

        return quality;
    }
}
