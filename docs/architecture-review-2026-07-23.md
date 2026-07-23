# Architecture review — 2026-07-23 (branch 0.6.1)

Deepening candidates from `/improve-codebase-architecture`. Vocabulary: **module / interface / deep / shallow / seam / adapter / leverage / locality** (see `/codebase-design`). Nothing implemented yet — this is a resume point.

## Candidates (ranked)

### 1 · Collapse the three parallel `BotOptions` initializers — **Strong**

Files: `src/TbotUltra.Core/Configuration/{BotOptions.cs, BotOptionsFactory.cs, BotOptionsPayloadApplier.cs, BotOptionPayloadKeys.cs}` (+ 9 domain sub-appliers).

- One setting must appear in 5–7 sites: property + `[ConfigurationKeyName]`, payload key const, `Factory.FromConfiguration`, `Factory.CloneWithOverrides`, `PayloadApplier.Apply`, and the domain sub-applier (≈4 internal sites).
- Three hand-maintained parallel object-initializers of ~199 / ~194 / ~199 lines. 199 properties, 250 keys.
- **Shallow**: the interface (add a setting) is as complex as the implementation (touch it everywhere). Miss one site → the setting silently keeps its C# default. No compile error, no test failure.
- **Live bug (uncaught today):** `NpcTradeBuildTimeLimitEnabled` / `NpcTradeBuildTimeLimitSeconds` have property + key + applier but are absent from **both** factory initializers → never loaded from `bot.json`, reset on every `CloneWithOverrides`.
- No test reflects over all properties, so a missed mapping site passes CI.

Deepen: one `SettingDescriptor` per setting (key · default · read · copy · from-payload); the three initializers iterate descriptors instead of naming fields. One round-trip test over the descriptor set guards every future setting.

### 2 · Extract a deep `SleepCycle` module from the pacing host — **Strong**

Files: `src/TbotUltra.Desktop/MainWindow.SessionPacing.cs` (1018), `MainWindow.Session.cs` (1129), `MainWindow.Freeze.cs` (111), `src/TbotUltra.Desktop/Services/Orchestration/SessionPacer.cs` (833).

- `SessionPacer` is the **exemplar deep module**: 21 private state fields, small verb interface, injected `Func<DateTimeOffset> now` clock, 23 unit tests, no WPF/browser refs.
- The sleep→wake orchestration and its restore snapshot (`_wasLoggedInBeforeSleep`, `_wasContinuousLoopRunningBeforeSleep`, `_wasQueueAutoRunningBeforeSleep`) leak across four `MainWindow` partials (SessionPacing, Session, Freeze, ContinuousLoop) as loose flags on `this`. Untestable.

Deepen: a `SleepCycle` module owns the snapshot + freeze gate; browser/loop operations enter as injected interfaces (`IBrowserOps`, `ILoopOps`) — two adapters (WPF in prod, fake in tests) justify the seam. Bounded, testable slice of candidate 3, built on the SessionPacer pattern.

### 3 · Deepen the `MainWindow` monolith, one concept at a time — **Worth exploring**

73 `MainWindow*.cs` partials, 30,383 lines, one `partial class`, ~283 mutable fields on `this`, ~1,129 methods, **0 instance tests** (needs a live WPF Window + DI + Playwright; only `static` helpers are reachable).

Concepts that force bouncing across partials: session pause/pacing, village/account switch, queue execution. Deepen incrementally — lift cohesive concepts (SleepCycle, VillageSwitch, QueueExecution) into deep modules that own their state; leave `MainWindow` a thin wiring shell. Carve, don't rewrite; one extraction per PR. Large effort — sequence behind 1 & 2.

### 4 · Shrink the `TravianClient` interface — **Speculative**

56 partials, 27,470 lines, one `sealed partial class`, ~278 public members but only ~53 behind its 6 interfaces; ctor takes a Playwright `IPage` + 12 callbacks. 0 instance tests.

Internals **already do the right thing**: ~25 pure, well-tested classes (parsers, decisions, calculators). Only the interface is shallow: push the ~225 public automation methods behind the 6 ports (Send Troops, Map Oasis Scan, …); fold the 12 ctor callbacks into one injected options/dependency object. Low urgency.

> Note: an earlier hunch about *duplicate* `TravianClient` partial paths (`Automation/Hero/…` vs `Automation/…`) was **false** — that was a completed move into feature folders, no dead copies.

## Top recommendation

**Candidate 1** — highest leverage, lowest risk: contained to one Core folder, no WPF/browser, pays back on every future setting, and fixes the `NpcTradeBuildTimeLimit*` bug by construction. **Candidate 2** is the natural next step.

## Test-coverage context

~1,050 test methods across `TbotUltra.Worker.Tests` (~514) and `TbotUltra.Desktop.Tests` (~537), almost all on pure parsers/calculators/decisions/stores. The three large stateful orchestrators (MainWindow, TravianClient, the session/pacing host) are sealed off from unit testing by their WPF-Window / Playwright-`IPage` construction. `SessionPacer` is the one deep orchestrator that is tested.
