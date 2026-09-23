# Handover: mimic realm abilities (PR19a–PR19b)

Companion to `HANDOVER.md`, `HANDOVER_Archery.md`, `HANDOVER_Rez.md`
(PR16–PR18) and `SESSION_SUMMARY.md`. Same conventions: stacked drop-in
folders, each only its own delta, verified in temp overlays before delivery.

Context: bots already *earn* ranks (`MimicNPC.GainRealmPoints` auto-levels
`RealmLevel`, same ratesmodifiers as players) but could never *spend* them —
RA purchase needs trainer-window packets (real client). These patches keep the
earning and replace the dialog gate with a server-side auto-buyer. Player
flows untouched. Artifacts skipped by user decision (design sketched in
`SESSION_SUMMARY.md`, easy to revive).

## The patches (copy in order, each over the previous, test between steps)

```powershell
Copy-Item "<Folders>\Caliburn_PR19a_RAbuyer\*" "<YourCaliburn>\" -Recurse -Force
# ... then PR19b_Purge
```

| # | Folder | Files | What it adds |
|---|---|---|---|
| PR19a | `Caliburn_PR19a_RAbuyer` | 6 | RA auto-buyer. `MimicRABuyer`: curated priority table (universal → archetype: Healer/Caster/Archer/Melee, stealth-gated MoStealth) spends the rank budget (`Level>19 ? max(1,RL) : RL`, player formula) on class-list RAs; RR5 excluded (arrives free via spec at RL40 like players). One mimic-only engine branch in `LiveRealmAbilitiesSpecialization` surfaces granted RAs as live abilities, so **all passives apply through the normal Activate/AbilityBonus path with zero brain code**. Trigger: throttled brain check (immediately at spawn, then on rank change / every 10s). `/mra` shows rank, points spent/budget and the RA list. |
| PR19b | `Caliburn_PR19b_Purge` | 8 | Self-purge. `PurgeAbility.RemoveNegativeEffects` generalized from `GamePlayer` to `GameLiving` (was a hard `return false` for NPCs; chat/stealth feedback stays player-guarded — player behavior byte-identical). `PurgeGoal` (30) / `UsePurge` → `ExecuteGoapPurge` when mezzed/stunned/rooted with Purge off cooldown; yields while interrupted. |

End state: 19 goals / 18 actions / 6 sensors. Tests: **76/76**
(`GoapTests|RoleElectionTests`: 65 pre-RA + 9 PR19a + 2 PR19b).

## Verification (build machine, temp overlays)

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# PR19a stage: expect 74/74; full chain: expect 76/76
```

Unit-subset parity (`--filter 'FullyQualifiedName~Unit'`) TRX-compared vs clean
base: identical 44 pre-existing spell-math failures on both sides. PR19a was
additionally verified as an isolated stage (base+PR1–PR18+PR19a: 0 errors,
74/74). Final clean-room re-apply PR1–PR19b: 0 errors, 76/76. (Full unfiltered
`dotnet test` stays flaky DB-less — scheduler-host crash, also on pristine
base; use the Unit subset.)

## Live-test protocol (one round per folder)

1. **PR19a:** spawn any bot group → within ~10s `/mra` on a member shows rank,
   points and bought RAs (rank-seeded at spawn, more as they earn RPs).
   Cross-check one passive: e.g. a Toughness bot has more HP than the same bot
   pre-patch, an Aug-Con bot more con. Rank-ups over time buy further levels
   (watch `/mra` after a kill streak).
2. **PR19b:** mezz/stun/root a Purge-owning bot → `/mgoap status` shows
   `PurgeGoal / UsePurge` → CC breaks early. Reuse timer honored (no spam).

## Needs eyes (cannot verify without a server+client)

- Whether the RA spec (`Realm Abilities`, generic career) is actually in the
  mimic career load: if `/mra` lists RAs but stats don't move, the spec gate
  needs a follow-up (report which class).
- Whether healer/caster archetypes buy sensibly (priority table is curated
  data — report odd picks and the table gets tuned, no code change).
- Atlas RA set (`USE_ATLAS_REALM_ABILITIES=true`): buyer only knows Live
  KeyNames + one encoded Atlas chain (`Avoid Pain` ← AugCon 3). Under Atlas,
  unmatched KeyNames are skipped safely (no wrong buys), but bots stay
  RA-less until the table is extended — report if you run Atlas.

## Known limits (accepted)

- Listed RAs only: unlisted actives (Charge, Vanish, First Aid, …) are never
  bought and never wired. Next candidates when wanted.
- No persistence: mimics re-buy from scratch every spawn (transient by design).
- Atlas prereq chains beyond `Avoid Pain` are not encoded yet.
- Live `CheckRequirement` parity: buying skips the dialog-time requirement
  check; class list + rank budget + encoded prereqs enforce the same outcome
  for all curated entries.

## Field data protocol

Per incident paste back, numbered: PR folder applied, `/mra` output, `/mgoap
status` (for Purge), class/level/rank of the bot, and what you expected vs saw.
