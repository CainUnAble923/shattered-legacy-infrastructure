// ServUO: Mobiles/Normal/DespiseCreature.cs (CC4 Despise).
//
// The `Alignment` enum ServUO declares at the top of this file is NOT redeclared here. CC9 batch 5
// carried it in Items/Artifacts/Equipment/Armor/Sets/Epiphany/EpiphanyHelper.cs (namespace
// Server.Mobiles, the same three values in the same order) and said this port should take it from
// there rather than redeclare it.
//
// Members with no ModernUO counterpart, and what was done (evidence in notes/cc4-despise.md):
//
//   NoLootOnDeath = true            ServUO gates GenerateLoot and the treasure-level path on it
//                                   (BaseCreature.cs:5374, :5894, :5933). This creature has no loot to
//                                   generate, so NoKillAwards = true below covers what is observable.
//   GivesFameAndKarmaAward => false ModernUO gates fame, karma and looting rights on NoKillAwards
//                                   (BaseCreature.cs:3287), so NoKillAwards = true is the equivalent.
//   ForceNotoriety => true          Absent. ServUO's Notoriety.cs:188/371 uses it to compute a controlled
//                                   creature's notoriety from itself rather than its master. Here a
//                                   possessed creature's name colour follows the possessing player (D-33).
//   CanAutoStable => false          Added to BaseCreature by BaseCreature-PlayerMobile-can-auto-stable.patch
//                                   so a possessed creature is not taken into the stable on logout.
//   FightMode.Good / FightMode.Evil ServUO pub57's target acquisition applies no karma filter for either
//                                   mode: BaseAI.cs:2846-2960 accepts a target when IsHostile (Combatant
//                                   relation, :2968) or IsEnemy, and the karma test lives entirely in the
//                                   IsEnemy override below, ported verbatim. ModernUO's FightMode.Evil DOES
//                                   filter non-hostiles to karma < 0 (BaseAI.cs:868-876), which would
//                                   narrow the good creatures' targets, and it has no Good value at all.
//                                   Both map to FightMode.Closest, which ranks by distance exactly as the
//                                   two modes do on both emulators (GetFightModeRanking default arm).
//
// The XmlSpawner power roll ({RND,1,5} / {RND,4,8}) arrives through OnAfterSpawn from DespiseSpawner.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class DespiseCreature : BaseCreature
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private WispOrb _orb;

    // Power and Progress have setters with side effects (a level-up raises stats and plays effects).
    // Their storage is a private generated property so that a world load assigns the value without
    // running the setter; the public properties below carry ServUO's logic.
    [SerializableField(1, getter: "private", setter: "private")]
    private int _powerValue;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _maxPower;

    [SerializableField(3, getter: "private", setter: "private")]
    private int _progressValue;

    public DespiseCreature(AIType ai, FightMode fightmode) : base(ai, fightmode)
    {
        _maxPower = 10;
        _powerValue = 1;

        SetStr(StrStart);
        SetDex(DexStart);
        SetInt(IntStart);

        SetHits(HitsStart);
        SetStam(StamStart);
        SetMana(ManaStart);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 5, 50);
        SetResistance(ResistanceType.Fire, 5, 50);
        SetResistance(ResistanceType.Cold, 5, 50);
        SetResistance(ResistanceType.Poison, 5, 50);
        SetResistance(ResistanceType.Energy, 5, 50);

        SetSkill(SkillName.Wrestling, SkillStart);
        SetSkill(SkillName.Tactics, SkillStart);
        SetSkill(SkillName.MagicResist, SkillStart);
        SetSkill(SkillName.Anatomy, SkillStart);
        SetSkill(SkillName.Poisoning, SkillStart);
        SetSkill(SkillName.DetectHidden, SkillStart);
        SetSkill(SkillName.Parry, SkillStart);
        SetSkill(SkillName.Magery, SkillStart);
        SetSkill(SkillName.EvalInt, SkillStart);
        SetSkill(SkillName.Meditation, SkillStart);
        SetSkill(SkillName.Necromancy, SkillStart);
        SetSkill(SkillName.SpiritSpeak, SkillStart);
        SetSkill(SkillName.Focus, SkillStart);
        SetSkill(SkillName.Discordance, SkillStart);

        // ServUO: NoLootOnDeath = true, plus GivesFameAndKarmaAward => false. See the header.
        NoKillAwards = true;

        SetDamage(MinDamStart, MaxDamStart);
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public virtual Alignment Alignment
    {
        get
        {
            if (Karma > 0)
            {
                return Alignment.Good;
            }

            if (Karma < 0)
            {
                return Alignment.Evil;
            }

            return Alignment.Neutral;
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public int Power
    {
        get => _powerValue;
        set
        {
            var oldPower = _powerValue;

            if (value > _maxPower)
            {
                _powerValue = _maxPower;
            }

            if (oldPower < value)
            {
                _powerValue = value;
                IncreasePower();
                InvalidateProperties();
            }

            _orb?.InvalidateProperties();
            this.MarkDirty();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public int Progress
    {
        get => _progressValue;
        set
        {
            _progressValue = value;

            if (_progressValue >= _powerValue)
            {
                Power++;

                IncreaseResists();

                _progressValue = 0;
            }

            _orb?.InvalidateProperties();
            this.MarkDirty();
        }
    }

    public virtual int TightLeashLength => 1;
    public virtual int ShortLeashLength => 1;
    public virtual int LongLeashLength => 10;

    public virtual int StatRatio => Utility.RandomMinMax(35, 60);

    public virtual double SkillStart => Utility.RandomMinMax(80, 130);
    public virtual double SkillMax => _maxPower == 15 ? 130.0 : 110.0;

    public virtual int StrStart => Utility.RandomMinMax(91, 100);
    public virtual int DexStart => Utility.RandomMinMax(91, 100);
    public virtual int IntStart => Utility.RandomMinMax(91, 100);

    public virtual int StrMax => 600;
    public virtual int DexMax => 150;
    public virtual int IntMax => 450;

    public virtual int HitsStart => StrStart + (int)(StrStart * (StatRatio / 100.0));
    public virtual int StamStart => DexStart + (int)(DexStart * (StatRatio / 100.0));
    public virtual int ManaStart => IntStart + (int)(IntStart * (StatRatio / 100.0));

    public virtual int MaxHits => 1000;
    public virtual int MaxStam => 1000;
    public virtual int MaxMana => 1500;

    public virtual int MinDamStart => 8;
    public virtual int MaxDamStart => 13;

    public virtual int MinDamMax => 12;
    public virtual int MaxDamMax => 17;

    public virtual bool RaiseDamage => false;
    public virtual double RaiseDamageFactor => 0.33;

    public virtual int GetFame => _powerValue * 500;
    public virtual int GetKarmaGood => _powerValue * 500;
    public virtual int GetKarmaEvil => _powerValue * -500;

    public override bool Commandable => false;

    public override bool InitialInnocent => Alignment < Alignment.Evil;
    public override bool AlwaysMurderer => Alignment == Alignment.Evil;
    public override bool IsBondable => false;
    public override bool CanAutoStable => false;

    public override Poison HitPoison => null;

    public override TimeSpan ReacquireDelay
    {
        get
        {
            if (!Controlled || _orb == null || _orb.Aggression == Aggression.Defensive)
            {
                return TimeSpan.FromSeconds(10.0);
            }

            return TimeSpan.FromSeconds(Utility.RandomMinMax(4, 6));
        }
    }

    // ServUO's FightMode.Evil for good creatures and FightMode.Good for evil ones; see the header.
    public static FightMode AlignedFightMode(Alignment alignment) =>
        alignment == Alignment.Neutral ? FightMode.Aggressor : FightMode.Closest;

    public override bool IsEnemy(Mobile m)
    {
        if (m is PlayerMobile)
        {
            if (m.Karma <= 1000 && Alignment == Alignment.Good)
            {
                return true;
            }

            if (m.Karma >= 1000 && Alignment == Alignment.Evil)
            {
                return true;
            }
        }
        else if (m is DespiseCreature dc)
        {
            return dc.Alignment != Alignment;
        }

        return false;
    }

    public override bool CanBeRenamedBy(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player)
        {
            return base.CanBeRenamedBy(from);
        }

        return false;
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (ControlMaster != null)
        {
            list.Add(1153303, ControlMaster.Name); // Controller: ~1_NAME~
        }

        list.Add(1153297, $"{_powerValue}\t#{GetPowerLabel(_powerValue)}"); // Power Level: ~1_LEVEL~: ~2_VAL~
    }

    public override void OnCombatantChange()
    {
        base.OnCombatantChange();

        _orb?.InvalidateHue();
    }

    public override void OnKarmaChange(int oldValue)
    {
        if (oldValue < 0 && Karma > 0 || oldValue > 0 && Karma < 0)
        {
            FightMode = AlignedFightMode(Alignment);
        }
    }

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();

        // XmlSpawner: Silenii,{RND,1,5} — the power level was a random constructor argument. ModernUO's
        // spawner entries take fixed parameters only, so the roll lives on the spawner (DespiseSpawner).
        if (Spawner is DespiseSpawner spawner)
        {
            Power = spawner.RollPower();
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (_orb != null)
        {
            Unlink(false);
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_orb?.Deleted == false)
        {
            _orb.Pet = null;
        }
    }

    public int GetLeashLength()
    {
        if (_orb == null)
        {
            return RangePerception;
        }

        return _orb.LeashLength switch
        {
            LeashLength.Long => LongLeashLength,
            _                => ShortLeashLength
        };
    }

    public void Link(WispOrb orb)
    {
        _orb = orb;
        RangeHome = 2;
        _orb.InvalidateHue();
        this.MarkDirty();
    }

    public void Unlink(bool message = true)
    {
        RangeHome = 10;
        SetControlMaster(null);

        if (Alive && message)
        {
            if (_orb?.Owner != null)
            {
                _orb.Owner.SendLocalizedMessage(1153335, Name); // You have released control of ~1_NAME~.
                NonlocalOverheadMessage(MessageType.Regular, 0x59, 1153296, Name); // * This creature is no longer influenced by a Wisp Orb *
            }
        }

        if (_orb != null)
        {
            _orb.Conscripted = false;
            _orb.OnUnlinkPet();
            _orb.InvalidateHue();

            _orb = null;
            this.MarkDirty();
        }
    }

    public virtual void IncreasePower()
    {
        foreach (var skill in Skills)
        {
            if (skill != null && skill.Base > 0 && skill.Base < SkillMax)
            {
                var toRaise = SkillMax / _maxPower * _powerValue + Utility.RandomMinMax(-5, 5);

                if (toRaise > skill.Base)
                {
                    skill.Base = Math.Min(SkillMax, toRaise);
                }
            }
        }

        var strRaise = StrMax / 15 * _powerValue + Utility.RandomMinMax(-5, 5);
        var dexRaise = DexMax / 15 * _powerValue + Utility.RandomMinMax(-5, 5);
        var intRaise = IntMax / 15 * _powerValue + Utility.RandomMinMax(-5, 5);

        if (strRaise > RawStr)
        {
            SetStr(Math.Min(StrMax, strRaise));
        }

        if (dexRaise > RawDex)
        {
            SetDex(Math.Min(DexMax, dexRaise));
        }

        if (intRaise > RawInt)
        {
            SetInt(Math.Min(IntMax, intRaise));
        }

        var hitsRaise = MaxHits / 15 * _powerValue + Utility.RandomMinMax(-5, 5);
        var stamRaise = MaxStam / 15 * _powerValue + Utility.RandomMinMax(-5, 5);
        var manaRaise = MaxMana / 15 * _powerValue + Utility.RandomMinMax(-5, 5);

        if (hitsRaise > HitsMax)
        {
            SetHits(Math.Min(MaxHits, hitsRaise));
        }

        if (stamRaise > StamMax)
        {
            SetStam(Math.Min(MaxStam, stamRaise));
        }

        if (manaRaise > ManaMax)
        {
            SetMana(Math.Min(MaxMana, manaRaise));
        }

        if (RaiseDamage && Utility.RandomDouble() < RaiseDamageFactor)
        {
            DamageMin = Math.Min(MinDamMax, DamageMin + 1);
            DamageMax = Math.Min(MaxDamMax, DamageMax + 1);
        }

        FixedEffect(0x373A, 10, 30);
        PlaySound(0x209);
    }

    private void IncreaseResists()
    {
        SetResistance(ResistanceType.Physical, Math.Min(80, PhysicalResistanceSeed + Utility.RandomMinMax(5, 15)));
        SetResistance(ResistanceType.Fire, Math.Min(80, FireResistSeed + Utility.RandomMinMax(5, 15)));
        SetResistance(ResistanceType.Cold, Math.Min(80, ColdResistSeed + Utility.RandomMinMax(5, 15)));
        SetResistance(ResistanceType.Poison, Math.Min(80, PoisonResistSeed + Utility.RandomMinMax(5, 15)));
        SetResistance(ResistanceType.Energy, Math.Min(80, EnergyResistSeed + Utility.RandomMinMax(5, 15)));
    }

    public static int GetPowerLabel(int power) =>
        power switch
        {
            4 or 5 or 6 => 1153299, // Improved
            7 or 8      => 1153300, // Heightened
            9 or 10     => 1153301, // Magnified
            11 or 12    => 1153302, // Amplified
            13 or 14    => 1153307, // Inspired
            15          => 1153308, // Galvanized
            _           => 1153298  // Normal (0, 1, 2, 3 and anything else, as ServUO's default arm)
        };
}
