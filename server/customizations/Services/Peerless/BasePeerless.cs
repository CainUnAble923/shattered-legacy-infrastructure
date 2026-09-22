// ServUO: Services/Peerless/BasePeerless.cs (CC6 batch 8; Q-052 answered yes). The creature base of the seven
// Mondain's Legacy peerless bosses and, through BaseSABoss, of the Stygian Abyss bosses (Medusa, the Slasher of
// Veils, the Stygian Dragon, Niporailem).
//
// Ported WITHOUT the altar. B14 Instanced Peerless stays WON'T: there is no PeerlessAltar, no instancing, no
// teleporters, and the keys (PeerlessKey and its three, P9) are consumed by nothing. Every boss on this base is a
// walk-up fight. That is one deliberate, accepted difference for the whole batch, D-79, recorded once.
//
// The altar sat at SEVEN sites in ServUO's 367 lines (three references to the PeerlessAltar type, eleven lines that
// touch m_Altar; notes/cc6-batch8.md section 2 has each line):
//   field + GameMaster property (lines 10-23)          -> gone; there is nothing to point at
//   OnDeath -> m_Altar.OnPeerlessDeath() (137-138)     -> gone
//   Serialize / Deserialize (154 / 163)                -> gone. Version 0 carries nothing; a future altar is a new
//                                                         field and a version bump, an ordinary migration
//   AllHelpersDead (207-208)                           -> the altar-less arm ServUO already had: true
//   SpawnHelper -> m_Altar.AddHelper(helper) (253-254) -> gone; helpers are placed and not tracked
//
// Other members that changed, each proved against D:\UO\ModernUO-pinned before it was numbered:
//   DropPrimer / SkillMasteryPrimer  pinned has no Skill Masteries at all (no type, no "mastery" in UOContent
//                                    outside a buff icon, a ConPVP ruleset and a book). The virtual stays so
//                                    descendants' overrides compile; the OnDeath branch is dropped (D-80).
//   MondainsLegacy.DropPeerlessMinor pinned's MondainsLegacy has no such method, but its Artifacts list is the
//                                    same 21 types as ServUO's m_Artifacts; the call is reproduced inline.
//   CanBeParagon => false            no such virtual in pinned. Paragon.CheckConvert excludes by a type list
//                                    (BaseChampion, Harrower, BaseVendor, BaseEscortable, Clone) and only on
//                                    Paragon.Maps = { Ilshenar } (D-81; none of this batch spawns there).
//   GetSpawnPosition(int)            no such method on pinned BaseCreature; ServUO's static body is reproduced.
//   the constructor                  ServUO's is (ai, mode, rangePerception, rangeFight, activeSpeed, passiveSpeed).
//                                    Every descendant passes 10 (ServUO rewrites it to 16) and dead speed literals,
//                                    so this one mirrors pinned BaseCreature's own signature (Q-008, the recipe).
//
// Final parent: BaseCreature. The B5 carrier regex was run over this file, BaseSABosses.cs and all eleven ServUO
// descendants (transitively) and hit nothing; the walk is in the note.
//
// Two ServUO quirks kept, bug-list section 3's kind: m_CurrentWave is never serialized, so a boss reloaded from a
// save has 0 waves left; and the FireRing gate reads m_NextFireRing > Core.TickCount, which is the cooldown
// backwards. No descendant in this batch turns HasFireRing on.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BasePeerless : BaseCreature
{
    private long _nextFireRing = Core.TickCount;
    private int _currentWave;

    public BasePeerless(
        AIType aiType,
        FightMode fightMode = FightMode.Closest,
        int rangePerception = DefaultRangePerception,
        int rangeFight = 1
    ) : base(aiType, fightMode, rangePerception, rangeFight)
    {
        _nextFireRing = Core.TickCount + 10000;
        _currentWave = MaxHelpersWaves;
    }

    // Read by nothing here: pinned has no SkillMasteryPrimer to drop (D-80). Kept so BaseSABoss's override compiles
    // and so a Skill Masteries port turns the branch back on in one place.
    public virtual bool DropPrimer => Core.TOL;
    public virtual bool GiveMLSpecial => true;

    public override bool Unprovokable => true;
    public virtual double ChangeCombatant => 0.3;

    public override void OnThink()
    {
        base.OnThink();

        if (HasFireRing && Combatant != null && Alive && Hits > 0.8 * HitsMax && _nextFireRing > Core.TickCount &&
            Utility.RandomDouble() < FireRingChance)
        {
            FireRing();
        }

        if (CanSpawnHelpers && Combatant != null && Alive && CanSpawnWave())
        {
            SpawnHelpers();
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        // ServUO: if (DropPrimer) { SkillMasteryPrimer.GetRandom() to a random looting-rights holder }. D-80.

        if (GivesMLMinorArtifact && 0.5 > Utility.RandomDouble())
        {
            // ServUO: MondainsLegacy.DropPeerlessMinor(c) - one of the ML minor artifacts onto the corpse.
            c.DropItem(Loot.Construct(MondainsLegacy.Artifacts.RandomElement()));
        }

        if (GiveMLSpecial)
        {
            if (Utility.RandomDouble() < 0.10)
            {
                c.DropItem(new HumanFeyLeggings());
            }

            if (Utility.RandomDouble() < 0.025)
            {
                c.DropItem(new CrimsonCincture());
            }

            if (0.05 > Utility.RandomDouble())
            {
                switch (Utility.Random(32))
                {
                    case 0: c.DropItem(new AssassinChest()); break;
                    case 1: c.DropItem(new AssassinArms()); break;
                    case 2: c.DropItem(new AssassinLegs()); break;
                    case 3: c.DropItem(new AssassinGloves()); break;
                    case 4: c.DropItem(new DeathChest()); break;
                    case 5: c.DropItem(new DeathArms()); break;
                    case 6: c.DropItem(new DeathLegs()); break;
                    case 7: c.DropItem(new DeathBoneHelm()); break;
                    case 8: c.DropItem(new DeathGloves()); break;
                    case 9: c.DropItem(new MyrmidonArms()); break;
                    case 10: c.DropItem(new MyrmidonLegs()); break;
                    case 11: c.DropItem(new MyrmidonGorget()); break;
                    case 12: c.DropItem(new MyrmidonChest()); break;
                    case 13: c.DropItem(new LeafweaveGloves()); break;
                    case 14: c.DropItem(new LeafweaveLegs()); break;
                    case 15: c.DropItem(new LeafweavePauldrons()); break;
                    case 16: c.DropItem(new PaladinGloves()); break;
                    case 17: c.DropItem(new PaladinGorget()); break;
                    case 18: c.DropItem(new PaladinArms()); break;
                    case 19: c.DropItem(new PaladinLegs()); break;
                    case 20: c.DropItem(new PaladinHelm()); break;
                    case 21: c.DropItem(new PaladinChest()); break;
                    case 22: c.DropItem(new HunterArms()); break;
                    case 23: c.DropItem(new HunterGloves()); break;
                    case 24: c.DropItem(new HunterLegs()); break;
                    case 25: c.DropItem(new HunterChest()); break;
                    case 26: c.DropItem(new GreymistArms()); break;
                    case 27: c.DropItem(new GreymistGloves()); break;
                    case 28: c.DropItem(new GreymistLegs()); break;
                    case 29: c.DropItem(new MalekisHonor()); break;
                    case 30: c.DropItem(new Feathernock()); break;
                    case 31: c.DropItem(new Swiftflight()); break;
                }
            }
        }

        // ServUO: if (m_Altar != null) m_Altar.OnPeerlessDeath(); D-79.
    }

    #region Helpers

    public virtual bool CanSpawnHelpers => false;
    public virtual int MaxHelpersWaves => 0;
    public virtual double SpawnHelpersChance => 0.05;

    public int CurrentWave
    {
        get => _currentWave;
        set => _currentWave = value;
    }

    // ServUO: m_Altar != null ? m_Altar.AllHelpersDead() : true. The altar-less arm.
    public bool AllHelpersDead => true;

    public virtual bool CanSpawnWave()
    {
        if (MaxHelpersWaves > 0 && _currentWave > 0)
        {
            var hits = Hits / (double)HitsMax;
            var waves = _currentWave / (double)(MaxHelpersWaves + 1);

            if (hits < waves && Utility.RandomDouble() < SpawnHelpersChance)
            {
                _currentWave -= 1;
                return true;
            }
        }

        return false;
    }

    public virtual void SpawnHelpers()
    {
    }

    public void SpawnHelper(BaseCreature helper, int range)
    {
        SpawnHelper(helper, GetSpawnPosition(range));
    }

    public void SpawnHelper(BaseCreature helper, int x, int y, int z)
    {
        SpawnHelper(helper, new Point3D(x, y, z));
    }

    public void SpawnHelper(BaseCreature helper, Point3D location)
    {
        if (helper == null)
        {
            return;
        }

        helper.Home = location;
        helper.RangeHome = 4;

        // ServUO: if (m_Altar != null) m_Altar.AddHelper(helper); D-79.

        helper.MoveToWorld(location, Map);
    }

    // ServUO BaseCreature.GetSpawnPosition(int) / (Point3D, Map, int) (BaseCreature.cs:7001-7029); pinned has neither.
    public virtual Point3D GetSpawnPosition(int range) => GetSpawnPosition(Location, Map, range);

    public static Point3D GetSpawnPosition(Point3D from, Map map, int range)
    {
        if (map == null)
        {
            return from;
        }

        for (var i = 0; i < 10; i++)
        {
            var x = from.X + Utility.RandomMinMax(-range, range);
            var y = from.Y + Utility.RandomMinMax(-range, range);
            var z = map.GetAverageZ(x, y);

            var p = new Point3D(x, y, from.Z);

            if (map.CanSpawnMobile(p) && map.LineOfSight(from, p))
            {
                return p;
            }

            p = new Point3D(x, y, z);

            if (map.CanSpawnMobile(p) && map.LineOfSight(from, p))
            {
                return p;
            }
        }

        return from;
    }

    #endregion

    public virtual void PackResources(int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            switch (Utility.Random(6))
            {
                case 0: PackItem(new Blight()); break;
                case 1: PackItem(new Scourge()); break;
                case 2: PackItem(new Taint()); break;
                case 3: PackItem(new Putrefaction()); break;
                case 4: PackItem(new Corruption()); break;
                case 5: PackItem(new Muculent()); break;
            }
        }
    }

    public virtual void PackItems(Item item, int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            PackItem(item);
        }
    }

    public virtual void PackTalismans(int amount)
    {
        var count = Utility.Random(amount);

        for (var i = 0; i < count; i++)
        {
            PackItem(Loot.RandomTalisman());
        }
    }

    #region Fire Ring

    private static readonly int[] _north =
    {
        -1, -1,
        1, -1,
        -1, 2,
        1, 2
    };

    private static readonly int[] _east =
    {
        -1, 0,
        2, 0
    };

    public virtual bool HasFireRing => false;
    public virtual double FireRingChance => 1.0;

    public virtual void FireRing()
    {
        for (var i = 0; i < _north.Length; i += 2)
        {
            var p = Location;

            p.X += _north[i];
            p.Y += _north[i + 1];

            IPoint3D po = p;

            SpellHelper.GetSurfaceTop(ref po);

            Effects.SendLocationEffect(new Point3D(po), Map, 0x3E27, 50);
        }

        for (var i = 0; i < _east.Length; i += 2)
        {
            var p = Location;

            p.X += _east[i];
            p.Y += _east[i + 1];

            IPoint3D po = p;

            SpellHelper.GetSurfaceTop(ref po);

            Effects.SendLocationEffect(new Point3D(po), Map, 0x3E31, 50);
        }

        _nextFireRing = Core.TickCount + 10000;
    }

    #endregion
}
