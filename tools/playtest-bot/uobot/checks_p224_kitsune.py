"""cc-P224: P138-3 and P138-5, the Kitsune Ears and Tail after cc-P138 (humans and elves of either sex wear it; gargoyles
are refused). Code under test: server/customizations/KitsuneEarsTail.cs."""
from .checks import check, give, try_equip

ITEM_ID = 0x3C3C          # KitsuneEarsTail.cs:36 ItemIdValue
LAYER = 0x12              # KitsuneEarsTail.cs:39 WornLayer = Layer.Earrings (18)
REFUSAL = "Only humans and elves can wear this."   # KitsuneEarsTail.cs:52 RefusalForGargoyles, sent at :58


@check("P138-3", "A male human wears the Kitsune Ears and Tail (no 'Only females' refusal), on the earrings layer",
       kind="BOT-PARTIAL", left="P138-3: on his paperdoll the ears sit on the crown and the tail leaves the lower back")
def p138_3(ctx):
    # KitsuneEarsTail.cs:55-63: OnEquip refuses only Race.Gargoyle; anyone else goes to base.OnEquip. The worn item
    # arrives as a 0x2E (equip update) or inside the 0x78 with layer 0x12 and graphic 0x3C3C.
    a = ctx.ensure_admin()
    c = ctx.fresh(race="human", female=False, label="p138m")
    try:
        s = give(a, c, "KitsuneEarsTail")
        worn, texts, it = try_equip(c, s, 2.5)
        refused = [t for t in texts if t.startswith("Only ")]
        ok = worn and it.get("layer") == LAYER and it.get("graphic") == ITEM_ID and not refused
        return ok, "male human: worn %s on layer %s (want 0x12) graphic 0x%X (want 0x3C3C); refusals %s; messages %s" % (
            worn, it.get("layer"), it.get("graphic") or 0, refused or "none", texts[-3:])
    finally:
        ctx.done(c)


@check("P138-5", "A gargoyle is refused: 'Only humans and elves can wear this.' and the item stays in the pack")
def p138_5(ctx):
    # KitsuneEarsTail.cs:55-60: from.Race == Race.Gargoyle -> SendMessage(RefusalForGargoyles), return false (not equipped).
    a = ctx.ensure_admin()
    c = ctx.fresh(race="gargoyle", female=True, label="p138g")
    try:
        s = give(a, c, "KitsuneEarsTail")
        worn, texts, it = try_equip(c, s, 2.5)
        refused = any(REFUSAL in t for t in texts)
        back = c.wait(lambda: c.items.get(s, {}).get("container") == c.backpack(), 4)
        ok = refused and not worn and bool(back)
        return ok, "gargoyle: refusal %s; worn %s; back in pack %s; messages %s" % (refused, worn, bool(back), texts[-3:])
    finally:
        ctx.done(c)
