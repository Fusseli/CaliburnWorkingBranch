# PR43 — Group state that is measured, not cached (apply ON TOP OF PR42)

## Symptom

After a fight, reported live:

> "The other group members had speed but not the healers. Although the fight was long over
> the mimic window didn't pop up with those. All other members I got that window."

Two symptoms, one root cause.

## Root cause 1: a healer's aggro list was never pruned

`HasAggro` is `!AggroList.IsEmpty`, and it feeds two things that matter here:

```csharp
// MimicNPC.cs
public override bool InCombat => base.InCombat || MimicBrain.HasAggro;

// MimicBrain.cs
public virtual bool HasAggro => !AggroList.IsEmpty;
```

**(a) The status window.** `MimicNPC.Interact` is the popup with the
`[State] [Brain] [Debug] [Spec] [Inventory]` tabs, and its only per-entity gate is:

```csharp
if (this.InCombat || !base.Interact(player))
    return false;          // ← refuses SILENTLY, no message
```

So a mimic with aggro in its list **cannot be inspected at all** — the click does nothing
and says nothing.

**(b) The group speed.** `CanBuffUpGroup()` returns false if **any** group member has
aggro:

```csharp
foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
{
    if (member is MimicNPC mate && mate.MimicBrain != null && mate.MimicBrain.HasAggro)
        return false;
}
```

So one stale entry blocks the **whole group** from putting its speed up.

And the aggro list of a **healer** was never pruned at all. The only pruner was:

```
SelectGoapAttackTarget -> CalculateNextAttackTarget -> CleanUpAggroListAndGetHighestModifiedThreat
```

and the agent **skips target selection for healers**:

```csharp
// MimicReGoapAgent.TryThink
if (context == MimicDecisionContext.Combat && !Brain.PreventCombat && !Brain.IsHealer)
    Brain.SelectGoapAttackTarget();
```

The only other path is `ClearAggroList()` on a **state exit** — and the AGGRO state's
exit needs `!HasAggro` (an empty list) or a 25 s timer. A healer holding aggro while
sitting in `FOLLOW_THE_LEADER` or `CAMP` **never enters AGGRO, so its list is never
cleared.**

**=> A stale entry makes the healer invisible to inspection and blinds the group's speed,
for as long as the entry survives.**

**The fix:** prune the aggro list every decision, for every bot:

```csharp
public override void Think()
{
    CheckRealmAbilitiesThrottled();
    PruneAggroList();      // PR43
    FSM.Think();
}

public void PruneAggroList() => CleanUpAggroListAndGetHighestModifiedThreat();
```

The cleanup is the same one a fighter already runs every combat tick, and it does not
order the list — it only drops the entries that no longer apply.

## Root cause 2: the heal target ignored distance

`MimicGroup.CheckGroupHealth` picked the heal target by health percent with **no distance
test at all**:

```csharp
if (m_percentCurrent < m_healthPercent)
{
    m_healthPercent = m_percentCurrent;
    MemberToHeal = groupMember;
}
```

So a healer could be sent to a member it had no way of healing — and since **PR42** lets
the walk-to a heal target actually happen (the follow no longer overwrites it), the healer
would **walk off after it and leave the group**, taking the group's speed and its
position with it.

**The fix:** the target must be reachable. The reach comes from the healer's own spells,
so it is data-driven and not a magic number — the same shape as the existing
`GetRezSpellRange()`:

```csharp
public int GetHealSpellRange()   // widest range over the heal pools
public static bool IsBetterHealTarget(int newPercent, int newDistance, int curPercent, int curDistance)
public static bool IsWithinHealReach(int distance, int reach)
```

Most injured first, nearest as the tie-break, and only members within the healer's reach
are candidates.

## Root cause 3: the rez flag could stick true forever

`AlreadyCastingRez` was a **cached** flag, set when a rez was **requested**, and cleared in
exactly one place:

- **set** — `MimicBrain.ExecuteGoapRez` (twice, on the request)
- **cleared** — `MimicGroup.CheckGroupHealth` (line 328)
- and `CheckGroupHealth` is called from **exactly one** place — `MimicBrain.CheckHeals`
- and `CheckHeals` bails for any bot that cannot cast a heal

**=> If the group's only heal-capable member died, nothing ever cleared the flag again:
one rez was requested, and no corpse was ever rezzed again.** `ExecuteGoapRez` bailed on
the flag, the sensor reported `RezAvailable == false`, and the group was done.

**The fix:** ask the live cast state, which cannot get stuck. The queued handler is
included, because a request that has not started yet is still a rez in flight:

```csharp
public bool IsGroupCastingRez()
```

The flag is no longer set on the request, and the sensor writes the live value back so the
two legacy readers stay in step.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR42 | `PruneAggroList`, `GetHealSpellRange`, `IsBetterHealTarget`, `IsWithinHealReach`, `IsGroupCastingRez`; the optimistic rez flag removed |
| `GameServer/custom/MimicNPC/MimicGroup.cs` | PR42 | reach-aware `MemberToHeal` |
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | PR42 | (unchanged — the prune moved into `MimicBrain.Think`, which runs for every state) |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR42 | the rez gate reads the live state |
| `Tests/Unit/GoapTests.cs` | PR42 | 2 new tests |

## Apply order

Full-file snapshots — order matters. PR43 stacks on PR42 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR42 in order …
Copy-Item "$base\Caliburn_PR43_GroupState\*"                 $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **142 total**, all green.

- `TheHealTargetIsTheMostInjuredMemberTheHealerCanReach` — the pure pick and reach rules.
- `EveryBotPrunesItsAggroListAndTheRezFlagIsNeverStuck` — a **guard over the source**,
  because these are wiring facts rather than decisions: `Think()` must prune, the rez flag
  must not be set on the request, and the rez decision must read the live state.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 142/142
```

1. **Click a healer after a fight** — the status window must open. Before this patch a
   mimic with aggro refused the click with no message at all.
2. **The group speed must come up** — it was blocked by one stale aggro entry.
3. **A healer must not walk off after a distant member** — it heals what it can reach.
4. **A group whose only healer dies must still be able to rezz** — the corpse window is
   60 s, and the flag used to stick for good.
5. **Whisper `Debug` on a healer** — the log prints every heal attempt.

## Known limits — deliberately left alone

| Left alone | Why |
|---|---|
| `!Body.attackComponent.AttackState` in `FindTargetForDefensiveSpell` (`:4623`, `:4646`) | a bot that is **melee-attacking** cannot cast any buff at all, self or group. Combined with `ElectHealers` flagging only `max(1, count/4)` members as the group's healer, the other heal-capable members are ordinary attackers and can rarely buff themselves. **That is a second, opposite-direction cause of "some members have the speed and some do not"** — it points at the fighters, not the healers. It is a separate patch: PR43 is about the healer. |
| `MimicGroup.ElectHealers` — one healer in four | a deliberate ratio, and a real design question, not a bug |
| the random buff pick in `CheckDefensiveSpells` | the ordering the goal documents (speed → armour → stat → shield → resist) was never implemented. That is the "Part 2" of the buff work |
| `SpellHandler.CheckDuringCast` / `CheckBeginCast` | engine code, shared with players |
| `Spell.MoveCast` | a per-spell DB flag; faking it would change the heal for players |
| `AlreadyCheckedHeals` | correct and load-bearing |
| PR40's `ShouldFallbackToFsm` / `HoldTickForCast` | correct: every FSM fallback destroys a cast |
| `MimicGroup.HealThreshold` / `EmergencyThreshold` (75 / 37) | changing them ripples through every comparison |
| the instant-heal path | it works while moving and must keep doing so |
