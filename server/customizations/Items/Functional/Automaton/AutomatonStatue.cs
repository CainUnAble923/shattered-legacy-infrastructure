// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/AutomatonStatue.cs. A dormant automaton statue in Kotl City
// (art 0x9DB3 or 0x9DB4, "Automaton", immovable): a player within 5 tiles has an even chance, on each step, to wake
// it. Waking spawns a wild KotlAutomaton on the statue that attacks that player; the statue goes invisible and stops
// watching. When the automaton dies wild in the KotlCity region (KotlAutomaton.OnBeforeDeath), its statue is deleted.
// ServUO places these through its XML spawner (Spawns/TreasuresOfKotl.xml, TerMur: TreasuresOfKotl4 centre 587,2289
// max 4; TreasuresOfKotl5 centre 491,2299 max 5; TreasuresOfKotl3 centre 643,2300 max 8); nothing here places one.
//
// Differences from ServUO:
//   - Serialization: ServUO writes a flag, 0 when the statue had an automaton (followed by it) and 1 when not, and on
//     load deletes a statue whose flag says it had one that no longer reads back (AutomatonStatue.cs:79-104). Here the
//     generator saves the automaton and a private _linked flag, and [AfterDeserialization] makes the same deletion.
//   - Timer.DelayCall(() => automaton.Combatant = m) is Timer.StartTimer, pinned's no-handle form.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AutomatonStatue : Item
{
    [SerializableField(1, getter: "private", setter: "private")]
    private bool _linked;

    [Constructible]
    public AutomatonStatue() : base(Utility.RandomBool() ? 0x9DB3 : 0x9DB4) => Movable = false;

    public static Dictionary<KotlAutomaton, AutomatonStatue> Statues { get; set; }

    private KotlAutomaton _automaton;

    [SerializableProperty(0, useField: nameof(_automaton))]
    [CommandProperty(AccessLevel.GameMaster)]
    public KotlAutomaton Automaton
    {
        get => _automaton;
        set
        {
            _automaton = value;
            _linked = value != null;
            this.MarkDirty();
        }
    }

    public override int LabelNumber => 1124395; // Automaton
    public override bool HandlesOnMovement => _automaton == null;

    public override void OnMovement(Mobile m, Point3D lastLocation)
    {
        base.OnMovement(m, lastLocation);

        if (m.Player && m.InRange(Location, 5) && m.AccessLevel == AccessLevel.Player && 0.5 > Utility.RandomDouble())
        {
            var automaton = new KotlAutomaton();
            automaton.MoveToWorld(Location, Map);

            OnBirth(automaton, this);

            Visible = false;
            Automaton = automaton;

            Timer.StartTimer(() => automaton.Combatant = m);
        }
    }

    public static void OnBirth(KotlAutomaton automaton, AutomatonStatue statue)
    {
        Statues ??= new Dictionary<KotlAutomaton, AutomatonStatue>();

        if (!Statues.ContainsKey(automaton))
        {
            Statues[automaton] = statue;
        }
    }

    public static void OnDeath(KotlAutomaton automaton)
    {
        if (Statues == null)
        {
            return;
        }

        if (Statues.TryGetValue(automaton, out var statue) && statue?.Deleted == false)
        {
            statue.Delete();
            Statues.Remove(automaton);

            if (Statues.Count == 0)
            {
                Statues = null;
            }
        }
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        if (_linked && _automaton == null)
        {
            Delete();
        }
    }
}
