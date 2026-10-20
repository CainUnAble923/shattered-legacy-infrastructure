// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/DemonicJailor.cs (cc-P194).
//
// The level-3 jailor. Every melee hit it lands sends the victim (and the victim's pets) back to the cell at
// 5703,639,0 with "You are captured by the jailor and returned to your cell"; a spell that damages it slows the
// caster to a walk for 15 seconds ("You are frozen and cannot move"); a player who comes within three tiles is
// frozen for five seconds by terror, once a minute; and it reveals the hidden as it moves (Detect Hidden 100).
//
// Conversion notes: ServUO passes range perception 15 (carried, it is not 10); OnGaveMeleeAttack,
// OnDamagedBySpell and OnGotMeleeAttack take a damage argument here; SendSpeedControl is NetState's with
// SpeedControlSetting.Walk / Disable; the paralyze table is a Dictionary of timers; DateTime.Now is Core.Now.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DemonicJailor : BaseCreature
{
    private static readonly Dictionary<Mobile, Timer> _table = new();

    private DateTime _nextTerror;

    [Constructible]
    public DemonicJailor() : base(AIType.AI_Melee, FightMode.Closest, 15)
    {
        Name = NameList.RandomName("male");
        SpeechHue = Utility.RandomDyedHue();
        Title = "the demonic jailor";
        Hue = 34531;
        Body = 0x190;

        SetStr(386, 400);
        SetDex(151, 165);
        SetInt(161, 175);

        SetDamage(8, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 25, 30);
        SetResistance(ResistanceType.Cold, 25, 30);
        SetResistance(ResistanceType.Poison, 10, 20);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.Anatomy, 125.0);
        SetSkill(SkillName.Fencing, 46.0, 77.5);
        SetSkill(SkillName.Macing, 35.0, 57.5);
        SetSkill(SkillName.Poisoning, 60.0, 82.5);
        SetSkill(SkillName.DetectHidden, 100);
        SetSkill(SkillName.MagicResist, 83.5, 92.5);
        SetSkill(SkillName.Swords, 125.0);
        SetSkill(SkillName.Tactics, 125.0);
        SetSkill(SkillName.Lumberjacking, 125.0);

        Fame = 5000;
        Karma = -5000;

        VirtualArmor = 40;

        this.SetWearable(new ShortPants(Utility.RandomRedHue()));
        AddItem(new Sandals(Utility.RandomRedHue()));
        AddItem(new Shirt(Utility.RandomRedHue()));
        AddItem(new SkinningKnife());

        Utility.AssignRandomHair(this);
    }

    public static Point3D CellLocation => new(5703, 639, 0);

    public override bool AlwaysMurderer => true;
    public override bool BardImmune => !Core.SE;
    public override bool Unprovokable => Core.SE;
    public override bool AreaPeaceImmune => Core.SE;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void OnGaveMeleeAttack(Mobile defender, int damage)
    {
        base.OnGaveMeleeAttack(defender, damage);

        var loc = CellLocation;
        var map = Map;

        Effects.SendLocationParticles(EffectItem.Create(loc, map, EffectItem.DefaultDuration), 0x3728, 10, 10, 0, 0, 2023, 0);
        Effects.PlaySound(loc, map, 0x1FE);

        TeleportPets(defender, loc, map);

        defender.MoveToWorld(loc, map);

        defender.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1152076); // You are captured by the jailor and returned to your cell.
    }

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);
        ParalyzeAttack(from);
    }

    public virtual void ParalyzeAttack(Mobile to)
    {
        if (to.Alive && to.Player && !_table.ContainsKey(to))
        {
            to.NetState?.SendSpeedControl(SpeedControlSetting.Walk);
            to.FixedEffect(0x376A, 6, 1);
            to.SendLocalizedMessage(500111); // You are frozen and cannot move.
            to.PlaySound(0x204);

            _table[to] = Timer.DelayCall(TimeSpan.FromSeconds(15), () => ParalyzeEnd(to));
        }
    }

    public virtual void ParalyzeEnd(Mobile from)
    {
        _table.Remove(from);

        from.NetState?.SendSpeedControl(SpeedControlSetting.Disable);
    }

    public static bool UnderParalyzeAttack(Mobile from) => _table.ContainsKey(from);

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        if (UseSkill(SkillName.DetectHidden))
        {
            m.RevealingAction();
        }

        base.OnMovement(m, oldLocation);

        if (_nextTerror < Core.Now && m != null && InRange(m.Location, 3) && m.AccessLevel == AccessLevel.Player)
        {
            m.Frozen = true;
            m.SendLocalizedMessage(1080342, Title, 33); // Terror slices into your very being, destroying any chance of resisting ~1_name~ you might have had

            Timer.DelayCall(TimeSpan.FromSeconds(5), () => Terrorize(m));
        }
    }

    private void Terrorize(Mobile m)
    {
        m.Frozen = false;
        m.SendLocalizedMessage(1005603); // You can move again!

        _nextTerror = Core.Now + TimeSpan.FromMinutes(1);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
        AddLoot(LootPack.Meager);
    }
}
