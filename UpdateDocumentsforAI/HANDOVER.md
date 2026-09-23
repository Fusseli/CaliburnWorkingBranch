# Handover: Mimic tactical GOAP patch stack

This file hands the finished GOAP work over to another machine / AI session.
Everything the other side needs is here; the patch folders carry the code.

- **Source repo:** a Caliburn (OpenDAoC) checkout. Base = `Fusseli/CaliburnWorkingBranch`
  `master @ 660394d` (pre-PR state). The working repo this was built against was
  verified to match the PR base (spot-checked `MimicSensor.cs` pre-PR
  `Console.WriteLine` version, `ReGoapPlanner.cs` `maxIterations = 1000`).
- **Starting point:** GitHub PR
  `Fusseli/CaliburnWorkingBranch#1` — "Wire mimic tactical GOAP with FSM fallback"
  (`joshlit:codex/wire-mimic-goap @ 5c9a96e`, 1 commit, 18 files, +861/−1416).
- **End state:** 11 stacked drop-in folders → **15 goals / 14 actions / 6 sensors**,
  role election with manual locks, `/mgoap` diagnostics, **42 unit tests green**,
  build 0 errors, full-suite parity with clean base (see Verification).

## The stack (copy in this order, each over the previous)

| # | Folder | Files | What it adds |
|---|---|---|---|
| PR1 | `Caliburn_PR1_GOAP` | 18 | The GitHub PR itself (head `5c9a96e`): hybrid GOAP (GOAP-first inside FSM states, FSM fallback same tick), 6 goals / 5 actions / 6 sensors, `/mgoap status\|on\|off`, `GoapTests.cs`. |
| PR2 | `Caliburn_PR2_Interrupt` | 6 | Interrupt: `enemyCasting`/`interruptAvailable` sensor facts, `InterruptEnemyCasterGoal` (prio 9), `InterruptCaster` (melee pressure first, else one offensive tick, cost 0.5), `ExecuteGoapInterrupt()`. |
| PR3 | `Caliburn_PR3_Assist` | 7 | Assist train: non-assist members open on the main-assist target (`CheckGoapAssistTarget`, restores intent of dead `CheckAssist()`), `AssistTrainGoal` (4) / `FollowAssist`. Target selection only — no forced movement, casters keep range. |
| PR4 | `Caliburn_PR4_Peel` | 7 | Tank peel: scan of aggroed enemies hitting group members (CC'd skipped, never breaks own mezz), `ProtectGroupGoal` (`min(6+3×loose,30)`, ×2 healer attacked) / `PeelEnemy`, `ExecuteGoapPeel()`. Main tank only. |
| PR5 | `Caliburn_PR5_Guard` | 9 | Auto-guard: neediest-member scoring (healer 6 / caster 4 / other 2, +3 under attack, +2 HP<50, hysteresis, shared helper so sensor and action agree), `GuardHealerGoal` (7 combat / 3 support) / `AssignGuard`. Manual `/mguard` or whisper-"Guard" locks the agent out while its target stands (`ManualGuardTarget`, cleared on toggle-off/lapse). Protect/intercept stay manual. |
| PR6 | `Caliburn_PR6_Survival` | 8 | Caster survival pair: `KiteGoal` (6, pressured casters gain distance via existing `Flee(600)`, melee never kites) / `KiteToSafety`; `QuickcastRecoveryGoal` (8, pops Quickcast, mirrors FSM handling) / `SecureCast`. Arrival hygiene clears stale flee flags so spell selection recovers. |
| PR6b | `Caliburn_PR6b_Roles` | 6 | Class-aware role election (NOT GOAP): fresh groups start all-roles-on-leader, so `MimicGroup.ElectRoles()` moves tank/CC/assist/puller to capable bots (Guard holders tank, mez-casters CC, nukers assist, bow-holders pull) and auto-flags heal-capable bots (1 per 4). Triggered lazily on membership change. `/mrole` locks roles, `/mheal` locks the flag, players never assigned/demoted, ties keep incumbents. `RoleElectionTests.cs` (6 tests, snapshot-pure by design — real NPCs need server singletons). |
| PR7 | `Caliburn_PR7_Caller` | 7 | Target caller: main assist opens on highest-value enemy (casting +50, weakest-first, +5 stickiness, CC'd never called) via `FindFocusTarget()`; `TargetCallerGoal` (4) / `CallFocusTarget` as planner safety net. Train follows one tick later via PR3. |
| PR8 | `Caliburn_PR8_Debuff` | 7 | Debuff before damage: sensor reads 16 major `eEffect` debuffs live off the target (source-agnostic, no redundant stacking); `DebuffPriorityGoal` (3 on-train / 0.9 off) / `ApplyDebuff` → `ExecuteGoapDebuff()` picks the first applicable `Spell.IsDebuff` via existing `CanCastOffensiveSpell` + `LivingHasEffect` + `CheckOffensiveSpells`. |
| PR9 | `Caliburn_PR9_Positional` | 7 | Flank walk for side/back styles: sensor mirrors the FSM flank gates (melee weapon, attack range, victim still + busy elsewhere, not tank, not flanking); `PositionalStyleGoal` (3.5) / `MoveToFlank` → `ExecuteGoapFlank()` (compute point → `WalkTo` → poll arrival → latch `IsFlanking`, same as FSM). Target-switch `ResetFlanking()` added to GOAP selection (FSM reset lives in `AttackMostWanted`, which GOAP bypasses). |
| PR10 | `Caliburn_PR10_Docs` | 1 | Closing README section (this table's rationale in prose). Docs only. |

Each folder mirrors repo structure (`GameServer/...`, `Tests/...`) and contains
**only its own delta** vs the previous stage. Copy command (PowerShell):

```powershell
Copy-Item "<Folders>\Caliburn_PR1_GOAP\*" "<YourCaliburn>\" -Recurse -Force
# ... repeat PR2, PR3, PR4, PR5, PR6, PR6b, PR7, PR8, PR9, PR10 in order, test between steps
```

Note: `Caliburn_PR6b_Roles` sorts between PR6 and PR7 alphabetically. PR2 has no
README update (accepted gap — its Interrupt row enters via PR3's README).

## Priorities (high → low, per decision)

EmergencyHeal 100 · CureGroup 20 · ControlAdds ≤40 · Interrupt 9 · Quickcast 8 ·
Guard 7 combat / 3 support · Peel ≤30 (≤60 healer attacked) · Assist 4 · Caller 4 ·
Debuff 3 (0.9 off-train) · Kite 6 · Flank 3.5 · Damage 2 (0.6 off-train) · Buffs ≤7.5.
Failed commands are excluded for that decision; total failure → FSM legacy runs
the same tick; exceptions → FSM for 5 s. One tick of work per decision, then
re-sense. Predicted effects are never written into live memory.

## Verification (all done in temp overlays; working repo untouched)

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 42/42 green (36 GOAP + 6 election)
```

Full suite: 364 tests, 292 pass. The rest is **pre-existing, proven by TRX
comparison against a clean-base overlay: identical 44 failure names**
(spell/combat math: `CalculateDamageVariance*`, `CalcValue_*`, `ToHitChance*`,
`CastSpell_*` — zero GOAP/Mimic/role involvement), plus an `Integration.Database`
test-host crash also present on clean base. To re-prove: run both trees with
`--logger trx`, compare failed test-name sets; expect empty diff.
The 42 new tests are the contract for this stack — if any of them fails, the
stack (not the base) regressed.

## Live checklist (DAoC client + dev server)

- `/mgoap status` (target a mimic): GOAP vs fallback decision counters, last
  goal/action. A healthy idle warrior legitimately shows fallback — check in combat.
- Interrupt: enemy caster → `InterruptEnemyCasterGoal / InterruptCaster`.
- Assist: grouped bots open on the assist target (works for casters too; healers
  and the assist itself excluded). `/mrole assist` moves it; players can assist.
- Peel: mob chewing the healer → tank shows `ProtectGroupGoal / PeelEnemy`,
  never touches CC'd adds.
- Guard: tank guards whoever is taking melee (not always the healer);
  `/mguard <name>` pins and GOAP leaves it alone.
- Survival: pressured caster → `SecureCast` if Quickcast ready else `KiteToSafety`,
  resumes nuking after (no stuck flee).
- Roles: healer-led bot group elects tank/CC/assist from capable classes and
  flags a healer out of combat. `/mrole`, `/mheal` always win.
- Caller: assist opens on casters, then weakest; train follows.
- Debuff: first cast vs undebuffed enemy is the debuff, then damage.
- Flank (needs eyes): side/back style users walk the flank of still, distracted
  victims; watch for chase-forever vs kiters and arrival on slopes.

## Known approximations (accepted)

- Player healers score as casters for guard priority (bot code can't read player spec).
- Mezz/disease/poison curing flows through CureGroup's existing mechanics.
- Same-count member swaps re-elect on the next membership change; election needs
  one AI tick after join/leave.
- Renegades (`Realm = Renegade` plain `MimicNPC`s) inherit the whole stack;
  only their hostility rules differ (untouched).

## Doctrines for continuation (please keep them)

1. One tick of work per decision, re-sense next tick; never write predictions
   into live memory.
2. Every behavior ships as sensor flag + goal + action + test, reusing existing
   brain mechanics (no second spell/target/movement engine).
3. Fail closed: false/failed → FSM legacy runs the same tick.
4. Manual commands (`/mrole`, `/mheal`, `/mguard`, …) always win via locks.
5. Don't register a goal without all three (sensor, effect, execution) + test.

## Open threads (not started)

- PR9 flank live tuning (kiters, slopes, style damage numbers).
- Optional: auto-protect second squishy (clone of PR5 guard pattern).
- Pulls stay FSM-owned (CAMP would fight GOAP pulls); Survive / BackupHeal /
  MaintainSpeed / BreakEnemyMezz stay retired — reasons in
  `GameServer/custom/MimicNPC/ReGoap/README.md` ("Not registered (by design)").
