# Handover: mimic archery (PR11–PR13)

Companion to `HANDOVER.md` (base GOAP stack PR1–PR10). Same conventions: stacked
drop-in folders, each only its own delta, verified in temp overlays.

Server context: `allow_old_archery = false` (new archery live). All three patches
keep mimics correct under **both** flags — each patch gates itself on the flag it
belongs to. Switching the flag for test sessions is safe; pick one for live play
(player consistency).

## Diagnosis recap (why ~0 damage)

New archery maps all bows to the single `Specs.Archery` specialization, which
mimics never train (their `ClassSpecs` hold Longbow 35–50 / CompositeBow 35–45 /
RecurveBow 35–45 — correct files, not the bug). Every bow shot resolves spec via
`WeaponSpecLevel` → `ServerRules.GetObjectSpecLevel` →
`GetModifiedSpecLevel("Archery")` → **0** (unknown keys return 0,
`MimicNPC.cs:4629`) — the exact hole `Fenyn/Caliburn@ba51dbb` patched for
`Mob_Spells`. Spec 0 poisons hit chance, spec modifier and caps. Same bug class,
same fix pattern. (`ClassSpecs` must NOT gain an Archery line: fixed spec-point
budgets would dilute melee/stealth, and RA thresholds key off bow specs.)

## The patches (copy in order, each over the previous)

| # | Folder | Files | What it adds |
|---|---|---|---|
| PR11 | `Caliburn_PR11_ArcheryFix` | 1 (`MimicNPC.cs`) | `GetModifiedSpecLevel`: `Archery` → best bow spec (Longbow/Composite/Recurve). ~6 lines. Dead code under old flag. Repairs hit chance, spec modifier, caps — all flow through this point. |
| PR12 | `Caliburn_PR12_OldArchery` | 2 (`ArcherBrain.cs`, `MimicBrain.cs`) | Old-flag specials. `ArcherBrain.OnRefreshSpecDependantSkills()` grants Crit Shot 1–9 / Rapid Fire 1–2 / SureShot / Penetrating Arrow 1–3 by bow spec (mirrors player `OnSkillTrained` thresholds, gated on old flag). `UpdateRangedShotType()` per decision: stealthed opener → Critical, enemy casting at range → Rapid Fire, else Normal (engine consumes per shot). Flag watcher re-grants shortly after a live admin flip. `MimicBrain.SelectGoapAttackTarget` is now `virtual` (one word) so the override runs on GOAP ticks; FSM fallback keeps legacy Normal shots. Penetrating is passive post-grant. Volley/Trueshot excluded (realm-point purchases, no bot RA economy); SureShot granted but untriggered (accuracy half lives in player code). |
| PR13 | `Caliburn_PR13_ArrowType` | 8 | New-flag damage-type choice. Chooser buffs Blunt/Thrusting/Slashing Arrows (spell IDs 7397/7398/7399, Archery line level 1, Self, ~permanent, stealth-safe, plain handler with no NPC gate) are applied by direct cast — no grant pipeline needed. `BestArrowType()` pure scoring (min resist + worn-torso armor resist, +5 stickiness, Slash default) → unit-tested. `ArrowTypeGoal` (2.5, combat only) / `ChooseArrowType` / `ExecuteGoapArrowType()`. Engine touch, mimic-only: `AttackComponent.AttackDamageType` NPC branch honors an active chooser buff (`GetMimicArrowType()`, default legacy `MeleeDamageType`, players byte-identical). Dormant under old flag (shots take type from ammo there). |

End state: 16 goals / 15 actions / 6 sensors. Tests: **45/45** (39 GOAP + 6 election).

## Verification (temp overlays, working repo untouched)

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 45/45 green
```

No unit test for the spec fallback or the grant trigger (real NPCs need server
singletons — PR6b lesson); those verify live below. Scoring (`BestArrowType`)
is covered by unit tests, including tie/stickiness behavior.

## Live-measure protocol

1. **New flag, PR11:** same archer bot, same target, ~10 shots before/after —
   expect Level-scaled hits instead of ~0, plus a real hit rate.
2. **New flag, PR13:** vs plate → `ChooseArrowType` then Crush lines; vs chain →
   Thrust/Slash; after a cold debuff lands (PR8), cold-side choices where
   applicable. `/mgoap status` shows the goal/action.
3. **Flip to old:** stealthed opener logs Critical, Rapid Fire vs casters,
   bladeturn pierce vs shielded targets, Normal otherwise. Flip back: one tick
   later everything Normal with correct damage (watcher check).

## Known limits (accepted)

- Chooser buffs cover Crush/Slash/Thrust only — no Heat/Cold variants exist in
  this DB (custom spells would be a separate project).
- Old-flag NPCs ignore quiver ammo (engine); arrow-tier variety (bodkin/
  broadhead) is not implemented for bots.
- New-flag bow cadence follows the `CastingSpeed` branch (mimics ~0 → base
  cadence); untouched, measure only if cadence feels off.
- `GetMimicArrowType()` reads buffs by spell ID (7397–7399) but returns each
  buff's own `DamageType`, so custom chooser buffs work the same way.
