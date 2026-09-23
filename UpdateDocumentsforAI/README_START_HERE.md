# START HERE — mimic stack index

You are looking at the finished mimic-bot work for a Caliburn (OpenDAoC)
checkout. Base = `Fusseli/CaliburnWorkingBranch` `master @ 660394d`
(pre-PR state). Everything below ships as stacked drop-in folders over that
base. Suggested read order for this file first, then the docs, then the code.

## 1. Read order

1. `SESSION_SUMMARY.md` — the build log: base facts, full folder table,
   verification method (use it for every future patch), doctrines (keep
   them), open threads, one section per work stack.
2. Topic handovers (test + live protocols, needs-eyes lists):
   - `HANDOVER.md` — base tactical GOAP stack (PR1–PR10).
   - `HANDOVER_Archery.md` — archery diagnosis + fixes (PR11–PR13).
   - `HANDOVER_Issues.md` — live triage: freeze, long ticks, stale display.
   - `HANDOVER_Rez.md` — rezzable bots (PR16–PR18).
   - `HANDOVER_RA.md` — realm abilities for bots (PR19a–PR19b).
   - `HANDOVER_Savebots.md` — persistent companion bots (PR20a–PR20c).
   - PR14 (kite rework), PR15 (diagnostics) and PR21 (help window + priv
     split) are covered in `SESSION_SUMMARY.md` only — they are small.
3. Folders `Caliburn_PR*_ *` — the code. Each mirrors repo structure and holds
   only its own delta vs the previous stage.

## 2. The chain (apply in this order, test between steps)

```powershell
$order = @("Caliburn_PR1_GOAP","Caliburn_PR2_Interrupt","Caliburn_PR3_Assist",
"Caliburn_PR4_Peel","Caliburn_PR5_Guard","Caliburn_PR6_Survival","Caliburn_PR6b_Roles",
"Caliburn_PR7_Caller","Caliburn_PR8_Debuff","Caliburn_PR9_Positional","Caliburn_PR10_Docs",
"Caliburn_PR11_ArcheryFix","Caliburn_PR12_OldArchery","Caliburn_PR13_ArrowType",
"Caliburn_PR14_KiteFix","Caliburn_PR15_Diagnostics","Caliburn_PR16_RezCorpse",
"Caliburn_PR17_GroupRez","Caliburn_PR17b_OutsideRez","Caliburn_PR18_BattleRez",
"Caliburn_PR19a_RAbuyer","Caliburn_PR19b_Purge","Caliburn_PR20a_SaveStore",
"Caliburn_PR20b_Summon","Caliburn_PR20c_DeathGuards","Caliburn_PR21_Help")
foreach ($p in $order) { Copy-Item "<Folders>\$p\*" "<YourCaliburn>\" -Recurse -Force }
```

What each stage is (one line; details in the summary/handovers): PR1 hybrid
GOAP + FSM fallback → PR2 interrupt → PR3 assist train → PR4 tank peel →
PR5 auto-guard (+manual `/mguard`) → PR6 kite+quickcast → PR6b role election
→ PR7 target caller → PR8 debuff-first → PR9 flank walk → PR10 docs →
PR11 archery spec fix → PR12 old-flag specials → PR13 arrow-type choice →
PR14 kite rework → PR15 diagnostics → PR16 corpse linger → PR17 group rez →
PR17b stranger rez (opt-in) → PR18 battle rez → PR19a RA auto-buyer + `/mra`
→ PR19b self-purge → PR20a bot storage + `/msave`/`/mbots`/`/mdelete` →
PR20b `/mcall`/`/mdismiss` + logout/shutdown save → PR20c death-to-storage →
PR21 `/mimic` help + GM priv split (`/mcreate`/`/mspawner`/`/mgroup`/`/mbattle`).

End state: 19 goals / 18 actions / 6 sensors; `/mgoap` diagnostics;
65+ tests green (see below).

## 3. Verify (repeat for every future patch)

Temp overlay (never the working repo), then:

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect all green (86/86 at PR21)
```

Parity: `--filter 'FullyQualifiedName~Unit'` TRX-compared vs clean base —
identical 44 pre-existing spell-math failures on both sides means your patch
broke nothing. A full unfiltered `dotnet test` is flaky without a database
(scheduler-host crash, also on pristine base) — use the Unit subset.

## 4. What to look at per area

- GOAP behavior table: `GameServer/custom/MimicNPC/ReGoap/README.md`
  (in the stack; PR21 version is latest).
- Bot commands in game: `/mimic` (priv-split help window).
- Live protocols + needs-eyes: the `HANDOVER_*.md` files above.
- Open threads: `SESSION_SUMMARY.md` "Open threads" (freeze, long ticks,
  archery measure) plus per-handover limits (RA spec-gate, Atlas table,
  savebots DB round-trip, corpse pose).
- Conventions that must survive: one tick of work per decision, fail closed
  to FSM, every behavior = sensor + goal + action + test, manual commands
  always win, never register a goal without all three + test, small stacked
  folders verified in overlay before delivery.
