// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/GooeyMaggots.cs (cc-P194).
//
// Gooey Maggots burst from Fezzik's stinking cauldrons, live three minutes, and leave slime behind on every tile
// they leave; a player who steps in the slime is slowed to a walk for a second ("You suddenly find yourself unable
// to run"). The slime fades after ten seconds.
//
// Conversion notes: OnLocationChange is protected on Mobile here; the self-delete and slime timers are
// Timer.DelayCall and are restarted after a load as ServUO's are; IsStaff is AccessLevel > Player.

using System;
using ModernUO.Serialization;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GooeyMaggots : BaseCreature
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    [Constructible]
    public GooeyMaggots() : base(AIType.AI_Melee)
    {
        Body = 319;
        BaseSoundID = 898;

        SetStr(738, 763);
        SetDex(61, 70);
        SetInt(10);

        SetMana(0);

        SetDamage(3, 9);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Tactics, 80.2, 89.7);
        SetSkill(SkillName.Wrestling, 80.2, 87.5);

        Fame = 1000;
        Karma = -1000;

        VirtualArmor = 24;

        BeginSelfDelete();
    }

    public override string CorpseName => "a gooey maggots corpse";
    public override string DefaultName => "Gooey Maggots";

    public override Poison PoisonImmune => Poison.Lethal;

    private void BeginSelfDelete()
    {
        Timer.DelayCall(
            Lifetime,
            () =>
            {
                if (!Deleted && Map != Map.Internal)
                {
                    Delete();
                }
            }
        );
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager, 2);
    }

    protected override void OnLocationChange(Point3D oldLocation)
    {
        // Not on the first placement (oldLocation is 0,0,0 then; ServUO drops a slime at the map corner there).
        if (Map != null && Map != Map.Internal && oldLocation != Point3D.Zero)
        {
            new GooeyMaggotSlime().MoveToWorld(oldLocation, Map);
        }

        base.OnLocationChange(oldLocation);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        BeginSelfDelete();
    }
}

[SerializationGenerator(0, false)]
public partial class GooeyMaggotSlime : Item
{
    [Constructible]
    public GooeyMaggotSlime() : this(Utility.RandomList(0x1645, 0x122A, 0x122B, 0x122C, 0x122D, 0x122E, 0x122F))
    {
    }

    [Constructible]
    public GooeyMaggotSlime(int itemID) : base(itemID)
    {
        Movable = false;
        Hue = 363;

        BeginDecay();
    }

    public override int LabelNumber => 1015246; // Slime

    private void BeginDecay()
    {
        Timer.DelayCall(TimeSpan.FromSeconds(10.0), () => Delete());
    }

    public override bool OnMoveOver(Mobile from)
    {
        if (from.AccessLevel == AccessLevel.Player && from.Alive && from.Player)
        {
            from.NetState?.SendSpeedControl(SpeedControlSetting.Walk);
            from.SendLocalizedMessage(1152144); // You suddenly find yourself unable to run.

            Timer.DelayCall(
                TimeSpan.FromSeconds(1),
                () =>
                {
                    from.NetState?.SendSpeedControl(SpeedControlSetting.Disable);
                    from.SendLocalizedMessage(1152145); // You are are free to move again.
                }
            );

            Delete();
        }

        return base.OnMoveOver(from);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        BeginDecay();
    }
}
