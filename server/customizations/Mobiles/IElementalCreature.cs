// ServUO: Mobiles/MobileInterfaces.cs, IElementalCreature (cc-P129). One extracted type per file (port recipe).
// IAuraCreature from the same file is NOT ported: pinned's BaseCreature.AuraEffect(Mobile) virtual is the same hook.
// IRepairableMobile is not ported either: pinned's Repair targets Golem by type and has no interface to implement.

namespace Server.Mobiles;

public interface IElementalCreature
{
    ElementType ElementType { get; }
}
