# PR35 — Song gate on aggro, not the sticky combat flag (apply ON TOP of PR34)

## Symptom

From the PR34 round, with a **solo** Minstrel:

- On spawn it played the speed song ✓ (unchanged)
- After the fight it played **`PowerRegenBuff`** instead of speed — previously it had been `HealthRegenBuff` — and **never switched back to speed**

## Root cause: PR34 gated speed on two signals that don't mean what I assumed

PR34 asked *"is it safe to put the group speed buff up?"* with:

```csharp
if (!inCombat || regenWindow)   // speed
    speed = PickHigherTier(speed, song);
```

**`inCombat` came from `Body.InCombat`** — a sticky flag that outlives the fight. And the test Minstrel had gone **to melee**, which sets it. So `speed` stayed blocked after the battle.

**`regenWindow` required `HealthPercent >= 70`**, which blocked the group buff exactly when the bot most wanted one — and was wrong in principle: a hurt singer should heal, and `HealGroupGoal` (50) and `EmergencyHealGoal` (100) already outrank the song (45), so the song never needed to police its own health.

**And only speed had a gate at all.** Endurance, mana and health were ungated, so once speed was blocked the `speed ?? endur ?? mana ?? heal` chain fell straight through to whichever resource song the group happened to need. That is precisely the `HealthRegenBuff` → `PowerRegenBuff` shift you saw, and why it never returned to speed.

## The fix

**1. One honest gate, aggro-based, with no health term**

```csharp
public bool CanBuffUpGroup()
{
    if (Body == null || HasAggro) return false;   // something is on me right now
    if (Body.Group == null)          return true;  // solo and calm
    foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
        if (member is MimicNPC mate && mate.MimicBrain != null && mate.MimicBrain.HasAggro)
            return false;                         // a group-mate is still being beaten on
    return true;
}
```

`HasAggro` is `!AggroList.IsEmpty`, and that table empties when enemies actually die — which is what "am I still being attacked" should mean. `Body.InCombat` does not mean that.

**2. It gates the speed song only.** Endurance, mana and health songs stay ungated, exactly as before — you were right that withholding those mid-fight makes a Bard or Minstrel worse precisely when the group needs them.

**3. `GetBestSong` loses two parameters for one** — `!inCombat || regenWindow` becomes `canBuffUpGroup`. The redundant `regenWindow` plumbing is gone.

**4. `IsInRegenWindow()` and `RegenWindowHealthPercent` are deleted**; `TacticalSensor` passes `_brain.CanBuffUpGroup()`.

## The behaviour you should see

| Moment | Result |
|---|---|
| **Solo Minstrel after a fight** | **speed** song — this is the fix |
| In combat, group low on endurance | **endurance** song — unchanged, still available mid-fight |
| In combat, group low on mana | **mana** song — unchanged |
| Anyone hurt mid-fight | **health** song — unchanged, already ungated |
| Aggro clears | speed becomes wanted, outranks the resource songs, and the yield hands over |

That last row is the payoff — no more sitting on a mana song for the whole lull.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR34 | `CanBuffUpGroup()`, `GetBestSong` signature, `IsInRegenWindow` removed |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR32 | call site |
| `Tests/Unit/GoapTests.cs` | PR34 | 2 new tests |

`TacticalSensor.cs` last changed in PR32 — PR33/34 touched neither — so its baseline is PR32.

## Apply order

Full-file snapshots — order matters. PR35 stacks on PR34 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR34 in order …
Copy-Item "$base\Caliburn_PR35_SongGate\*"                    $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **119 total**:

- `SpeedGateIsAggroBasedAndCarriesNoHealthTerm` — the gate exists and takes **no
  arguments**, which is how "no health threshold" is pinned rather than asserted in
  prose; plus the speed > endurance > mana ordering
- `RunningSongHandsBackToSpeedOnceTheFightEnds` — the yield must fire from a
  resource song to speed, and must never trade speed for something weaker

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 119/119 green
```

1. **Solo Minstrel after a fight** → **speed**, not mana
2. **In combat with endurance low** → endurance song still fires — *this is the
   regression to watch; it must not change*
3. Bard unchanged
4. Speed buff stays up through the lull instead of degrading

## Known limits

- A singer that just finished fighting may now wait a beat for its aggro table to
  clear before taking the speed song. That is one tick of hesitation, not a block.
- **Melee kiting** is still open and still the natural next patch — it shares
  `AttackAvailable` and the stance logic with PR32/33.
- The **debuff root cause**, the **`SkillBase` RA log spam** and the
  **traceability list** all remain open.