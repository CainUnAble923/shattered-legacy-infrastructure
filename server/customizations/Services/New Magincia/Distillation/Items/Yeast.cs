// ServUO: Services/New Magincia/Distillation/Items/Yeast.cs (CC6 batch 3). Values verbatim. The file sits in the
// distillation subsystem's folder and imports its namespace, but its body references nothing from it: it is a leaf
// item with one persisted int, so it is ported alone (the brief's "read it first" answer). The distillation system
// that consumes it (CraftDefinition, DistillationContext, DistillationGump, DistillationSystem, FementationBarrel) is
// not ported. Eight ServUO orcs pack it at 50%; OrcScout is the first here.
//
// Serialization: ServUO writes the one int by hand; here it is a [SerializableProperty], the shape stock
// Container.cs:86 uses for a property whose setter does more than assign (this one clamps to 1..5). The generator
// declares the _bacterialResistance backing field itself (build A of batch 3 went red on a duplicate when this file
// declared it too). ServUO's (int resistance) constructor sets no Hue, unlike the random one; kept as written.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Yeast : Item
{
    [Constructible]
    public Yeast() : base(3624)
    {
        Hue = 2418;
        var ran = Utility.Random(100);

        if (ran <= 5)
        {
            _bacterialResistance = 5;
        }
        else if (ran <= 10)
        {
            _bacterialResistance = 4;
        }
        else if (ran <= 20)
        {
            _bacterialResistance = 3;
        }
        else if (ran <= 40)
        {
            _bacterialResistance = 2;
        }
        else
        {
            _bacterialResistance = 1;
        }
    }

    [Constructible]
    public Yeast(int resistance) : base(3624)
    {
        BacterialResistance = resistance;
    }

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int BacterialResistance
    {
        get => _bacterialResistance;
        set
        {
            _bacterialResistance = Math.Clamp(value, 1, 5);
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    public override int LabelNumber => 1150453; // yeast

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1150455, GetResistanceLabel()); // Bacterial Resistance: ~1_VAL~
    }

    private string GetResistanceLabel() =>
        _bacterialResistance switch
        {
            4 => "+",
            3 => "+-",
            2 => "-",
            1 => "--",
            _ => "++" // 5, and ServUO's default arm
        };
}
