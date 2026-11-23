# PR34 — Song priority + regen window + song yielding (apply ON TOP of PR33)

## Symptom

From the PR33 test round:

- Bards and Minstrels **spawn holding the drum** ✓
- `SongGoal` / `CastSong` appears and the **speed song plays on spawn** ✓
- In battle they **switch to melee** ✓
- After battle the **Bard** switched back and played speed — but **"every Minstrel didn't play a speed song after battle. One of them played health regen song but not speed."**

## Root cause 1: the minstrel song sat below calm resurrection

| Goal | Priority |
|---|---|
| EmergencyHeal / HealGroup / ControlAdds | 100 / 50 / 40 |
| Purge, ProtectGroup, BattleRez | 30 / 30 / 25 |
| CureGroup | 20 |
| **CalmRez** | **15** |
| **Minstrel song** | **11** ← loses |
| **Bard song** | **45** ← wins |

Right after a fight there is almost always a corpse to rez, so `ResurrectGoal` at
15 permanently outranked a Minstrel's song at 11. The Bard at 45 sailed past it.
That is the whole asymmetry you saw.

## Root cause 2: `IsInRegenWindow()` was dead weight

PR31 defined it as `!InCombat && !HasAggro && HealthPercent >= 70`. Because it
required `!InCombat`, it could only ever be true when `!inCombat` was *already*
true — and `GetBestSong` gates speed on `!inCombat || regenWindow`, so the
`regenWindow` argument never changed the outcome.

That mattered specifically for Minstrels, because **they went to melee in the
fight**. A bot that swung gets `InCombat` set, and that flag outlives the fight. So
while it lingered, `speed` was never even a candidate and the singer fell through
to health regen.

## Root cause 3: a pulsing song held itself forever

A song sustains itself on its own power, and `IsSongPulsing` blocked every other
song. So the Minstrel that took health regen after a wipe could hold it for the
entire lull and never upgrade to speed.

## What this patch changes

**1. One priority, `SONG_PRIORITY = 45f`, for both singers**

The old split (bard 45 / minstrel 11) rested on a player-class instinct, not on
what these bots can do. `MinstrelSpec` grants only a melee weapon spec,
`Specs.Instruments` and `Specs.Stealth` — **no `Nurture`, no `Regrowth`, no cure
line**. A Minstrel has no heal, cure or rez to displace, so ranking its songs
below resurrection bought nothing and cost the realm its speed buff. The two
constants, the `supportLeaning` constructor flag and the agent's
`SupportLeaningSinger()` are all gone — less to mis-tune. If a Minstrel ever gains
heal or rez specs, split it again.

**2. `IsInRegenWindow()` no longer requires `!InCombat`**

Redefined as what you asked for — *nobody is beating on me, I'm healthy enough to
be worth buffing, and the group is not still fighting*:

```csharp
if (Body == null || HasAggro)          return false;   // something is on me
if (Body.HealthPercent < RegenWindowHealthPercent) return false;
if (Body.Group == null)                return true;    // solo and calm
foreach (member in group) if (member.InCombat) return false;
return true;
```

Aggro is the honest "is something hitting me" signal; `InCombat` is a sticky flag
that outlives the fight. This makes the `regenWindow` argument do real work for
the first time, and covers your *"the whole group, if grouped, is not in combat"*
case.

**3. A running song yields to a strictly better one**

New `SongRank(eSpellType)` — 0 speed, 1 endurance, 2 mana, 3 health — matching the
order `GetBestSong` always preferred. `ActivePulseSpells` is keyed by `eSpellType`,
so the running song's rank is the dictionary key. `ShouldYieldSong` is **strictly**
better, so a good buff is never churned. Cancellation uses the same call
`MimicNPC.SwitchWeapon` uses to break a pulse.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR33 | `SongRank`, `ShouldYieldSong`, `GetPulsingSong`, `StopPulsingSong`, new `IsInRegenWindow`, yielding in `ExecuteGoapSong` |
| `GameServer/custom/MimicNPC/ReGoap/Goals/SongGoal.cs` | PR31 | single `SONG_PRIORITY`, bool flag dropped |
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | PR31 | `SupportLeaningSinger()` removed |
| `Tests/Unit/GoapTests.cs` | PR33 | 2 new tests, 1 rewritten |

`TacticalSensor.cs` needs **no** change — the regen-window call site is unchanged.

## Apply order

Full-file snapshots — order matters. PR34 stacks on PR33 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR33 in order …
Copy-Item "$base\Caliburn_PR34_SongPriority\*"               $repo -Recurse -Force
```

Full server restart.

## Tests

**117 total** (115 + 2 new, one rewritten — not the 118 I predicted).

- `SongRankingPutsSpeedFirstAndHealingLast` — rewritten to assert the real
  `SongRank` ordering, including that a non-song ranks `int.MaxValue` so it can
  neither be chosen nor interrupt a real song
- `SongPriorityBeatsCalmRezButNeverHeals` — pins the single priority above
  cures/rez, below group heal and emergency heal
- `RunningSongOnlyYieldsToAStrictlyBetterOne` — the churn guard: strictly better
  yields, equal does not

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 117/117 green
```

1. **Minstrel after a fight** → **speed** song, not health regen
2. Speed buff **stays up** through the lull
3. **Bard** unchanged — still songs, still heals (heal at 50 still wins)
4. A Minstrel at low health after a wipe → health song may take over, then should
   **hand back to speed** once the group recovers. That is the yield logic
   working, not a regression
5. Watch for visual churn: a pulsing song *can* now be cancelled mid-effect

## Known limits

- A song may now be interrupted mid-effect. That is the cost of the yield, and it
  is bounded — strictly-better only, so a correct song is never churned.
- **Melee kiting** is still open and is still the natural next patch: it shares
  `AttackAvailable` and the stance logic with PR32/33.
- The **debuff root cause**, the **`SkillBase` RA log spam** and the
  **traceability list** all remain open.
