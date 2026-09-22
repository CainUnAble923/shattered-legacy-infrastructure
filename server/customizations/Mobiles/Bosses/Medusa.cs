// ServUO: Mobiles/Bosses/Medusa.cs (CC6 batch 8, Part D). Medusa, the Abyss's gorgon, on BaseSABoss (the altar-less
// Peerless base, D-79: a walk-up fight), with her MedusaClone and the [addclone command in the same file, as ServUO
// has them. Body 728 is a bodyTable.cfg row. The gaze (a petrified copy of the target summoned beside her while the
// target stands frozen and blessed for 5-10 s, then the copy wakes and attacks), the petrified menagerie (up to five
// stone monsters summoned around her, one released when she is struck), the live harvest (a knife on her takes 2-3
// of her 8-9 scales a minute) and the corpse carve all port whole.
//
// What changed, each proved against D:\UO\ModernUO-pinned:
//   the Gorgon Lens (CheckBlockGaze)     pinned has no GorgonLense item, no LenseType, no GorgonLenseCharges on
//                                        BaseArmor, BaseClothing or BaseJewel: nothing a player wears can deflect
//                                        the gaze. The check is kept as the method it was and returns false (D-88).
//   IFreezable / OnRequestedAnimation    ServUO's PacketHandlers animate a frozen IFreezable as a statue; pinned's
//                                        PlayerMobile.CanSee (:3340) asks only a CharacterStatue. The petrified copy
//                                        stands still without the statue animation (D-89).
//   SetSpecialAbility(VenomousBite)      pinned has no MonsterAbility that poisons everyone within 3 tiles on a hit
//                                        (D-82, with the batch's others). SetWeaponAbility(MortalStrike) is
//                                        GetWeaponAbility (the recipe).
//   ICarvable.Carve                      returns void in pinned (Interfaces.cs:16), bool in ServUO; same body.
//                                        Knives reach it through BladedItemTarget.cs:34.
//   OnGotMeleeAttack / OnDamagedBySpell  pinned's virtuals carry the damage as a second argument; same bodies.
//   BuffInfo.AddBuff / RemoveBuff        pinned's are PlayerMobile instance methods; same icon and clilocs.
//   RemoveMobile / MobileIncoming        NetState.SendRemoveEntity and SendMobileIncoming (OutgoingEntityPackets.cs:75,
//                                        OutgoingMobilePackets.cs:601).
//   m_Scales                             the generated property would be `Scales` and hide BaseCreature.Scales (the
//                                        carve yield), so the field is _lightScales / LightScales. GM-visible only.
//   Deserialize's "alive helpers only"   [AfterDeserialization], after the world has loaded, so every helper it
//                                        checks has been read.
//   DateTime.UtcNow                      Core.Now.
// ServUO Abilities/SlayerGroup.cs:70 lists Medusa under Repond; pinned's is a static typeof list (D-86).
//
// ServUO quirks kept, bug-list section 3's kind: ReleaseStoneMonster sets each nearby player's Combatant to the
// player themself; GazeTimer's target filter reads `m != null && m is PlayerMobile || (...)`, so a null m reaches
// GetDistanceToSqrt first (it never is null: the enumerator yields mobiles); the gaze's loc search uses Random(10) - 1,
// a -1..8 offset, not the +-5 it reads as.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Medusa : BaseSABoss, ICarvable
{
    private readonly List<Mobile> _turnedToStone = new();
    public List<Mobile> AffectedMobiles => _turnedToStone;

    [SerializableField(1)]
    private List<Mobile> _helpers = new();

    [SerializableField(0)]
    private int _lightScales;

    private DateTime _gazeDelay;
    private DateTime _stoneDelay;
    private DateTime _nextCarve;

    [Constructible]
    public Medusa() : base(AIType.AI_Mage)
    {
        Body = 728;

        SetStr(1235, 1391);
        SetDex(128, 139);
        SetInt(537, 664);

        SetHits(60000);

        SetDamage(21, 28);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 55, 65);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 60, 75);

        SetSkill(SkillName.Anatomy, 110.6, 116.1);
        SetSkill(SkillName.EvalInt, 100.0, 114.4);
        SetSkill(SkillName.Magery, 100.0);
        SetSkill(SkillName.Meditation, 118.2, 127.8);
        SetSkill(SkillName.MagicResist, 120.0);
        SetSkill(SkillName.Tactics, 111.9, 134.5);
        SetSkill(SkillName.Wrestling, 119.7, 128.9);

        Fame = 22000;
        Karma = -22000;

        VirtualArmor = 60;

        PackItem(new Arrow(Utility.RandomMinMax(100, 200)));

        var bow = new IronwoodCompositeBow();
        bow.Movable = false;
        AddItem(bow);

        _lightScales = Utility.RandomMinMax(1, 2) + 7;

        // ServUO: SetSpecialAbility(SpecialAbility.VenomousBite); D-82.
    }

    public override string CorpseName => "a medusa corpse";
    public override string DefaultName => "Medusa";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.MortalStrike;

    public override Type[] UniqueSAList => new[]
    {
        typeof(Slither), typeof(IronwoodCompositeBow), typeof(Venom), typeof(PetrifiedSnake),
        typeof(StoneDragonsTooth), typeof(MedusaFloorTileAddonDeed)
    };

    public override Type[] SharedSAList => new[] { typeof(SummonersKilt) };

    public override bool IgnoreYoungProtection => true;
    public override bool AutoDispel => true;
    public override double AutoDispelChance => 1.0;
    public override bool BardImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => 0.8 >= Utility.RandomDouble() ? Poison.Deadly : Poison.Lethal;

    public override int GetIdleSound() => 1557;
    public override int GetAngerSound() => 1554;
    public override int GetHurtSound() => 1556;
    public override int GetDeathSound() => 1555;

    public override void OnCarve(Mobile from, Corpse corpse, Item with)
    {
        var amount = Utility.Random(5) + 1;

        corpse.DropItem(new MedusaDarkScales(amount));

        if (0.20 > Utility.RandomDouble())
        {
            corpse.DropItem(new MedusaBlood());
        }

        base.OnCarve(from, corpse, with);

        corpse.Carved = true;
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);

        if (0.05 > Utility.RandomDouble())
        {
            ReleaseStoneMonster();
        }
    }

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);

        if (0.05 > Utility.RandomDouble())
        {
            ReleaseStoneMonster();
        }
    }

    public override void OnHarmfulSpell(Mobile from)
    {
        base.OnHarmfulSpell(from);

        if (0.05 > Utility.RandomDouble())
        {
            ReleaseStoneMonster();
        }
    }

    public void RemoveAffectedMobiles(Mobile toRemove)
    {
        if (_turnedToStone.Contains(toRemove))
        {
            _turnedToStone.Remove(toRemove);
        }
    }

    public Mobile FindRandomMedusaTarget()
    {
        var list = new List<Mobile>();

        foreach (var m in GetMobilesInRange(12))
        {
            if (m == null || m == this || _turnedToStone.Contains(m) || !CanBeHarmful(m) || !InLOS(m) ||
                m.AccessLevel > AccessLevel.Player)
            {
                continue;
            }

            // Pets
            if (m is BaseCreature bc && bc.GetMaster() is PlayerMobile)
            {
                list.Add(m);
            }
            // players
            else if (m is PlayerMobile)
            {
                list.Add(m);
            }
        }

        if (list.Count == 0)
        {
            return null;
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        return list[Utility.Random(list.Count)];
    }

    // ServUO: walks the helm, shield, neck and earring slots for GorgonLenseCharges and rolls the lens type's
    // effectiveness (Enhanced 100%, Regular 50%, Limited 15%). Pinned has no Gorgon Lens on any base, so nothing can
    // deflect the gaze (D-88).
    public static bool CheckBlockGaze(Mobile m) => false;

    // ServUO: bool Carve(Mobile from, Item item); pinned's ICarvable returns void.
    public void Carve(Mobile from, Item item)
    {
        if (_lightScales > 0)
        {
            if (Core.Now < _nextCarve)
            {
                // The creature is still recovering from the previous harvest. Try again in a few seconds.
                from.SendLocalizedMessage(1112677);
            }
            else
            {
                var amount = Math.Min(_lightScales, Utility.RandomMinMax(2, 3));

                LightScales -= amount;

                Item scales = new MedusaLightScales(amount);

                if (from.PlaceInBackpack(scales))
                {
                    // You harvest magical resources from the creature and place it in your bag.
                    from.SendLocalizedMessage(1112676);
                }
                else
                {
                    scales.MoveToWorld(from.Location, from.Map);
                }

                new Blood(0x122D).MoveToWorld(Location, Map);

                _nextCarve = Core.Now + TimeSpan.FromMinutes(1.0);
            }
        }
        else
        {
            from.SendLocalizedMessage(1112674); // There's nothing left to harvest from this creature.
        }
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Combatant == null)
        {
            return;
        }

        if (_stoneDelay < Core.Now)
        {
            SpawnStone();
        }

        if (_gazeDelay < Core.Now)
        {
            DoGaze();
        }
    }

    public void DoGaze()
    {
        var target = FindRandomMedusaTarget();
        var map = Map;

        if (map == null || target == null)
        {
            return;
        }

        if (target is BaseCreature tc && tc.SummonMaster != this || CanBeHarmful(target))
        {
            if (CheckBlockGaze(target))
            {
                // ServUO: 1112600 "Your lenses crumble..." / 1112599 "Your Gorgon Lens deflect Medusa's petrifying
                // gaze!" - unreachable without the lens (D-88).
            }
            else
            {
                BaseCreature clone = new MedusaClone(target);

                var validLocation = false;
                var loc = Location;

                for (var j = 0; !validLocation && j < 10; ++j)
                {
                    var x = X + Utility.Random(10) - 1;
                    var y = Y + Utility.Random(10) - 1;
                    var z = map.GetAverageZ(x, y);

                    if (validLocation = map.CanFit(x, y, Z, 16, false, false))
                    {
                        loc = new Point3D(x, y, Z);
                    }
                    else if (validLocation = map.CanFit(x, y, z, 16, false, false))
                    {
                        loc = new Point3D(x, y, z);
                    }
                }

                Effects.SendLocationEffect(loc, target.Map, 0x37B9, 10, 5);
                clone.Frozen = clone.Blessed = true;
                clone.SolidHueOverride = 761;

                target.Frozen = target.Blessed = true;
                target.SolidHueOverride = 761;

                // clone.MoveToWorld(loc, target.Map);
                Summon(clone, false, this, loc, 0, TimeSpan.FromMinutes(90));

                if (target is BaseCreature pet && !pet.Summoned && pet.GetMaster() != null)
                {
                    pet.GetMaster().SendLocalizedMessage(1113281, default, 43); // Your pet has been petrified!
                }
                else
                {
                    target.SendLocalizedMessage(1112768); // You have been turned to stone!!!
                }

                new GazeTimer(target, clone, this, Utility.RandomMinMax(5, 10)).Start();
                _gazeDelay = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(45, 75));

                _helpers.Add(clone);
                _turnedToStone.Add(target);

                (target as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.MedusaStone, 1153790, 1153825));
                return;
            }
        }

        _gazeDelay = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(25, 65));
    }

    public void SpawnStone()
    {
        DefragHelpers();

        var map = Map;

        if (map == null)
        {
            return;
        }

        var stones = 0;

        foreach (var m in _helpers)
        {
            if (m is not MedusaClone)
            {
                ++stones;
            }
        }

        if (stones >= 5)
        {
            return;
        }

        var stone = GetRandomStoneMonster();

        var validLocation = false;
        var loc = Location;

        for (var j = 0; !validLocation && j < 10; ++j)
        {
            var x = X + Utility.Random(10) - 1;
            var y = Y + Utility.Random(10) - 1;
            var z = map.GetAverageZ(x, y);

            if (validLocation = map.CanFit(x, y, Z, 16, false, false))
            {
                loc = new Point3D(x, y, Z);
            }
            else if (validLocation = map.CanFit(x, y, z, 16, false, false))
            {
                loc = new Point3D(x, y, z);
            }
        }

        Summon(stone, false, this, loc, 0, TimeSpan.FromMinutes(90));
        // stone.MoveToWorld(loc, map);
        stone.Frozen = stone.Blessed = true;
        stone.SolidHueOverride = 761;
        stone.Combatant = null;

        _helpers.Add(stone);

        _stoneDelay = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 150));
    }

    private void DefragHelpers()
    {
        var toDelete = new List<Mobile>();

        foreach (var m in _helpers)
        {
            if (m == null)
            {
                continue;
            }

            if (!m.Alive || m.Deleted)
            {
                toDelete.Add(m);
            }
        }

        foreach (var m in toDelete)
        {
            if (_helpers.Contains(m))
            {
                _helpers.Remove(m);
            }
        }
    }

    private BaseCreature GetRandomStoneMonster()
    {
        switch (Utility.Random(6))
        {
            default:
            case 0: return new OphidianWarrior();
            case 1: return new OphidianArchmage();
            case 2: return new WailingBanshee();
            case 3: return new OgreLord();
            case 4: return new Dragon();
            case 5: return new UndeadGargoyle();
        }
    }

    public void ReleaseStoneMonster()
    {
        var stones = new List<Mobile>();

        foreach (var mob in _helpers)
        {
            if (mob is not MedusaClone && mob.Alive)
            {
                stones.Add(mob);
            }
        }

        if (stones.Count == 0)
        {
            return;
        }

        var m = stones[Utility.Random(stones.Count)];

        if (m != null)
        {
            m.Frozen = m.Blessed = false;
            m.SolidHueOverride = -1;
            Mobile closest = null;
            var dist = 12;

            _helpers.Remove(m);

            foreach (var targ in m.GetMobilesInRange(12))
            {
                if (targ != null && targ.Player)
                {
                    targ.SendLocalizedMessage(1112767, default, 43); // Medusa releases one of the petrified creatures!!
                    targ.Combatant = targ;
                }

                if (targ is PlayerMobile || targ is BaseCreature bc && bc.GetMaster() is PlayerMobile)
                {
                    var d = (int)m.GetDistanceToSqrt(targ.Location);

                    if (d < dist)
                    {
                        dist = d;
                        closest = targ;
                    }
                }
            }

            if (closest != null)
            {
                m.Combatant = closest;
            }
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 8);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.025)
        {
            c.DropItem(new MedusaStatue());
        }
    }

    public override void OnAfterDelete()
    {
        foreach (var m in _helpers)
        {
            if (m != null && !m.Deleted)
            {
                m.Delete();
            }
        }

        base.OnAfterDelete();
    }

    // ServUO's Deserialize keeps only the helpers that are non-null and alive.
    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _helpers.RemoveAll(m => m == null || !m.Alive);
    }

    public class GazeTimer : Timer
    {
        private readonly Mobile _target;
        private readonly Mobile _clone;
        private readonly Medusa _medusa;
        private int _count;

        public GazeTimer(Mobile m, Mobile mc, Medusa medusa, int duration)
            : base(TimeSpan.FromSeconds(duration), TimeSpan.FromSeconds(duration))
        {
            _target = m;
            _clone = mc;
            _medusa = medusa;
            _count = 0;
        }

        protected override void OnTick()
        {
            ++_count;

            if (_count == 1 && _target != null)
            {
                _target.Frozen = false;
                _target.SolidHueOverride = -1;
                _target.Blessed = false;
                _medusa.RemoveAffectedMobiles(_target);

                if (_target is BaseCreature pet && !pet.Summoned && pet.GetMaster() != null)
                {
                    pet.GetMaster().SendLocalizedMessage(1113285, default, 43); // Beware! A statue of your pet has been created!
                }

                (_target as PlayerMobile)?.RemoveBuff(BuffIcon.MedusaStone);
            }
            else if (_count == 2 && _clone != null)
            {
                _clone.SolidHueOverride = -1;
                _clone.Frozen = _clone.Blessed = false;
                var dist = 12;
                Mobile closest = null;

                foreach (var m in _clone.GetMobilesInRange(12))
                {
                    var d = (int)_clone.GetDistanceToSqrt(m.Location);

                    if (m != null && m is PlayerMobile || m is BaseCreature bc && bc.GetMaster() is PlayerMobile)
                    {
                        if (m.NetState != null)
                        {
                            m.NetState.SendRemoveEntity(_clone.Serial);
                            m.NetState.SendMobileIncoming(m, _clone);
                            m.SendLocalizedMessage(1112767); // Medusa releases one of the petrified creatures!!
                        }

                        if (d < dist)
                        {
                            dist = d;
                            closest = m;
                        }
                    }
                }

                if (closest != null)
                {
                    _clone.Combatant = closest;
                }
            }
            else
            {
                Stop();
            }
        }
    }
}

// ServUO: the same file. A petrified copy of Medusa's gaze target: the target's body, stats, skills, name and a
// non-movable copy of each worn item, summoned frozen and blessed beside her and released by the GazeTimer. Never
// saved alive: ServUO's Deserialize deletes it, and so does [AfterDeserialization] here.
[SerializationGenerator(0, false)]
public partial class MedusaClone : BaseCreature
{
    public MedusaClone(Mobile m) : base(AIType.AI_Melee)
    {
        SolidHueOverride = 33;
        Clone(m);
    }

    public override bool DeleteCorpseOnDeath => true;
    public override bool ReacquireOnMovement => true;
    public override bool AlwaysMurderer => !Frozen;

    public void Clone(Mobile m)
    {
        if (m == null)
        {
            Delete();
            return;
        }

        Body = m.Body;

        Str = m.Str;
        Dex = m.Dex;
        Int = m.Int;

        Hits = m.HitsMax;

        Hue = m.Hue;
        Female = m.Female;

        Name = m.Name;
        NameHue = m.NameHue;

        Title = m.Title;
        Kills = m.Kills;

        HairItemID = m.HairItemID;
        HairHue = m.HairHue;

        FacialHairItemID = m.FacialHairItemID;
        FacialHairHue = m.FacialHairHue;

        BaseSoundID = m.BaseSoundID;

        for (var i = 0; i < m.Skills.Length; ++i)
        {
            Skills[i].Base = m.Skills[i].Base;
            Skills[i].Cap = m.Skills[i].Cap;
        }

        for (var i = 0; i < m.Items.Count; i++)
        {
            if (m.Items[i].Layer != Layer.Backpack && m.Items[i].Layer != Layer.Mount && m.Items[i].Layer != Layer.Bank)
            {
                AddItem(CloneItem(m.Items[i]));
            }
        }
    }

    public Item CloneItem(Item item)
    {
        var cloned = new Item(item.ItemID);
        cloned.Layer = item.Layer;
        cloned.Name = item.Name;
        cloned.Hue = item.Hue;
        cloned.Weight = item.Weight;
        cloned.Movable = false;

        return cloned;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Frozen)
        {
            DisplayPaperdollTo(from);
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }

    // ServUO: IFreezable.OnRequestedAnimation(Mobile from) sends UpdateStatueAnimation(this, 1, 31, 5) while frozen;
    // nothing in pinned asks a creature for it (D-89).

    public override void OnDelete()
    {
        Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 10, 15, 5042);

        base.OnDelete();
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Delete();
    }
}

// ServUO: the same file, namespace Server.Commands. [addclone: a frozen, blessed MedusaClone of the caller, for a Seer.
public static class AddCloneCommands
{
    public static void Initialize()
    {
        CommandSystem.Register("addclone", AccessLevel.Seer, AddClone_OnCommand);
    }

    [Description("")]
    public static void AddClone_OnCommand(CommandEventArgs e)
    {
        BaseCreature clone = new MedusaClone(e.Mobile);
        clone.Frozen = clone.Blessed = true;
        clone.MoveToWorld(e.Mobile.Location, e.Mobile.Map);
    }
}
