# PR23 — Rezzing fix (apply ON TOP of PR22, which is on top of the export)

## Apply order (important)

**These are full-file snapshots, so order matters. PR23 stacks on PR22, not on
the export.** PR22 and PR23 both replace `MimicBrain.cs`, `TacticalSensor.cs` and
`GoapTests.cs`; applying them in the wrong order silently discards one of them.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR23_RezFix\*"                     $repo -Recurse -Force
```

Full server restart afterwards.

`MimicNPC.cs` is the only file PR23 adds on its own — PR22 does not touch it, so
it is safe regardless.

Both fixes are behaviourally independent (kiting vs rezzing), but they were
verified in the same round. If you want to test them separately, ask for a
rez-only rebased on the export.

## Symptom

Rezzing "behaved weird": corpses lingered but were not reliably rezzed, and
`/mgoap status` sat on `FSM: All eligible actions failed` while a corpse was
waiting.

## Root cause — the sensor and the action disagreed

Battle rez itself is fine and is **not** touched by this patch. PR18's work is
present and correct:

- `ResurrectGoal` carries `REZ_PRIORITY = 15` (calm) and `BATTLE_REZ_PRIORITY = 25`
- `TacticalSensor` computes `REZ_COMBAT_SAFE`: caster clear (no aggro'd enemy
  within 350) and corpse nearly alone (≤ 2 live enemies within 1000)
- `RezAvailable` then requires `!inCombat || combatSafe`, and the corpse must
  already be in range — the caster never chases it
- Priority 25 sits correctly between cure (20) and protect/purge (30)

The problem was four places where the sensor promised something the action would
refuse. Each refusal is expensive: `MimicReGoapAgent.TryThink` removes the failed
action, calls `UpdateSensors()` again (**a full live rescan**) and replans, up to
18 times in one tick before settling on a lower-priority goal. So a corpse just
out of reach taxed every caster tick and buried the readout.

## What this patch changes

### 1. Range gate (the main fix)

`TacticalSensor` gated on the flat `REZ_RANGE` (1500) while `ExecuteGoapRez`
gated on the spell's own `Range`. Any corpse in between was planned and refused,
every tick.

New `MimicBrain.GetRezSpellRange()` returns the **best range among the rez spells
this bot actually holds**, capped at `REZ_RANGE`, and 0 when it holds none. The
sensor now gates on that. `FindOutsiderRezTarget` scans with the same value, so
the stranger-rez path can no longer return a corpse the action will refuse.

### 2. Interrupt gate

`ExecuteGoapRez` returns false immediately when `Body.IsBeingInterrupted`, but
the sensor did not test it outside combat. Added `!_body.IsBeingInterrupted` plus
`rezReach > 0` to the sensor's `RezAvailable`.

### 3. One bad rez spell no longer kills the pool

Four `return false` statements sat **inside** the `foreach` loops in
`ExecuteGoapRez` and `ExecuteGoapRezOutside`. One transient cast refusal (mana,
GCD, a cast still finishing) abandoned every remaining rez spell for that tick.
All four are now `continue`, so the loop keeps looking.

### 4. Stranger rez honours the sensor's choice

`ExecuteGoapRezOutside` re-ran `FindOutsiderRezTarget()` — a second
radius-wide player **and** NPC sweep every decision — and could rezz a different
stranger than the goal was planned for. It now reads
`MimicWorldStateKeys.OUTSIDER_TO_REZ` from the live world state via the new
`MimicBrain.GetGoapObject`, falling back to the scan only if that key is absent.
Same state object the sensors write (`ReGoapAgent.memory.GetWorldState()`).
Halves the stranger-rez sensor cost and removes the disagreement.

### 5. Rez sickness can no longer abort a revive

`MimicNPC.OnMimicRevived` cast the rez-sickness spell (IDs 8181/2435) with no
null check, **before** `ClearAggroList()`, the FSM reset and the viewer
notification loop. `SkillBase.GetSpellByID` returns null when an ID is not
loaded, and a throw there would leave a corpse-looking mimic that fights. Now
guarded, with the switch semantics preserved (RvR → 8181, PvP/PvE → 2435, other
→ no cast). The player's `OnRevive` still has the unguarded pattern; that is
untouched here.

## Files

| File | Change |
|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | `GetRezSpellRange()`, `GetGoapObject()`, pool `continue`s, outsider reach + world-state read |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | `RezAvailable` uses real reach + interrupt gate |
| `GameServer/custom/MimicNPC/MimicNPC.cs` | rez-sickness null guard |
| `Tests/Unit/GoapTests.cs` | 3 new tests |

## Tests

3 new tests, **91 total** (was 88):

- `RezReachNeverExceedsTheSpellsTheBotHolds` — a bot with no rez spell reports
  reach 0, so the sensor withdraws the goal rather than planning a doomed cast
- `RezGoalNeedsBothSensorAndActionToAgree` — calm rez still ranks 15 when the
  sensor says reachable, 0 when it does not
- `StrangerRezStaysTheLowestPriorityThingABotCanDo` — outsider rez stays below
  plain damage

No test can prove the *live* cast succeeds; that needs a live group with a
corpse, same as the original handover's live protocol.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 91/91 green
```

Live:

1. Kill a group member, stand the corpse ~1000 from a caster. `/mgoap status`
   should show `ResurrectGoal` / `CastRez`, not `FSM: All eligible actions
   failed`. Rezzed within a second.
2. Same but with the corpse beyond the caster's spell range. Goal must simply
   not appear — and the caster's tick time should not spike.
3. Mid-fight, corpse safe (no enemy within 350 of the caster): rezzes at priority
   25. Mid-fight, corpse contested: no rezz, goal absent.
4. `/mrez outside` with a stranger corpse nearby and the group calm: rezzes, and
   the target matches the one the sensor picked.
5. Interrupt a caster mid-decision: it should hold off, not spam failed plans.

## Known limits (unchanged, still open)

- **Grouped-kite stale flag** in `SelectGoapAttackTarget` — leading suspect for
  "bots freeze after fights", untouched (kept as its own patch).
- `TacticalSensor.cs` still hardcodes `BUFFS_MAINTAINED = false` every tick, so
  `BuffMaintenanceGoal` is never satisfied and every calm support think re-plans
  a buff round. Bounded waste, documented in `HANDOVER_Issues.md`.
