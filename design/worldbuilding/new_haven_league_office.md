# New Haven — League of Extraordinary Citizens Field Office

## Overview

The League of Extraordinary Citizens field office is the first permanent institution building added to New Haven via client patch.

It serves as the in-world home of the League in New Haven: a public-facing office where new arrivals can register as League members, receive their first adventuring contracts, and be referred to profession guilds.

## Location

**Facet:** Trammel
**Coordinates:** `3459, 2598`
**Town:** New Haven

## Client Patch

The office layout is a **statics-only edit** applied via client patch `newhaven-league-office` v`1.0.0`.

The `.mul` payload (edited in CentrED#) lives on the fileshare — not in Git:

```text
\\enderman\clusterf-artifacts\uo\client-patches\newhaven-league-office\1.0.0\
```

See: [`design/client_patching.md`](../client_patching.md) and [`client-patcher/patches/newhaven-league-office/README.md`](../../client-patcher/patches/newhaven-league-office/README.md)

## Building Notes

The edit converts an existing New Haven building into the League field office.

Key interior considerations:

- Space for League Registrar NPC
- Public area for players to interact with the Registrar
- Visual identity appropriate to a civic/adventuring institution (banners, signage, desk, notice board)

Exterior considerations:

- Signage or banners identifying the building as a League office
- Visible from the street approach

## Planned NPCs

### League Registrar

Primary NPC at the field office.

Responsibilities:

- Welcome new arrivals
- Explain the League
- Accept League membership registrations
- Issue Iron Citizen rank
- Provide introductory quest flow
- Refer players to the Miners' Compact as the first profession guild
- Offer access to the League Dispatch
- Display citizen rank and Renown information

Suggested NPC name: **League Registrar**

Suggested title: no title, or "of the League of Extraordinary Citizens"

## Server-Side Systems

The client patch provides the visual space. Server-side implementation adds:

| Feature | Status |
| --- | --- |
| League Registrar NPC placement | Planned |
| League membership registration | Planned |
| Iron Citizen rank issuance | Planned |
| Introductory quest flow | Planned |
| Profession guild referral (Miners' Compact first) | Planned |
| League Dispatch access | Planned |
| Citizen rank display | Planned |
| Renown placeholder | Planned |

See the League design document for full system details:
[`design/systems/league_of_extraordinary_citizens.md`](../systems/league_of_extraordinary_citizens.md)

## Introductory Flow (Planned)

1. Player arrives in New Haven.
2. Speaks to the League Registrar at the field office.
3. Joins the League — receives Iron Citizen rank.
4. Registrar explains Renown, guilds, relics, commissions, and expedition systems.
5. Registrar refers player to the Miners' Compact.
6. Player visits the Miners' Compact representative.
7. Player returns to the League Registrar.
8. Registrar awards introductory Renown.

## CentrED# Workstation Note

The CentrED# editing workstation and UO test installation used to create this patch lives outside the server infrastructure. The edited `.mul` files must be manually copied to the fileshare after each edit session.

See the patch README for the fileshare prep commands:
[`client-patcher/patches/newhaven-league-office/README.md`](../../client-patcher/patches/newhaven-league-office/README.md)
