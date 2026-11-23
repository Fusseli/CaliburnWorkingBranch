# Mimic tactical GOAP

GOAP is enabled by default. Each mimic lazily constructs its own agent at the first
tactical decision. `MimicBrain.Think()` still runs the FSM's lifecycle; the state
handlers give GOAP first choice for healing, support, and combat work. They run the
legacy decision when `TryGoap` returns false.

This is a hybrid integration, not a replacement for every FSM state. Travel,
following, camping, pulling, resting, duel readiness, aggro discovery, combat
entry/exit, and death/respawn retain their existing state handling. Skipping that
handling whenever a plan exists would strand bots in stale states.

Group roles (tank/assist/CC/puller) default to the leader at formation and are
re-elected to the most capable bot whenever membership changes: Guard holders
tank, mez-casters CC, nukers assist, bow-holders pull, heal-capable bots are
flagged healers until covered. `/mrole` locks a role and `/mheal` locks the
healer flag; manual choice always wins. Players are never assigned or demoted.

## Registered behavior

| Goal | Action/mechanics |
| --- | --- |
| EmergencyHealGoal | HealOrCure; existing emergency, instant and group healing selection |
| HealGroupGoal | HealOrCure; includes solo self-healing, power/cooldowns, range movement and group coordination |
| CureGroupGoal | HealOrCure; existing mezz/disease/poison curing rules |
| ResurrectGoal | CastRez; dead group member in rez range, same realm per engine rules; 15 calm, 25 battle-rez only when the cast is safe (caster clear, corpse nearly alone); manual /mrez wins |
| ResurrectOutsiderGoal | CastRezOutside; dead stranger in rez range, same realm; only when the own group is fully calm; opt-in via "/mrez outside" |
| PurgeGoal | UsePurge; self-purge when mezzed/stunned/rooted with Purge ready; bought automatically like all class RAs |
| ControlAddsGoal | ControlAdd; main CC's valid queued adds, including a single remaining add |
| InterruptEnemyCasterGoal | InterruptCaster; melee pressure first, else one offensive-spell tick |
| AssistTrainGoal | FollowAssist; non-assist members take the main assist target |
| TargetCallerGoal | CallFocusTarget; main assist calls casters first, then weakest valid enemy |
| ProtectGroupGoal | PeelEnemy; main tank takes mobs off attacked group members |
| GuardHealerGoal | AssignGuard; neediest member (attacked healer/caster first); manual /mguard wins |
| QuickcastRecoveryGoal | SecureCast; interrupted caster pops Quickcast; outranks kiting |
| KiteGoal | KiteToSafety; solo long flight with hold ring, grouped falls back to the tank; melee and main tanks hold |
| DebuffPriorityGoal | ApplyDebuff; first missing debuff from the harmful pool, then damage |
| PositionalStyleGoal | MoveToFlank; flank walk for side/back styles; tanks hold front |
| ArrowTypeGoal | ChooseArrowType; resist-beating chooser buff (new archery only) |
| DealDamageGoal | CastOffensiveSpell, then EngageTarget if casting cannot act |
| BuffMaintenanceGoal | MaintainBuffs; existing missing-effect checks and class overrides |

EngageTarget reuses the existing weapon, pet, offensive ability, positional-style and
chase mechanics. Kiting and Quickcast are separate registered actions (see table). Spell selection remains class-aware,
including ArcherBrain and AssassinBrain overrides. GOAP actions do not call the
FSM or recursively enter GOAP.

The other prototype goals and per-spell actions remain available in the source,
but are not registered by this integration. Their presence alone does not imply
that they have complete sensors, achievable effects, or verified execution paths.
New independent tactical behaviors should be added with all three and a regression
test; do not register a goal just to claim coverage.

## Decisions and fallback

Sensors read current state before every decision. The tactical sensor reads living
group members directly, so it does not depend on a prior FSM healing pass to
populate cached group health. It uses the configured healing/emergency thresholds
and treats aggro as combat intent before the first attack sets `Body.InCombat`.
Class capabilities and roles are read again each decision, so level, spec and role
changes do not require rebuilding a cached spell-action catalogue.

The planner tries positive-priority, unsatisfied goals in descending order. An
unachievable high-priority goal does not starve a lower feasible goal. Search is
bounded to 128 expansions per goal, with value-based state deduplication. A zero
heuristic preserves cost ordering for actions with fractional costs or multiple
effects. Planning and execution occur on the owning brain thread, with no pending
manager callbacks that could overwrite a newer decision.

Registered actions issue one tick of work. They may initiate or maintain an attack,
cast, or movement; completion means that command was serviced, not that a target
was killed or fully healed. Plans use incremental effects (`targetDamaged` and
`targetHealed`) and never copy predicted effects into live memory. On the next tick,
the agent re-senses and reprioritizes. Emergency healing can interrupt a non-healing
cast through the existing healing routine; normal spell completion is preserved.
These coarse tactical plans are normally one action long. They do not yet implement
long multi-action strategies such as a separately planned flank-and-interrupt sequence.

If a command fails, its action is excluded for that decision and the planner can
choose another action. Attempts are bounded by the action count. If all choices
fail, no goal is feasible, configuration is absent, or GOAP is disabled, the legacy
decision runs in the same tick. Exceptions clear the plan, log a warning, and use
FSM decisions for five seconds before retrying. Ordinary lack of work does not
disable GOAP or produce repeated warning logs.

## Diagnostics

As a GM, target a mimic:

```text
/mgoap status
/mgoap off
/mgoap on
```

Status shows the latest result/fallback reason, last selected goal/action,
successful GOAP decision count, fallback decision count, and registration counts.
The switch is applied on the next tactical AI tick. Counts are decision counts,
not spell hits or completed kills. A healthy idle warrior can legitimately show
fallback because it has no support work; check the counter during combat.

## Validation

From the repository root, the existing test project requires a local config file:

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj
dotnet test Tests/Tests.csproj --filter FullyQualifiedName~DOL.Tests.Unit.GoapTests
```

The GOAP tests run without a game database. They cover registered goal/action
compatibility, planning, priority switching, failed-command alternatives, fallback,
recovery, missing configuration, sensor errors, and the live brain-to-FSM routing
seam. Mechanics are stubbed in controller tests: this is not a live gameplay test.

Before deploying, use a configured development server and DAoC client to check:

1. Solo melee and caster bots acquire an enemy and increase the GOAP decision
   counter; killing the target returns them to their prior travel/camp behavior.
2. A healer switches to EmergencyHealGoal as a nearby group member crosses the
   configured emergency threshold. Check solo healing and normal healing recovery.
3. A main CC controls the last queued add without damaging the group's controlled
   targets. Check target changes and cooldown/LoS failures.
4. Support bots maintain missing buffs, finish current casts, and resume following,
   camping or resting when no support work is possible.
5. Archer/assassin stealth, poison, ranged weapons, tank target selection, pet
   attacks, positional styles and caster fleeing retain their existing behavior.
6. `/mgoap off` continues legacy behavior; `/mgoap on` resumes increasing GOAP
   counts. Check role/spec changes, duel readiness and death/respawn.

No database-backed server/client session was run as part of this change.

## Not registered (by design)

All 21 goal files in `ReGoap/Goals` are in a decided state: 15 registered above,
1 owned by the FSM, 4 retired. Retired files stay in source as reference; their
presence alone implies no complete sensors, achievable effects, or verified
execution paths.

| Goal | Disposition |
| --- | --- |
| PullTargetGoal | FSM-owned. Pulls run through the CAMP state machine (`CheckPuller`, `IsPulling`, camp re-entry); a GOAP pull would double-pull, fight camp returns, and stall on stale targets. |
| SurviveGoal | Retired. A universal 0–90 self-preservation goal would preempt every role goal; self-survival is covered by EmergencyHeal (healers) and Kite (pressured casters). |
| BackupHealGoal | Retired. Same `GROUP_FULL_HEALTH` effect key as HealGroupGoal; registering both double-plans the same work. |
| MaintainSpeedGoal | Retired. Same `BUFFS_MAINTAINED` effect as BuffMaintenanceGoal (which already escalates on missing speed); its own `speedMaintained` key is set by no action. |
| BreakEnemyMezzGoal | Retired. Mezz/disease/poison curing runs through CureGroupGoal's existing cure mechanics; its `friendlyTargetFree` key is set by no action. |

Protect and Intercept stay manual (`/mprotect`, `/mintercept`, whispers): one-shot
reaction windows with no maintained state to plan around. Guard is the only
defensive ability GOAP manages, and only while no manual `/mguard` stands.
