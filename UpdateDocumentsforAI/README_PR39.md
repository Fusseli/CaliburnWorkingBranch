# PR39 — The song and the melee stance must stop fighting each other (apply ON TOP OF PR38)

## Symptom

Bards and Minstrels behaved strangely — "stuck in a loop, or so". The song animation
played and the bot went back to holding its sword. The weapon flip-flopped between the
instrument and melee.

## Root cause 1: a song on cooldown was offered on every tick

`GetBestSong` picked the best song **by value** and never looked at the recast:

```csharp
foreach (var song in GetSongs())
{
    switch (song.SpellType) { ... speed = PickHigherTier(speed, song); ... }
}
```

A Bard's long instrument song has a **multi-minute** recast. So the SongGoal was planned
at priority 45 on every single tick, and the action then refused it:

```csharp
if (Body.GetSkillDisabledDuration(song) > 0)
{
    log.Debug($"Song: {song.Name} still on cooldown for {MimicBody.Name}");
    return false;
}
```

**The sensor never mirrored that check** — `SongAvailable` was just `songToPlay != null`.
So the goal was planned, the action failed, and the agent fell back to the FSM.

## Root cause 2: the fallback put the instrument away

`MimicState.cs:349-355`:

```csharp
if (_brain.TryGoap(MimicDecisionContext.Combat))
    return;

if (_brain.IsHealer)
    _brain.CheckHeals();
else
    _brain.AttackMostWanted();     // -> PrepareMeleeStance -> SwitchWeapon(Standard)
```

And `MimicNPC.SwitchWeapon` is destructive:

```csharp
// GameServer/custom/MimicNPC/MimicNPC.cs:6794-6807
if (CurrentSpellHandler != null)
{
    StopCurrentSpellcast();          // aborts the in-flight cast
}

foreach (Spell spell in ActivePulseSpells.Values)
{
    if (spell.InstrumentRequirement != 0)
        EffectService.RequestImmediateCancelEffect(...);   // kills the running song
}
```

**Every flip cancelled the in-flight cast AND the running pulse.** So the cycle was:

```
song goal: switch to the instrument  ->  song on cooldown, action fails
   ->  FSM fallback: switch back to melee  ->  cancels everything
   ->  next tick: song goal: switch to the instrument again  ->  ...
```

## Root cause 3: the two ticks of a song were not atomic

PR33 split the song deliberately — one tick to step onto the instrument, the next to
cast — because `SpellHandler.CheckInstrument()` reads the **active** weapon and can
reject a cast made in the same breath that changed it.

But the split left no trace of itself:

```csharp
if (Body.ActiveWeaponSlot != stance)
{
    _pendingSong = null;        // the song is re-derived on the next tick
    Body.SwitchWeapon(stance);
    return true;
}
```

`_pendingSong` was **nulled**, so the song was chosen again on the cast tick from
whatever the group needed *at that moment* — and anything that ran in between could put
the instrument away again.

## The fix

**1. A song on cooldown is not a candidate.** `GetBestSong` skips it, so the next best
song is used instead of the same blocked one:

```csharp
foreach (var song in GetSongs())
{
    if (song == null || IsSongOnCooldown(song))
        continue;
    ...
}
```

**2. The sensor and the action agree.** `SongAvailable` now tests the same thing, so the
goal is not planned when the action would refuse it:

```csharp
SetBool(SongAvailable, songToPlay != null && !_brain.IsSongOnCooldown(songToPlay));
```

**3. The song is chosen on the stance tick and remembered.** `_pendingSong` holds the
song that is about to be played, and a **stance-committed** flag makes the two ticks one
decision:

```csharp
if (Body.ActiveWeaponSlot != stance)
{
    _pendingSong = GetBestSong(...);
    if (_pendingSong == null)
        return false;

    _stanceCommitted = true;
    Body.SwitchWeapon(stance);
    return true;
}
```

**4. Nothing may take the instrument back.** `PrepareMeleeStance` returns early while a
song is committed, and so does the PR31 block in `AttackSelectedTarget` that switches a
Minstrel or a solo Bard to its sword.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR38 | `IsSongOnCooldown`; `GetBestSong` skips a song on cooldown; `_pendingSong` survives the stance tick; `_stanceCommitted`; `PrepareMeleeStance` and the PR31 switch respect it |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR38 | `SongAvailable` tests the recast delay |
| `Tests/Unit/GoapTests.cs` | PR38 | 2 new tests |

## Apply order

Full-file snapshots — order matters. PR39 stacks on PR38 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR38 in order …
Copy-Item "$base\Caliburn_PR39_SongStance\*"                 $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **133 total**, all green.

- `ASongOnCooldownIsNotOfferedToTheBot`
- `TheSongStanceIsCommittedAcrossBothTicks`

The cooldown itself needs a live body (it reads a recast timer), so what is pinned is
the *shape* of the decision: the cooldown belongs to picking the song, and the goal is
not offered a song the action would refuse. The live proof is the checklist below.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 133/133
```

1. **Bard** — the song plays and **stays**. The instrument must not be put away while
   the song is running.
2. **Bard with a long song on cooldown** — it must do something else (heal, cleanse)
   rather than stand there flipping weapons. The log line
   `Song: <name> still on cooldown for <bot>` should appear **once**, not every tick.
3. **Minstrel** — it must be able to sing *and* to swing. The instrument is only put
   away for a real attack, and only when no song is committed.
4. **Watch the weapon** — if it flips between instrument and sword every tick, the
   commit flag is not holding.
5. `/mgoap status` — `Last action: CastSong` should appear once per song, not
   repeatedly.

## Known limits

- **A Bard still does not re-pick a song while one pulses**, unless the running song is
  strictly worse (`ShouldYieldSong`). That is PR34's rule and it is unchanged.
- **The speed song is still gated** on `CanBuffUpGroup` (PR35) — a Bard will not put
  the group speed buff up in the middle of a fight. Unchanged by this patch.
- **The spawn stance is still wrong.** `MimicNPC.SetWeapons` switches to the instrument
  for an `eSpecType.Instrument` build, and twenty lines later undoes it with
  `SwitchWeapon(Standard)` because the class is not `Is2H`. So every Bard and Minstrel
  spawns holding the melee weapon and the first song tick has to switch. Cosmetic, not
  a loop, and not fixed here.
- The control work itself is **PR38**; this patch is only about the song.