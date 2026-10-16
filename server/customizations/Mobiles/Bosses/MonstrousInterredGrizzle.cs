// ServUO: Mobiles/Bosses/MonstrousInterredGrizzle.cs (cc-P129): the Bedlam peerless and its InfernalOoze, one file as
// in ServUO, on our BasePeerless (D-79, altar-less). Values verbatim; serialization by the generator.
//
// AI_Spellweaving is AI_Mage (D-41's shape: pinned has no Spellweaving AI; the Arcanist scrolls still pack).
// SetSpecialAbility(HowlOfCacophony) goes to the profile. SpillAcid(null, n) and NewHarmfulItem are pinned's own
// (BaseCreature.cs:4289, :4317). The ooze: ServUO's SpellHelper.AcquireIndirectTargets(owner, loc, map, 0) is
// reproduced as pinned's GetMobilesInRange(Location, 0) with the same filters (CrystalHydraBreath's reading of that
// method); IDurability is pinned's internal interface, reachable from this assembly; BalmOfProtection.HandleDamage is
// ours (cc-P127). The ooze deletes itself on load, as ServUO's Deserialize does ([AfterDeserialization]); it carries
// no persisted field. ServUO's unused m_Tiles array and m_NextDrop field are not ported. The Grizzle items, the
// mare statuette and the Grizzle armour are pinned's; the ParrotItem is ported beside this file.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class MonstrousInterredGrizzle : BasePeerless
{
    [Constructible]
    public MonstrousInterredGrizzle() : base(AIType.AI_Mage)
    {
        Body = 0x103;
        BaseSoundID = 589;

        SetStr(1198, 1207);
        SetDex(127, 135);
        SetInt(595, 646);

        SetHits(50000);

        SetDamage(27, 31);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 48, 52);
        SetResistance(ResistanceType.Fire, 77, 82);
        SetResistance(ResistanceType.Cold, 56, 61);
        SetResistance(ResistanceType.Poison, 32, 40);
        SetResistance(ResistanceType.Energy, 69, 71);

        SetSkill(SkillName.Wrestling, 112.6, 116.9);
        SetSkill(SkillName.Tactics, 118.5, 119.2);
        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Anatomy, 111.0, 111.7);
        SetSkill(SkillName.Magery, 100.0);
        SetSkill(SkillName.EvalInt, 100);
        SetSkill(SkillName.Meditation, 100);
        SetSkill(SkillName.Spellweaving, 100.0);

        Fame = 24000;
        Karma = -24000;

        VirtualArmor = 80;
        PackResources(8);
        PackTalismans(5);

        for (var i = 0; i < Utility.RandomMinMax(1, 6); i++)
        {
            PackItem(Loot.RandomScroll(0, Loot.ArcanistScrollTypes.Length - 1, SpellbookType.Arcanist));
        }

        SetSpecialAbility(SpecialAbility.HowlOfCacophony);
    }

    public override string CorpseName => "a monstrous interred grizzle corpse";
    public override string DefaultName => "a monstrous interred grizzle";

    public override bool GivesMLMinorArtifact => true;
    public override int TreasureMapLevel => 5;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 8);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new GrizzledBones());

        switch (Utility.Random(4))
        {
            case 0:
                c.DropItem(new TombstoneOfTheDamned());
                break;
            case 1:
                c.DropItem(new GlobOfMonstreousInterredGrizzle());
                break;
            case 2:
                c.DropItem(new MonsterousInterredGrizzleMaggots());
                break;
            case 3:
                c.DropItem(new GrizzledSkullCollection());
                break;
        }

        if (Utility.RandomDouble() < 0.6)
        {
            c.DropItem(new ParrotItem());
        }

        if (Utility.RandomDouble() < 0.05)
        {
            c.DropItem(new GrizzledMareStatuette());
        }

        if (Utility.RandomDouble() < 0.05)
        {
            switch (Utility.Random(5))
            {
                case 0:
                    c.DropItem(new GrizzleGauntlets());
                    break;
                case 1:
                    c.DropItem(new GrizzleGreaves());
                    break;
                case 2:
                    c.DropItem(new GrizzleHelm());
                    break;
                case 3:
                    c.DropItem(new GrizzleTunic());
                    break;
                case 4:
                    c.DropItem(new GrizzleVambraces());
                    break;
            }
        }
    }

    public override int GetDeathSound() => 0x57F;
    public override int GetAttackSound() => 0x580;
    public override int GetIdleSound() => 0x581;
    public override int GetAngerSound() => 0x582;
    public override int GetHurtSound() => 0x583;

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        if (Utility.RandomDouble() < 0.06)
        {
            SpillAcid(null, Utility.RandomMinMax(1, 3));
        }

        base.OnDamage(amount, from, willKill);
    }

    public override Item NewHarmfulItem() => new InfernalOoze(this, Utility.RandomBool());
}

[SerializationGenerator(0, false)]
public partial class InfernalOoze : Item
{
    private bool _corrosive;
    private readonly int _damage;
    private readonly Mobile _owner;
    private Timer _timer;

    private readonly DateTime _startTime;

    public InfernalOoze(Mobile owner) : this(owner, false)
    {
    }

    public InfernalOoze(Mobile owner, bool corrosive, int damage = 40) : base(0x122A)
    {
        Movable = false;
        _owner = owner;
        Hue = 0x95;

        _damage = damage;

        _corrosive = corrosive;
        _startTime = Core.Now;

        _timer = Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick);
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Corrosive
    {
        get => _corrosive;
        set => _corrosive = value;
    }

    private void OnTick()
    {
        if (ItemID == 0x122A && _startTime + TimeSpan.FromSeconds(30) < Core.Now)
        {
            ItemID++;
        }
        else if (_startTime + TimeSpan.FromSeconds(35) < Core.Now)
        {
            Delete();
            return;
        }

        if (_owner == null)
        {
            return;
        }

        if (!Deleted && Map != Map.Internal && Map != null)
        {
            // ServUO: SpellHelper.AcquireIndirectTargets(m_Owner, Location, Map, 0): every mobile on the tile that is
            // alive, in the owner's LOS, harmable by the owner and a valid indirect target.
            foreach (var m in Map.GetMobilesInRange(Location, 0))
            {
                if (m == _owner || !m.Alive || m is BaseCreature { IsDeadBondedPet: true })
                {
                    continue;
                }

                if (!_owner.InLOS(m) || !_owner.CanBeHarmful(m, false) || !SpellHelper.ValidIndirectTarget(_owner, m))
                {
                    continue;
                }

                OnMoveOver(m);
            }
        }
    }

    public override bool OnMoveOver(Mobile m)
    {
        if (Map == null)
        {
            return base.OnMoveOver(m);
        }

        if (m is BaseCreature bc && bc.GetMaster() is PlayerMobile || m.Player)
        {
            Damage(m);
        }

        return base.OnMoveOver(m);
    }

    public virtual void Damage(Mobile m)
    {
        if (_corrosive)
        {
            for (var i = 0; i < m.Items.Count; i++)
            {
                if (m.Items[i] is IDurability item && Utility.RandomDouble() < 0.25)
                {
                    if (item.HitPoints > 10)
                    {
                        item.HitPoints -= 10;
                    }
                    else
                    {
                        item.HitPoints -= 1;
                    }
                }
            }
        }
        else
        {
            var dmg = _damage;

            if (m is PlayerMobile pm)
            {
                dmg = (int)BalmOfProtection.HandleDamage(pm, dmg);
                AOS.Damage(m, _owner, dmg, 0, 0, 0, 100, 0);
            }
            else
            {
                AOS.Damage(m, _owner, dmg, 0, 0, 0, 100, 0);
            }
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _timer?.Stop();
        _timer = null;
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Delete();
    }
}
