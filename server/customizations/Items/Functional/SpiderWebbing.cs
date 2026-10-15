// cc-P121: forced leaf dependency of Pet Training's Webbing ability (SpecialAbility.cs). Ported from ServUO
// Scripts/Items/Functional/SpiderWebbing.cs. The web a spider throws: it holds its victim (and anyone not a monster
// who walks into it) for up to ten seconds, then falls apart. ServUO deletes it on load; [AfterDeserialization] here.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SpiderWebbing : Item
{
    private static readonly List<Mobile> _webVictims = new();

    private Timer _timer;

    public SpiderWebbing(Mobile m) : base(0xEE3 + Utility.Random(4))
    {
        Movable = false;

        BeginWebbing(m);

        _timer = new InternalTimer(this, m);
        _timer.Start();
    }

    public override bool BlocksFit => true;

    public static void BeginWebbing(Mobile m)
    {
        m.RevealingAction();
        m.Frozen = true;
        m.SendLocalizedMessage(1113247); // You are wrapped in spider webbing and cannot move!
        _webVictims.Add(m);
        (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.Webbing, 1153789, 1153825));
    }

    public static bool IsTrapped(Mobile m) => _webVictims.Contains(m);

    public static void RemoveEffects(Mobile m)
    {
        m.Frozen = false;
        m.SendLocalizedMessage(1113248); // You escape the spider's web!
        (m as PlayerMobile)?.RemoveBuff(BuffIcon.Webbing);
        _webVictims.Remove(m);
    }

    public override void OnDelete()
    {
        _timer?.Stop();
        _timer = null;

        // ServUO frees every victim of every web here, not only this web's (Items/Functional/SpiderWebbing.cs:56-72).
        foreach (var m in _webVictims.ToArray())
        {
            RemoveEffects(m);
        }

        _webVictims.Clear();

        base.OnDelete();
    }

    public override bool OnMoveOver(Mobile m)
    {
        if (m is BaseCreature { IsMonster: true })
        {
            return true;
        }

        if (m.AccessLevel == AccessLevel.Player && m.Alive)
        {
            BeginWebbing(m);
            m.PlaySound(0x204);
            m.FixedEffect(0x376A, 10, 16);
        }

        return true;
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization() => Delete();

    private class InternalTimer : Timer
    {
        private readonly Mobile _target;
        private readonly Item _item;
        private int _ticks;

        public InternalTimer(Item item, Mobile target) : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
        {
            _item = item;
            _target = target;
            _ticks = 10;
        }

        protected override void OnTick()
        {
            _ticks--;

            if (!_target.Alive || _ticks == 0)
            {
                _item.Delete();
            }
        }
    }
}
