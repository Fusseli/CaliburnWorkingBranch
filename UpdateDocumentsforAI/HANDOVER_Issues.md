# Handover: live-test issues (freeze, long ticks, stale display)

Companion to `HANDOVER.md` (stack PR1–PR10) and `HANDOVER_Archery.md` (PR11–PR13).
Status: triaged from live reports + server logs; **PR15 (diagnostics micro-fix)
not built yet**. No code changes described here are applied anywhere.

Test setup when reported: multiple PR folders applied at once (exact set
unconfirmed — re-confirm on the test server), bot groups incl. solo bots,
post-fight/post-roam situations. Fresh spawns behave fine.

## Issue 1 (CONFIRMED, cosmetic): stale `/mgoap status` display

**Symptom:** `/mgoap status` permanently shows `MaintainBuffs` / BuffMaintenanceGoal
as last goal/action, long after the bot buffed once and moved on normally.

**Root cause:** `MimicReGoapAgent` writes `LastGoal`/`LastAction`/`Status` only on
the GOAP-success path. `Fallback()` updates `Status` (`"FSM: <reason>"`) but never
clears `LastGoal`/`LastAction`, and FSM-only ticks leave all three untouched.
So the display fossilizes at the last successful GOAP decision. Behavior is
correct — only the readout lies, and it poisons every future bug report.

**Fix (PR15 part 1):** clear `LastGoal`/`LastAction` to `"None"` inside
`Fallback()`. Zero behavior change, fully unit-testable (assert cleared state
after each fallback path). Also note: PR2 left no README row for Interrupt
(accepted earlier) — unrelated, do not touch.

## Issue 2 (OPEN): bots freeze after actions/fights

**Symptom:** bots/groups stand around doing nothing. Group leader shows status
`roaming` but never moves. Deleting the leader promotes the next member, buffs
resume briefly, then everything stands still again. Only after running/fighting,
never at spawn.

**Killed theory:** a permanent `MaintainBuffs` planning loop. Disproven by
observation — bots buff *once* and move on, so actions complete and the FSM
resumes. The always-false `BUFFS_MAINTAINED` sensor flag (see below) causes
bounded waste, not standstill.

**Narrowed suspects (unconfirmed, need data):**
1. ROAMING delay path — `CheckDelayRoam()` perpetually true
   (`MimicBrain.cs`, via `TryGoap(Support)` succeeding spuriously or
   `Sit(CheckStats(75))` resting forever) → `delayRoam` blocks all roam movement
   while status stays `roaming`. Fits best.
2. CAMP stale destination — `Body.IsDestinationValid` stuck true (unreachable
   point) → `MimicState_Camp.Think()` early-returns every tick: no buffs, no
   pull, no sit, no CC. Fits "stands still" generally.
3. Stuck flee/flank flags (PR6/PR9) — stale `IsFleeing`/`IsFlanking` suppressing
   spell selection via `OffenseAvailable`. Would freeze *casting*, not movement;
   weakest fit for full standstill, keep as fallback suspect.

**Data needed (in priority order):**
1. `/mgoap status` on a *frozen* bot (after the PR15 display fix, else the goal
   name can't be trusted): GOAP-vs-FSM line, decision/fallback counters, last
   goal/action. `GOAP + climbing Decisions` ⇒ suspect 1 variant (which goal?);
   `FSM: ...` + idle counters ⇒ suspects 2/3 (which state? what reason?).
2. Log context around a freeze (state transitions, warnings, exceptions).
3. Solo or grouped? Which FSM state (camp/roam/follow)? Mana full or drained?
   Anyone stuck mid-cast animation? Does `/mgoap off` unfreeze them (proves
   GOAP involvement) — cheap, decisive test.

**Do NOT build a fix blind.** All three suspects point at different files;
a guess patch risks breaking working behavior.

## Issue 3 (OPEN): long server ticks with many mimics

**Data (server logs, confirmed):**
- `Long NpcService.Tick`, 26–45 ms per think (interval 500), across ALL brain
  types (`MimicBrain`, `ArcherBrain`, `AssassinBrain`) — so the cost is in shared
  paths (sensors/planner/`CheckSpells`), not subclass logic.
- `Long EffectListService.Tick`, 93 ms, across everyone *including regular mobs*
  (`wolf youth`) — systemic effect processing, not mimic brains.

**Killed theory:** the buff loop as the main tick burner (same disproof as above:
single buff round, then normal behavior).

**Working hypothesis:** per-think sensor cost × mimic count. Every think runs full
live scans regardless of need: `GetOrderedAggroList` sorts, `CanAggroTarget`
(server rules) per enemy per CC candidate, `FindGuardCandidate` effect scans per
member, `FindFocusTarget` aggro scan, 16-effect debuff scan, resist reads. ~30 ms
× hundreds of mimics × 2 Hz concurrent thinks = long ticks. Unproven until measured.

**Next step (PR15 part 2, diagnostics — NOT optimization):** rolling per-phase
millisecond counters (sense / plan) on the agent, shown in `/mgoap status`,
unit-tested. Then read two numbers off a lagging bot: sense-heavy ⇒ stagger
sensors (PR16); plan-heavy ⇒ bound planner retries; neither ⇒ look at
`CheckSpells`/engine paths. No blind optimization.
The 93 ms effect ticks should be re-checked after behavior normalizes (possible
effect churn from redundant casts); if they persist independently, that's an
engine-side investigation, not a mimic one.

## Known waste (accepted, bounded, not the freeze)

`TacticalSensor` hardcodes `BUFFS_MAINTAINED = false` every tick, so the buff
goal is always *eligible* out of combat and every support think plans a buff it
usually doesn't need (action then fails → fallback → FSM runs). Cost, not
standstill. Proper fix is throttled truthful buff state (attempt at most every
~60 s per bot, only when calm 10 s+; buffs last 10–30 min) — deferred until
after the freeze is understood, to keep patches independently testable.
Previously discussed cadences (60 s / 10 s recommended) still stand.

## Planned PR15 (tiny, safe, unlocks everything above)

1. Clear `LastGoal`/`LastAction` on fallback (Issue 1 fix).
2. Rolling sense/plan ms counters in `/mgoap status` + unit tests (Issue 3
   instrumentation).
3. **No behavior change whatsoever** — same decisions, same priorities.
4. Verify: build 0 errors + unit tests in temp overlay (same process as all
   previous patches), then collect Issue-2/Issue-3 field data with clean instruments.

## Field data protocol (for the test-server session)

1. Apply PR15 first (after it is built), so goal names can be trusted.
2. On a frozen bot: `/mgoap status` + state (camp/roam/follow?) + solo/grouped +
   `/mgoap off` unfreeze test + 20 log lines around it.
3. On a lagging bot: `/mgoap status` sense/plan ms + total mimic count on server.
4. Paste all of it back here, numbered per incident. Do not change two things
   at once (one patch per test round, same as the build order).
