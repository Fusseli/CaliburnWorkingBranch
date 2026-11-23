# Handover: saveable bots (PR20a–PR20c)

Companion to `HANDOVER.md`, `HANDOVER_Archery.md`, `HANDOVER_Rez.md`
(PR16–PR18), `HANDOVER_RA.md` (PR19a–PR19b) and `SESSION_SUMMARY.md`. Same
conventions: stacked drop-in folders, each only its own delta, verified in
temp overlays (including isolated intermediate stages) before delivery.

Context: players can tame wild mimic bots into a persistent personal stable
(10 per realm per account, 30 total), resummon up to 7 of them at once, and
level 1–50 + RvR with them across sessions. Tame model: saving removes the
wild bot from the world and *moves* its gear into storage — items can never
duplicate. Spec points stay auto-distributed (no trainer UI exists for bots);
ML/Champion columns are stored from day one so later phases need no migration.
Artifacts skipped by user decision.

## The patches (copy in order, each over the previous, test between steps)

```powershell
Copy-Item "<Folders>\Caliburn_PR20a_SaveStore\*" "<YourCaliburn>\" -Recurse -Force
# ... then PR20b_Summon, PR20c_DeathGuards
```

| # | Folder | Files | What it adds |
|---|---|---|---|
| PR20a | `Caliburn_PR20a_SaveStore` | 5 | Storage. New `MimicSave` table (account/realm/slot, name, class, level, XP, serialized specs + RAs, rank, ML/Champion fields, appearance, spec variant). `MimicSaveManager`: snapshot/restore, pair-CSV codec, slot/name/region-gate helpers, gear move via the shared `Inventory` table under a stable storage key. `/msave [newname]` (tames: snapshot + gear moves + wild bot despawns), `/mbots` (stable list), `/mdelete <slot\|name>` (destroys row + stored gear). |
| PR20b | `Caliburn_PR20b_Summon` | 4 | Summoning. `/mcall <slot\|name>` (region gate: RvR = own realm only, else any realm; max 7 active saved bots per account; already-out bot hurries to you instead). `Instantiate` rebuilds the bot (specs + RAs restored, stored gear re-equipped else fresh spawn gear, full HP, joins your group when realms allow, one live instance per row). `/mdismiss` writes back + despawns. Logout (`GamePlayer.Quit`, 1 line) and orderly shutdown (`[GameServerStoppedEvent]`, no engine edit) dismiss all actives. |
| PR20c | `Caliburn_PR20c_DeathGuards` | 4 | Death + guards. Corpse expiry (`ReleaseCorpse`) returns saved bots to storage instead of spawning a replacement; owned bots grant kill credit/RP but **drop no loot** (else kill/resummon farms). Active-instance registry self-heals (bots deleted by other paths stop blocking resummon). `/mdelete` refuses live bots; `/msave` on your own active bot refreshes its snapshot (gear upgrades, levels) without despawning. |

Commands: `/msave [newname]`, `/mbots`, `/mcall <slot|name>`, `/mdismiss`,
`/mdelete <slot|name>`. No clashes with existing `/m*` commands.

End state: 19 goals / 18 actions / 6 sensors (unchanged — no GOAP in this
stack). Tests: **85/85** (`GoapTests|RoleElectionTests`: 76 pre-save + 4
PR20a + 4 PR20b-cases-as-1-method… precisely: 76 → 83 → 84 → 85).

## Verification (build machine, temp overlays)

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# PR20a stage: expect 83/83; PR20b stage: expect 84/84; full chain: expect 85/85
```

Unit-subset parity (`--filter 'FullyQualifiedName~Unit'`) TRX-compared vs clean
base: identical 44 pre-existing spell-math failures on both sides. PR19a,
PR20a and PR20b were each additionally verified as isolated stages
(base+stack+folder: 0 errors, green). Final clean-room re-apply PR1–PR20c:
0 errors, 85/85. (Full unfiltered `dotnet test` stays flaky DB-less —
scheduler-host crash, also on pristine base; use the Unit subset.)

## Live-test protocol (one round per folder)

1. **PR20a:** target a wild bot → `/msave` (try a rename too) → bot gone from
   world, `/mbots` lists it with class/level/rank. `/mdelete` removes it
   (row + gear). Realm stable caps at 10 (11th refused); duplicate names per
   realm refused; corpses and other players' bots refused.
2. **PR20b:** `/mcall` → bot appears at your side with its gear, joins your
   group. Level it: XP/specs/rank accrue. `/mdismiss` → gone; `/mcall` again
   → progress kept. Logout with bots out → relog → `/mbots` shows kept
   progress, nothing stranded in world. RvR zone: foreign-realm bot refused,
   own-realm OK; PvE zone: any realm OK. 8th simultaneous summon refused.
3. **PR20c:** let a saved bot die → corpse window → it returns to storage
   (no replacement spawns, no loot on the ground, killer still credited).
   Resummon keeps everything. `/mdelete` on a live bot refused. `/msave` on
   your active bot refreshes (check via `/mbots` after gearing it up).

## Needs eyes (cannot verify without a server+client)

- First real save/summon cycle against MariaDB (table auto-creates on boot;
  confirm `MimicSave` appears and rows round-trip, including relog).
- Stored-gear restore: slots land where they were (paperdoll + backpack).
- Group join on summon in all cases (solo owner, full group of 8, cross-realm
  in PvE — refused only where the rules say so).
- Shutdown-with-bots-out: rows must hold session progress (watch the
  `OnServerStopped` dismiss in the log).
- `AddObject` with a nulled `ObjectId` (gear move path) — if the DB layer
  rejects it, the live log will show it on first `/msave` of a geared bot.

## Known limits (accepted)

- No level cap on summoning (sandbox by design); 7 active + group-of-8 cap
  are the only brakes. Tick cost per bot is unchanged from wild bots.
- Buffs/effects, quests, money on the bot, and house/guild state do not
  persist (fresh spawn state + rebuff via group on summon).
- Crash (not orderly shutdown) loses progress since the last dismiss/logout.
- Name rules are simple (3–20 letters/apostrophe/hyphen), not the full player
  invalid-names check.
- ML/CL values store 0 until their phases wire the live properties.

## Field data protocol

Per incident paste back, numbered: PR folder applied, command + exact reply
text, `/mbots` output, solo/grouped, zone (RvR?), and ~20 log lines around it.
