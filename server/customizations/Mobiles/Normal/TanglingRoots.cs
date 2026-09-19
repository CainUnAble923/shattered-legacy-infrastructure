// ServUO: Mobiles/Normal/TanglingRoots.cs (CC6 batch 1). Values verbatim; serialization by the generator.
//
// The entangle-on-movement mechanic is ported as is. Two ServUO TimerStateCallback<Mobile> delays become
// Timer.DelayCall lambdas; the per-victim acid timer is kept in the same dictionary and stopped the same way.
// FountainOfFortune.UnderProtection is ours (S2, Items/Addons/FountainOfFortune.cs), the same member ServUO
// calls. TransformationSpellHelper.UnderTransformation(Mobile, Type) and EtherealVoyageSpell exist unchanged.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells;
using Server.Spells.Spellweaving;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TanglingRoots : BaseCreature
{
    private static readonly List<Mobile> _tangleCooldown = new();
    private readonly Dictionary<Mobile, Timer> _damageTable = new();

    [Constructible]
    public TanglingRoots() : base(AIType.AI_Melee)
    {
        Body = 8;
        BaseSoundID = 684;

        SetStr(157, 189);
        SetDex(51, 64);
        SetInt(26, 39);

        SetHits(231, 246);
        SetMana(0);

        SetDamage(10, 23);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Poison, 40);

        SetResistance(ResistanceType.Physical, 35, 40);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.MagicResist, 15.1, 20.0);
        SetSkill(SkillName.Tactics, 45.1, 60.0);
        SetSkill(SkillName.Wrestling, 45.1, 60.0);

        Fame = 3000;
        Karma = -3000;

        VirtualArmor = 18;

        if (0.25 > Utility.RandomDouble())
        {
            PackItem(new Board(10));
        }
        else
        {
            PackItem(new Log(10));
        }

        PackItem(new MandrakeRoot(3));
    }

    public override string CorpseName => "a tangling root corpse";
    public override string DefaultName => "a tangling root";

    public override Poison PoisonImmune => Poison.Lesser;
    public override bool DisallowAllMoves => true;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        if (m.Alive && !m.IsDeadBondedPet && m.AccessLevel == AccessLevel.Player && !m.Hidden &&
            !TransformationSpellHelper.UnderTransformation(m, typeof(EtherealVoyageSpell)))
        {
            if (0.2 > Utility.RandomDouble() && !_tangleCooldown.Contains(m) && InRange(m, 6) &&
                !FountainOfFortune.UnderProtection(m))
            {
                m.Frozen = true;
                m.MoveToWorld(Location, Map);

                m.PlaySound(0x1FE);
                m.SendLocalizedMessage(1111641); // You become entangled in the acid drenched roots.

                _tangleCooldown.Add(m);

                Timer.DelayCall(TimeSpan.FromSeconds(Utility.RandomMinMax(3, 6)), () => Untangle(m));
                Timer.DelayCall(TimeSpan.FromSeconds(15.0), () => RemoveCooldown(m));
            }

            if (m.InRange(this, 1) && !_damageTable.ContainsKey(m))
            {
                // Should start the timer
                _damageTable[m] = Timer.DelayCall(TimeSpan.Zero, TimeSpan.FromSeconds(1.0), () => DoDamage(m));
            }
        }
    }

    protected void Untangle(Mobile m)
    {
        m.Frozen = false;
        m.SendLocalizedMessage(1111642); // You manage to untangle yourself.
    }

    protected void RemoveCooldown(Mobile m)
    {
        _tangleCooldown.Remove(m);
    }

    protected void DoDamage(Mobile m)
    {
        if (m.Alive && !m.IsDeadBondedPet && !Deleted && m.InRange(this, 1))
        {
            m.Damage(4, this);
            m.SendLocalizedMessage(1111643); // The acid is damaging you!
        }
        else if (_damageTable.Remove(m, out var t))
        {
            t.Stop();
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.02)
        {
            c.DropItem(new LuckyCoin());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
    }
}
