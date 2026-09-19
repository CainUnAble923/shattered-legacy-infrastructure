// ServUO: Services/Revamped Dungeons/DespiseRevamped/AI.cs (CC4 Despise). Two AIs, duplicated statement for
// statement as ServUO has them (a melee and a mage flavour of the same possession logic).
//
// While a creature is possessed (Orb != null and Controlled) the orb's Aggression decides what Obey does:
// Defensive engages only mobiles already fighting the creature or its master near the anchor; Aggressive
// hunts any DespiseCreature of the other alignment, or any DespiseBoss, near the anchor. Following the
// master is redirected to following the anchor within the leash length, and the pet context menu and
// speech commands are suppressed because the creature is not Commandable.
//
// Conversion notes:
//   ModernUO kept only the Mobile overload of WalkMobileRange (AIMovement.cs:375); RunUO's IPoint3D
//     overload, which ServUO still has and this AI needs for an Item anchor, is reproduced as WalkToPoint.
//   GetContextMenuEntries takes a ref PooledRefList; DebugSay lives on BaseAI, not the creature;
//     IPooledEnumerable.Free is gone.

using Server.Collections;
using Server.ContextMenus;
using Server.Mobiles;

namespace Server.Engines.Despise;

public class DespiseMeleeAI : MeleeAI
{
    private readonly DespiseCreature _creature;
    private long _nextAggressorCheck;

    public DespiseMeleeAI(DespiseCreature m) : base(m) => _creature = m;

    public override bool Obey()
    {
        if (_creature.Orb == null || !_creature.Controlled)
        {
            return base.Obey();
        }

        switch (_creature.Orb.Aggression)
        {
            default:
                {
                    _creature.ControlOrder = OrderType.Follow;
                    DoOrderFollow();
                }
                break;
            case Aggression.Defensive:
                {
                    if (_creature.Combatant != null)
                    {
                        if (_creature.ControlOrder == OrderType.Follow)
                        {
                            _creature.ControlOrder = OrderType.Attack;
                            Action = ActionType.Combat;
                        }

                        break;
                    }

                    if (_nextAggressorCheck <= Core.TickCount)
                    {
                        var closest = DespiseAIHelper.FindAggressorNearAnchor(_creature);

                        if (closest != null)
                        {
                            _creature.ControlTarget = closest;
                            _creature.ControlOrder = OrderType.Attack;
                            _creature.Combatant = closest;
                            DebugSay("But -that- is not dead. Here we go again...");

                            Action = ActionType.Combat;
                        }

                        _nextAggressorCheck = Core.TickCount + 1000;
                    }
                }
                break;
            case Aggression.Aggressive:
                {
                    if (_creature.Combatant != null)
                    {
                        if (_creature.ControlOrder == OrderType.Follow)
                        {
                            _creature.ControlOrder = OrderType.Attack;
                            Action = ActionType.Combat;
                        }

                        break;
                    }

                    if (AcquireFocusMob(_creature.RangePerception, _creature.FightMode, false, false, true))
                    {
                        if (_creature.FocusMob == _creature.ControlMaster)
                        {
                            break;
                        }

                        if (_creature.Debug)
                        {
                            DebugSay($"I have detected {_creature.FocusMob.Name}, attacking");
                        }

                        _creature.ControlOrder = OrderType.Attack;
                        _creature.Combatant = _creature.FocusMob;

                        Action = ActionType.Combat;
                    }
                }
                break;
        }

        if (_creature.Combatant == null)
        {
            _creature.ControlOrder = OrderType.Follow;
            _creature.ControlTarget = _creature.ControlMaster;
            Action = ActionType.Guard;
            DoOrderFollow();
        }

        Think();
        return true;
    }

    public override bool DoOrderFollow()
    {
        _creature.Orb?.InvalidateHue();

        return base.DoOrderFollow();
    }

    public override bool AcquireFocusMob(int iRange, FightMode acqType, bool bPlayerOnly, bool bFacFriend, bool bFacFoe)
    {
        if (_creature.Orb == null || _creature.ControlMaster == null)
        {
            return base.AcquireFocusMob(iRange, acqType, bPlayerOnly, bFacFriend, bFacFoe);
        }

        if (_creature.Orb.Aggression != Aggression.Aggressive)
        {
            return false;
        }

        if (Core.TickCount - _creature.NextReacquireTime < 0)
        {
            _creature.FocusMob = null;
            return false;
        }

        _creature.NextReacquireTime = Core.TickCount + (int)_creature.ReacquireDelay.TotalMilliseconds;

        var focus = DespiseAIHelper.GetFocus(_creature, _creature.Orb.Anchor ?? _creature, _creature.RangePerception);

        if (focus != null)
        {
            _creature.FocusMob = focus;
            return true;
        }

        return false;
    }

    public override bool WalkMobileRange(Mobile m, int iSteps, bool run, int iWantDistMin, int iWantDistMax)
    {
        if (_creature.Orb == null || _creature.ControlMaster == null)
        {
            return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
        }

        var range = _creature.GetLeashLength();

        if (m == _creature.ControlMaster)
        {
            var p = _creature.Orb.GetAnchorActual();

            if (p == null)
            {
                return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
            }

            if (_creature.InRange(p, range))
            {
                return false;
            }

            if (p is Mobile anchor)
            {
                return base.WalkMobileRange(anchor, iSteps, run, range, range);
            }

            return DespiseAIHelper.WalkToPoint(this, p, iSteps, run, range, range);
        }

        return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
    }
}

public class DespiseMageAI : MageAI
{
    private readonly DespiseCreature _creature;
    private long _nextAggressorCheck;

    public DespiseMageAI(DespiseCreature m) : base(m) => _creature = m;

    public override bool Obey()
    {
        if (_creature.Orb == null || !_creature.Controlled)
        {
            return base.Obey();
        }

        switch (_creature.Orb.Aggression)
        {
            default:
                {
                    _creature.ControlOrder = OrderType.Follow;
                    DoOrderFollow();
                }
                break;
            case Aggression.Defensive:
                {
                    if (_creature.Combatant != null)
                    {
                        if (_creature.ControlOrder == OrderType.Follow)
                        {
                            _creature.ControlOrder = OrderType.Attack;
                            Action = ActionType.Combat;
                        }

                        break;
                    }

                    if (_nextAggressorCheck <= Core.TickCount)
                    {
                        var closest = DespiseAIHelper.FindAggressorNearAnchor(_creature);

                        if (closest != null)
                        {
                            _creature.ControlTarget = closest;
                            _creature.ControlOrder = OrderType.Attack;
                            _creature.Combatant = closest;
                            DebugSay("But -that- is not dead. Here we go again...");

                            Action = ActionType.Combat;
                        }

                        _nextAggressorCheck = Core.TickCount + 1000;
                    }
                }
                break;
            case Aggression.Aggressive:
                {
                    if (_creature.Combatant != null)
                    {
                        if (_creature.ControlOrder == OrderType.Follow)
                        {
                            _creature.ControlOrder = OrderType.Attack;
                            Action = ActionType.Combat;
                        }

                        break;
                    }

                    if (AcquireFocusMob(_creature.RangePerception, _creature.FightMode, false, false, true))
                    {
                        if (_creature.FocusMob == _creature.ControlMaster)
                        {
                            break;
                        }

                        if (_creature.Debug)
                        {
                            DebugSay($"I have detected {_creature.FocusMob.Name}, attacking");
                        }

                        _creature.ControlOrder = OrderType.Attack;
                        _creature.Combatant = _creature.FocusMob;

                        Action = ActionType.Combat;
                    }
                }
                break;
        }

        if (_creature.Combatant == null)
        {
            _creature.ControlOrder = OrderType.Follow;
            _creature.ControlTarget = _creature.ControlMaster;
            Action = ActionType.Guard;
            DoOrderFollow();
        }

        Think();
        return true;
    }

    public override bool DoOrderFollow()
    {
        _creature.Orb?.InvalidateHue();

        return base.DoOrderFollow();
    }

    public override bool AcquireFocusMob(int iRange, FightMode acqType, bool bPlayerOnly, bool bFacFriend, bool bFacFoe)
    {
        if (_creature.Orb == null || _creature.ControlMaster == null)
        {
            return base.AcquireFocusMob(iRange, acqType, bPlayerOnly, bFacFriend, bFacFoe);
        }

        if (_creature.Orb.Aggression != Aggression.Aggressive)
        {
            return false;
        }

        if (Core.TickCount - _creature.NextReacquireTime < 0)
        {
            _creature.FocusMob = null;
            return false;
        }

        _creature.NextReacquireTime = Core.TickCount + (int)_creature.ReacquireDelay.TotalMilliseconds;

        var focus = DespiseAIHelper.GetFocus(_creature, _creature.Orb.Anchor ?? _creature, _creature.RangePerception);

        if (focus != null)
        {
            _creature.FocusMob = focus;
            return true;
        }

        return false;
    }

    public override bool WalkMobileRange(Mobile m, int iSteps, bool run, int iWantDistMin, int iWantDistMax)
    {
        if (_creature.Orb == null || _creature.ControlMaster == null)
        {
            return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
        }

        var range = _creature.GetLeashLength();

        if (m == _creature.ControlMaster)
        {
            var p = _creature.Orb.GetAnchorActual();

            if (p == null)
            {
                return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
            }

            if (_creature.InRange(p, range))
            {
                return false;
            }

            if (p is Mobile anchor)
            {
                return base.WalkMobileRange(anchor, iSteps, run, range, range);
            }

            return DespiseAIHelper.WalkToPoint(this, p, iSteps, run, range, range);
        }

        return base.WalkMobileRange(m, iSteps, run, iWantDistMin, iWantDistMax);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
    }
}

// The three bodies ServUO writes out twice, once per AI. Same statements, one copy.
public static class DespiseAIHelper
{
    // Defensive: the nearest mobile near the anchor that is fighting the creature or its master.
    // ServUO's distance bookkeeping (comparing the previous closest's distance rather than the
    // candidate's) is reproduced as written.
    public static Mobile FindAggressorNearAnchor(DespiseCreature creature)
    {
        var p = creature.Orb?.GetAnchorActual();
        var map = creature.Map;

        if (p == null || map == null || map == Map.Internal)
        {
            return null;
        }

        double range = creature.RangePerception;
        Mobile closest = null;

        foreach (var m in map.GetMobilesInRange(new Point3D(p), (int)range))
        {
            if (m.Combatant == creature || m.Combatant == creature.ControlMaster)
            {
                var dist = closest == null ? range : closest.GetDistanceToSqrt(creature);

                if (closest == null || dist < range)
                {
                    range = dist;
                    closest = m;
                }
            }
        }

        return closest;
    }

    // Aggressive: the nearest wild DespiseCreature of the other alignment, or any DespiseBoss, near the anchor.
    public static Mobile GetFocus(DespiseCreature creature, IPoint3D p, int range)
    {
        var map = creature.Map;

        if (map == null || map == Map.Internal)
        {
            return null;
        }

        Mobile focus = null;
        var dist = range;

        foreach (var m in map.GetMobilesInRange(new Point3D(p), range))
        {
            if (m is DespiseCreature || m is DespiseBoss)
            {
                var dc = m as DespiseCreature;

                if (m is DespiseBoss || dc != null && (dc.Orb == null && !dc.Controlled || dc.Alignment != creature.Alignment))
                {
                    var distance = (int)creature.GetDistanceToSqrt(m);

                    if (focus == null || distance < dist)
                    {
                        focus = m;
                        dist = distance;
                    }
                }
            }
        }

        return focus;
    }

    // RunUO's WalkMobileRange(IPoint3D, ...) as ServUO still has it, for an anchor that is an Item. It is
    // only ever called from outside the wanted band (the callers return before it when already in range),
    // so the "move away" arm RunUO carried is not needed; stepping closer is the whole job.
    public static bool WalkToPoint(BaseAI ai, IPoint3D p, int iSteps, bool run, int iWantDistMin, int iWantDistMax)
    {
        var mobile = ai.Mobile;

        if (mobile.Deleted || mobile.DisallowAllMoves || p == null)
        {
            return false;
        }

        var target = new Point3D(p);

        for (var i = 0; i < iSteps; i++)
        {
            var dist = (int)mobile.GetDistanceToSqrt(target);

            if (dist >= iWantDistMin && dist <= iWantDistMax)
            {
                return true;
            }

            if (dist < iWantDistMin)
            {
                return false;
            }

            if (!ai.DoMove(mobile.GetDirectionTo(target, run && dist > 5), true))
            {
                return false;
            }
        }

        var finalDist = mobile.GetDistanceToSqrt(target);

        return finalDist >= iWantDistMin && finalDist <= iWantDistMax;
    }
}
