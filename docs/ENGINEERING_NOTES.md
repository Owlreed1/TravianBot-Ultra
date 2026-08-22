# Engineering Notes

Last updated: 2026-07-31

Read this file before changing architecture, selectors, paths, browser behavior, persisted state, queueing,
or server logic. Keep it short and current: durable rules belong here; detailed decisions belong in ADRs;
implementation history belongs in `docs/history/`.

## Project overview

Tbot Ultra is an Official Travian automation desktop application.

| Project | Responsibility |
|---|---|
| `TbotUltra.Core` | Domain models, parsers, calculators, configuration, queue logic |
| `TbotUltra.Worker` | Browser automation, Travian client, orchestration, diagnostics |
| `TbotUltra.Desktop` | WPF UI, ViewModels, dialogs, presentation services |

Dependency direction is Desktop -> Worker -> Core. Core must not depend on Worker or Desktop.

Build and test:

```powershell
dotnet build TbotUltra.sln -c Release --disable-build-servers -m:1 -p:MSBuildEnableWorkloadResolver=false -p:UseSharedCompilation=false -p:BuildInParallel=false
dotnet test TbotUltra.sln -c Release --no-build --disable-build-servers -m:1 -p:MSBuildEnableWorkloadResolver=false -p:UseSharedCompilation=false -p:BuildInParallel=false
```

The solution uses no optional .NET workloads. Keep `MSBuildEnableWorkloadResolver=false` in build
and test commands so a partial machine-wide workload installation cannot block the Desktop project.
The local SDK also requires disabled build servers and a serial solution build; use the commands above
or the repository verification scripts rather than a plain parallel `dotnet build`.

Published artifacts belong under `artifacts/`, never beside source files.

## Active architecture rules

- Official Travian is the only supported server flavor. Do not add SS-Travi, legacy selector fallbacks, or
  runtime flavor switching. Historical flavor-aware branches are archived, not active conventions.
- Keep parsers and calculators pure where possible and cover them with focused tests.
- Keep `TravianClient` methods thin: navigation/clicking plus delegation to parsers or handlers.
- Preserve working navigation and click sequences unless the task explicitly changes them.
- Prefer handler dictionaries for gid/type behavior instead of growing switch chains.
- Desktop calls Worker through explicit interfaces and ViewModels; calculations do not belong in code-behind.
- `LoopController` owns loop lifecycle and cancellation. UI code must not create competing loop state.
- Long-running UI commands use the shared busy/guard pattern, expose Cancel when supported, and restore UI
  state in `finally`.

## Official-only paths and selectors

- Build URLs through existing path helpers. Paths are server-root relative, never relative to an account
  base-URL subdirectory. Normalize the base URL and preserve escaped query strings.
- Current-page matching requires the same path and every query parameter supplied by the target helper;
  server-added parameters may be extra. Never identify all `/build.php` URLs as the same slot.
- Common paths are `/dorf1.php`, `/dorf2.php`, `/build.php?id={slot}`, `/karte.php`, `/berichte.php`, and
  `/messages.php`.
- Scope selectors to the relevant Official page, widget, dialog, row, or building contract.
- Prefer stable attributes and semantic structure over generated class names.
- Selector changes are additive only for verified Official DOM variants. Do not add broad legacy fallbacks or
  replace a verified selector without evidence.
- Verify selector changes against live Official HTML or a captured fixture. React elements must be visible and
  actionable, and dialog actions must be scoped to the open dialog.
- State-changing clicks must be exact. Navigation retry does not permit repeating an action.
- Prefer trusted Playwright clicks for visible classic buttons. Synthetic dispatch is an actionability fallback
  or a tool for genuine React/hidden controls. Preserve the farm-list real-click-with-JS-fallback pattern.
- React inputs may require native value assignment plus `input`/`change` events.
- Numeric parsing must handle locale separators, Unicode minus, and bidirectional markers.
- The account server picker loads active and upcoming non-standard Official worlds from Travian Lobby's public
  `/api/metadata` and `/api/calendar`. Treat any published `.travian.com` URL whose type is not `normal`, or
  whose host does not match the regular regional `ts{N}.x{speed}.{region}` scheme, as `Special`; ignore entries
  without a URL.
- `Special` is the first picker group. Hide a matching user-added duplicate while discovery is available, but
  keep the persisted custom entry so it remains usable if the calendar cannot be reached later.

## Configuration and persisted state

- `bot.json` is application-wide; account settings are account-scoped; village settings and queue state are
  village-scoped; runtime snapshots are Worker-owned observations, not user configuration.
- Use the existing path provider. Never derive data paths from the executable working directory.
- `ProjectRootLocator` uses the versioned solution file in source/CI and `config/bot.json` in deployed runtime;
  source tests must not depend on ignored runtime configuration.
- Interruptible writes use the atomic file helper. Retry bounded transient lock/sharing failures.
- Quarantine and log corrupt queue/state files instead of silently overwriting them.
- New settings require the complete pipeline: model, defaults, load/save, ViewModel, UI, and tests.
- Demolition is a village-scoped queue group: start one Official `table#demolish` step, persist the server timer plus its random delay as `NextAttemptAt`, and never poll or sleep through it in the browser.
- Persist village identity by coordinates/key, not display name. Names may collide or change; queue items retain
  their target village identity.
- Duplicate village names are valid. Fresh Official sidebar `data-did` plus `.coordinateX/.coordinateY` values
  are authoritative: never deduplicate by name, never overwrite fresh coordinates from a name-keyed cache, and
  never accept a same-name village switch without coordinate verification when coordinates are available.
- The village status cache and queue use canonical coordinate keys. Legacy name-keyed entries are migrated only
  when coordinates can be resolved; active coordinates come from `#villageName[data-x][data-y]`.
- Per-village runtime caches are shared by the UI and background loop and must be synchronized. A display-name
  lookup is valid only when exactly one cached village has that name; duplicate names never use last-write-wins.
- Queue status transitions are gated. `MarkDeferred` accepts only RUNNING items; Pending items use
  `UpdateDeferred`/`UpdatePending`. Check the returned boolean.
- New villages default to Auto enabled. The version-1 migration enables existing villages once; later manual
  Auto-off choices persist.

## Timing, cancellation, proxy and logging

- Normalize invalid timing ranges so minimum never exceeds maximum.
- Pass the active cancellation token through every cancellable operation. Never replace it with
  `CancellationToken.None`; cancellation is expected control flow, not an alarm.
- Sleeping/paused state must preserve work and must not start a competing loop.
- Entering sleep closes the active browser session, including planned sleep entered before login.
- Continuous-loop wake requests from saved settings or newly enabled automation must also end an active idle break;
  humanized idle pacing must not delay newly requested work.
- A ready enabled task always wins over holding the current village for an imminent deferred task. The account-scoped
  Pacing setting `short_village_defer_seconds` may be 20, 60, or 90 seconds (default 60) and applies only when no task
  is ready in any village; changing it while the continuous loop runs requests a wake at the next safe boundary.
- Continuous Loop and Auto Queue share runtime-only village batching over the account queue: ready work is drained
  across groups in the verified browser village before normal work elsewhere. Ready Account work or `Priority > 0`
  may preempt; after 10 execution attempts another ready village gets a turn. Deferred/unknown work never keeps a
  batch alive, and preview/forecast selection must not mutate the batch owner or attempt count.
- Applying Session pacing settings while automation is active must take effect immediately: enabling starts its run
  timer, while disabling a scheduled sleep resumes the captured automation state.
- Raising or disabling Daily max while sleeping for the old daily limit must re-evaluate the restriction immediately.
  If the recorded runtime is below the new limit and Allowed hours permit running, wake with zero added sleep delay.
- Known queue deadlines are authoritative and may not be shortened by pacing.
- Action pacing is mandatory. Persisted configuration and incoming payloads may change its delay ranges but may
  not disable it. The manual Catapult wave tab burst is the only exception: it uses only its explicitly selected
  50–500 ms tab delay between clicks and does not apply general human/action pacing while filling its form.
- Proxy settings are account-scoped. Browser, HTTP client, tests, and bonus video use the same effective route.
  Never log credentials or place them in user-visible URLs.
- Retry only transient failures with bounded attempts. Apply configured pacing; do not add unbounded sleeps.
- Alarms represent actionable failures. Expected waiting/blocking and an explicitly retrying bounded transient
  attempt are normal status. Deduplicate identical alarms for 30 minutes; repeated occurrences update visible
  count without another alarm line.
- Build-estimate server-speed detection accepts both `5x` and lobby-style `X5` names. Before the account has a
  verified login, missing speed is expected and silently uses 1x; only an unparseable logged-in account alarms.
- Detailed browser logging is development-only and off by default. Trace semantic operations, emit exactly one
  end event per flow, and sanitize all secrets. Navigation/mutations use the traced adapters.
- Bonus-video audio muting is best-effort and retried during playback polling because provider controls may render
  after play starts; a missing, detached, or unactionable audio control must never fail the video flow.

## Browser, login and account access

- Validate bundled Chromium by its exact Playwright revision and executable, but do not hard-code the Windows
  archive directory name; supported Playwright versions have used both `chrome-win` and `chrome-win64`.
- Never install or ship `chromium_headless_shell` (~270 MB). Headless game automation does not exist; install with
  `install chromium --no-shell`, and the cleanup removes the shell folder at ANY revision. Any internal headless
  launch (currently only the proxy IP check) MUST set `Channel = "chromium"` — a plain `Headless = true` resolves
  to the shell and fails with "Executable doesn't exist at ...chromium_headless_shell-<rev>...".
- The session runs the user's system Chrome/Edge (`Channel` from `ResolveInstalledChromeChannel`) for the H.264/AAC
  codecs bonus videos need; bundled Chromium is only the fallback. Do not add a browser "warmup" launch — it warms
  a binary the session does not run. Bot-launched browser processes are therefore indistinguishable from the user's
  own by name or path: orphan cleanup MUST go through `LaunchedBrowserRegistry` (PID + start time + exe path, all
  three must match), never by process name or executable path alone.
- `DOMContentLoaded` is sufficient only when followed by a required page-marker check.
- Full login starts in the Travian lobby and enters the owned world through SSO; never submit credentials to the
  configured game server or add direct-server fallback.
- Preserve filtered SSO state only in in-app session transitions. Real process startup and user exit clear every
  account's saved Playwright auth state.
- After Play now commits navigation to an Official game origin, rotate immediately to the clean in-app context that
  blocks the consent/ad stack. State filtering and the replacement context must use the resolved runtime game origin,
  not a stale configured base URL, so the selected world's SSO cookies survive the rotation.
- The Official mobile-version dialog can appear after Play now's first navigation wait expires. After confirming it
  with both mobile options off, wait again for the game origin before treating the current lobby URL as a failed SSO.
- Preserve the intentional headed/maximized anti-detection setup and `ViewportSize.NoViewport`.
- Login automation requires English UI and fails clearly when required markers are missing.
- The one-time Gold Shop offer is a blocking announcement, not an automation action. Dismiss it after game-page
  navigation/reload only through the visible `data-context="oneTimeOfferAnnouncement"` dialog; never use a broad
  dialog-close selector.
- Synchronize `BotOptions.BaseUrl` from the active account before login and fail fast when their normalized origins
  differ. An account switch invalidates the browser-session generation so a late `OpenPageAsync` cannot resurrect
  the previous account after shutdown.
- Account-picker changes require confirmation only while both authenticated and backed by an open browser session;
  logged-out/no-browser changes switch saved account immediately without a dialog.
- Lobby world matching treats speed labels (`x3`, etc.) as optional display metadata but rejects an explicit
  conflicting speed. If neither cached wuid nor automatic name/host matching reaches the configured origin,
  interactive login shows every owned lobby world as selectable cards. The lobby-owned list is authoritative:
  after a manual choice reaches an authenticated Official game origin, atomically update that account's server name
  and URL in Manage and sync the active runtime config. A failed selection reopens the picker with remaining worlds;
  persist the selected wuid and any server correction only after authenticated game-page verification.
- A manually selected lobby world may temporarily differ from `BotOptions.BaseUrl` until verification persists the
  correction. During that login flow, every navigation, URL resolution, and server-keyed cache must use the resolved
  game origin rather than the stale configured base URL.
- A recent-login cache hit is valid only on the configured game origin, never on lobby/login URLs, and still probes
  explicit restriction/challenge signals before skipping the full login check.
- Account `.env` mutations hold one shared per-file read-modify-write lock and use atomic replacement. New values are
  JSON-quoted so passwords round-trip spaces, quotes, backslashes, equals signs, hashes, and newlines; legacy values
  remain readable. New account keys add a stable identity hash and stores reject cross-identity overwrites.
- Account-analysis field updates are atomic per account/world; World UID, village, tribe, Gold Club, and settings
  writers must merge inside `AccountAnalysisStore.Update`, never load then save independently.
- Official special-server discovery routes through the active account proxy, never falls back to direct traffic when
  `NeverUseOwnIp` is enabled, isolates malformed source payloads, and uses a seven-day atomically written last-known-good
  cache when live sources are unavailable.
- Account holds are account-specific: a verified ban, restriction, challenge, or repeated unknown state stops only
  that account and preserves its queue/settings until manual re-enable. A Travian punishment page is evidence only:
  never click its Agree or Contact Support controls, and leave the browser open for manual review. Treat the
  Official sidebar ban warning plus its `/dorf1.php?action=stop` details link as an equivalent hard-stop signal;
  the jittered current-page refresh must turn it into an account hold and stop the rest of that refresh tick.
  Active task page-ready and resource-retry paths must probe the same signals before waiting or reloading, so a
  punishment response cannot spend tens of seconds in missing-widget recovery before the account hold is raised.
- A verified ban captures the last durable village structure once. After manual re-enable, the first Start bot runs
  a full dorf1+dorf2 recovery scan in strict read-only mode before any task generation, reward collection, queue
  reconciliation, construction-fill arming, or execution. Normal automation may resume only after the user's recovery choice.
- Detailed lifecycle, SSO, cleanup, and access rules: [browser/session ADR](adr/2026-07-18-browser-session-and-login.md).

## Feature implementation conventions

- Keep the WPF dispatcher limited to bounded presentation work: recurring ticks update countdowns from cached
  projections, expensive queue/overview calculations run from immutable snapshots, and persisted cache writes run
  through a serial latest-snapshot writer. Queue display refreshes are read-only; history is projected only when shown.

### Core and Worker

- Parse HTML/JSON into domain models before scheduling decisions.
- Map Oasis scan planning, filtering, pacing, retry, checkpoints, and results belong to `MapOasisScanOperation`; `TravianClient` only prepares the Official map page and reads map areas.
- Send Troops navigation and Rally Point-level checks route through `IRallyPointNavigator`; Catapult, Reinforcements, and Farm Lists keep their own action flows.
- Put resource, time, capacity, prerequisite, and queue calculations in Core.
- Worker owns browser interaction, timeouts, retries, cancellation, and operational logging.
- Prefer explicit result types for expected unavailable, deferred, and blocked states.
- Log account, village, operation, and failure stage without exposing secrets.

### Desktop

- UI text is English. Reuse theme resources and controls; do not hard-code near-match colors.
- The Settings window is category-tabbed: General (including post-login automation), Pacing, Construction, Hero,
  Farming, Troops, Celebrations, and NPC / Trade. Town Hall per-village/queue controls belong under Celebrations;
  account-wide Gold/Silver limits belong under NPC / Trade. Town Hall and Brewery restart delays include the
  configured random delay after the live celebration timer; a confirmed missing Town Hall disables that village's
  Town Hall group instead of deferring an impossible task.
- Every editable numeric Settings field is validated before any config mutation. Decimal input uses invariant
  culture and requires a period; invalid format, out-of-range values, and Max below Min block Save/Sleep now with
  a warning focused on the offending field instead of silently substituting or clamping a value.
- Gold/Silver spending has two independent guards: a minimum remaining balance and a daily spending budget.
  Daily totals reset at 00:00 server time and persist per account/server so restart cannot reset the allowance.
- Hero, Town Hall, Brewery, and Smithy restart delays are independently toggleable and enabled by default. Hero
  reuses one session deadline after returning home or discovering a new adventure. Smithy delays only after an
  occupied queue slot frees; an empty queue starts immediately and Plus slots are filled together without delay.
- Optional per-building troop minimum ranges are village-scoped. Randomize one threshold per building/run and
  evaluate it from the current village resource snapshot plus the Official unit-cost catalog before navigating;
  recheck live costs and resources before submit, and alarm/skip on a catalog-to-live cost mismatch.
- Hero HP regeneration per day is only a scheduling estimate for low-HP adventure defers. A successful current-page
  HP read is authoritative and releases the deferred Hero task immediately once the threshold is met. That release
  is centralized in the shared UI HP-read helper, so login, quick re-login, browser restart, the manual refresh
  button, and the periodic tick all clear a stale regen-estimate countdown, not just the background tick.
- Hero crop anti-starve is account-configured but selected per coordinate-keyed village and runs only while the
  continuous bot is Running. A missing per-village entry defaults enabled; the account master defaults disabled.
  It is observation-driven: trusted resource snapshots from the existing jitter read and village scan cancel the
  action for non-negative production or schedule a local no-browser deadline for negative production. Only when
  that deadline reaches the configured trigger may one deduplicated live-confirmation task enter the queue; never
  create permanent per-village polling tasks. The live confirmation uses dorf1 stock/production. Transfers
  navigate through `/hero/inventory`, open the visible `.heroItem` containing `.item.item148`, fill only
  `input[name="crop"]`, and click the enabled dialog action whose normalized text is exactly `Transfer` (never
  `Transfer maximum`). The configured minimum hero crop is an absolute post-transfer reserve: transferable crop is
  at most `hero crop - minimum remaining`, in addition to the per-transfer maximum and granary free capacity.
  Post-transfer ETA verification allows 60 seconds of observation drift; actual transfer limits or failed stock
  verification still raise the anti-starve alarm.
- Account-wide construction behavior, including storage look-ahead and construction start delay, belongs in the
  Construction settings category rather than the Buildings workspace.
- Secondary explanations use the shared `i` tooltip when permanent text wastes space.
- Disable duplicate commands while running; marshal observable collections through the dispatcher.
- Marshal to the UI thread via the shared `MainWindow` helpers: `RunOnUi` (blocking) or `RunOrPostToUi`
  (fire-and-forget off-thread); do not hand-roll new `CheckAccess` guards with matching semantics.
- Manual operations matching the canonical begin/busy/complete/paused/fail shape go through
  `RunGuardedOperationAsync`; flows with extra state, dialogs, or custom cancel handling keep explicit blocks.
- Keep `DataGrid.RowHeight` unset or `Double.NaN`; the string `Auto` is not a WPF `Double`.
- Queue Active/History grids use star sizing with explicit per-column `MinWidth` and disabled user resizing in both
  the embedded panel and Pop out. A narrow viewport must scroll horizontally; never allow a header drag or an early
  hidden-tab measurement to collapse queued task columns into apparently blank rows.
- Enumerate mutable collections through immutable snapshots when sanitizing/exporting.
- Village Overview is read-only and uses cache/queue snapshots; opening it never navigates or scans.
- Overview projections show only real deadlines and never mutate queue or scheduler state.
- The Dashboard status line prioritizes a running queue item, then a scoped active browser workflow, then the
  read-only next-task forecast. Long-running workflows such as Village scan publish nested activity so an inner
  queue task can temporarily replace the label and the outer workflow is restored when that task completes.
- Village Overview Farming renders only the allowed `send_farmlists` queue state: `Ready`, `Running`,
  `Blocked`, or its `NextAttemptAt` countdown. Individual farm-list raid timers belong to the Farming panel
  and must not replace the dashboard-synchronized dispatch deadline in Overview.
- Village Overview Town Hall renders each live celebration on its own line as `Small: <timer>` or
  `Great: <timer>`. The Town Hall read preserves up to two exact active timers (including mixed modes) in
  account state; generic task names and resource-wait descriptions do not belong in that cell.
- The 1 Hz presentation pulse must not perform file I/O, replace stable ItemsSource collections, or rebuild
  unchanged rows. Cache configuration outside the pulse, derive countdowns from absolute deadlines, and apply
  only changed values; persistence and high-volume log writes run serially off the UI dispatcher.

### New features

1. Capture the relevant Official page/dialog state and identify stable scoped markers.
2. Add only verified Official selectors and use existing root-relative path helpers.
3. Parse into domain models; keep decisions/calculations outside browser and WPF code.
4. Reuse queue, cancellation, pacing, persistence, logging, and busy-state patterns.
5. Add focused parser/calculator tests and a regression test for the reported failure.
6. Verify retries, cancellation, secrets, persisted state, and publish output when applicable.
7. Record durable cross-cutting rules here; put feature decisions in an ADR and history in the archive.

## Construction and queue invariants

- Account reads (`status`, account/village snapshots, and village scans), automatic reward collection, account-wide
  reset detection, and free production-bonus activation are immediate Account tasks. They run before
  village/group work and must never enter Construction's strict queue order. Account is an always-on queue category,
  not a user-toggleable automation group; do not show it on the Dashboard or in per-village group settings.
- `ActiveConstructions` is the source of truth for occupied construction slots. A full queue is a normal blocked
  state, not an exception.
- A confirmed empty dorf1/dorf2 construction overview arms a short per-village immediate-fill burst: start all
  available official resource/building slots without the construction start delay, then resume normal humanized
  timing. Romans have one resource plus one building slot without Plus; Plus adds one flexible third slot (up to
  two resources or two buildings, three total). Every pending construction row, including an in-progress parent such
  as `upgrade_all_resources_to_level`, preserves visible queue order and blocks later rows until it is complete.
- A confirmed empty overview gives the first stale resource `page_timer` head one immediate live validation so a
  free slot cannot idle behind an obsolete timer. Hero inventory is never polled for this: only an observed inventory
  increase wakes the first resource-deferred construction head per village; identical reads and transfer deductions do not.
- Construction follows visible per-village queue order. A deferred head blocks later construction in that village;
  verified automatic prerequisite repair may be promoted only when a live slot is available.
- Check storage, prerequisites, available slots, and resources before a Build/Upgrade click.
- Storage-capacity blocks create the required Warehouse/Granary dependency at highest queue priority and keep the
  parent deferred. Queue-time storage preflight covers constructs, selected/max building upgrades, single/bulk
  resource upgrades, upgrade-all, and templates. It projects earlier same-village work, splits targets at each
  capacity boundary, and atomically inserts only the next required storage level immediately before the blocked
  stage. If Warehouse or Granary does not exist, offer to construct it in a verified free slot before upgrading it.
  The confirmation groups actions by the resource/construction stage and visually distinguishes construction from
  upgrades; the displayed order must match the queue insertion order. The account-scoped Construction setting can
  request 1-10 storage levels ahead (default 2); a triggered storage action targets the greater of the minimum level
  required by the cost and the current storage-building level plus that configured value.
- The account-wide Construction setting for crop-shortage recovery is enabled by default. Only the scoped Official
  `.upgradeBlocked > .errorMessage` text `Lack of food: extend cropland first!` triggers it; negative production alone
  does not. Keep the blocked construction head, prioritize at most two lowest-level cropland steps (including active
  ones), and resume that village's Construction queue only after a completed recovery step and a fresh positive crop
  production read. With recovery disabled, defer only that village's Construction head for 30 minutes and alarm.
- Resource `Upgrade to max` uses the level-10 staged plan only in non-capital villages. Capitals show that max-mode
  storage planning is unsupported and direct the user to choose an explicit `Upgrade all to level` target.
- Official storage blocks use `.upgradeBlocked > .errorMessage`; disabled actions can remain in the DOM with a
  CSS `disabled` class. Construction and upgrades share the same `storage_capacity` flow.
- Correlate Official queue rows by slot when present, otherwise normalized name plus level/count. Do not treat
  `.underConstruction`, `.buildDuration`, or `#building_contract` as queue rows.
- Resource-field names repeat. When the target slot is known and either queue source identifies a same-name row
  by another slot, never apply an unknown-slot same-name row to the target; use exact slot identity.
- Existing buildings and level-zero sites are distinct. Select exact building types and verify active village,
  target slot, and result before considering an action successful.
- Immediately before constructing any building, read the complete live dorf2 overview. Remove a stale construct
  when its exact target slot already has the intended building, when a single-instance building exists anywhere,
  or when a level-gated duplicate has not reached its required level; rebind dependent upgrades to the confirmed
  live slot. Keep the construct when an additional copy is legal.
- A matching active prerequisite below the required level defers its dependent construct until the active step
  finishes, even when Official omits the active slot id. Re-plan the remaining prerequisite levels from the next
  complete live overview; never terminal-fail the dependent construct during that intermediate state.
- After a successful hero resource transfer reloads the same verified build.php slot, retry its exact construct or
  upgrade action directly; do not restart through queue and dorf2 probes unless the direct action remains unavailable.
- An upgrade that confirms its planned slot is empty is not a successful no-op. Reconstruct the expected building in
  that exact slot without slot fallback, keep the upgrade pending, then continue its original target level.
- Fresh full dorf2 reads reconcile single-instance building upgrades by gid across the whole village, not only the
  queued slot: remove targets already reached and rebind unfinished targets to the confirmed live slot. Duplicate
  construct detection must report that effective slot before removing the stale construct. Run reconciliation before
  desktop queue/requirement defers at every live full-status entrypoint; a disk snapshot used only to repaint UI must
  never mutate the queue.
- Missing-building recovery may reconstruct only after a second complete 22-slot dorf2 read confirms that the expected
  gid/name is absent village-wide. An incomplete or identity-ambiguous read defers without adding a construct.
- Every village-status cache write for the same village must also replace the preferred UI building snapshot after
  partial-state merging; never let an older unknown-level snapshot override a newer live or merged read.
- Queued and direct `Load buildings` must both produce a full village status with Warehouse/Granary capacity. A
  dorf2 building snapshot must be merged with the same village's existing status, never replace it with null capacity.
- Release smoke tests must wait on `ReleaseSmokeContract.ReadyLogMarker`, not logs from optional/removed startup work.
  Bundled Chromium is validated structurally by exact Playwright revision before launch; keep a contract test for the
  PowerShell marker so application startup and the GitHub release workflow cannot silently drift apart again.
- Templates preserve resource scope, reservations, ordered prerequisites, atomic insertion, and runtime slot
  rebinding. Tribe-incompatible choices remain disabled.
- Catalog coverage is required for Romans, Teutons, Gauls, Egyptians, and Huns. Vikings are unsupported.
- Detailed queue, storage, click, and estimate rules: [construction ADR](adr/2026-06-20-construction-queue.md).

## Current pitfalls

- Account tribe and active-village tribe are different on special servers. Cache village tribe by stable identity;
  unknown tribe is deferred, never borrowed from another village/account. Per-village Smithy option dialogs resolve
  their troop catalog from the target row's canonical village key, never from the Dashboard's selected village.
- Verify active village after switching and before state-changing actions. Missing villages are quarantined until
  confirmed, not deleted after one incomplete refresh.
- Hero ownership and current location are separate. Scope transfers to the active dialog and verify the target.
- Read an away Hero's ETA from Hero Attributes, never from Rally Point troop movements. Use the displayed timer
  directly for an explicit return to the home village; double every outbound movement timer (adventure, raid,
  attack, reinforcement, or another destination) to include the return leg.
- Empty building slots contain one contract per available type; scope cost reads and transfer clicks to the exact
  `#contract_building{gid}`.
- Cache only data with an owner, invalidation rule, and safe stale behavior. Incomplete refreshes must not erase
  the last valid snapshot or fabricate zero/empty state.
- Construction mutations use the short fresh-read cache; read-only observations may use the longer cache but
  never past a known completion deadline. Navigation and state-changing clicks invalidate both.
- A resource construction with unknown slot identity may prove queue occupancy and timing, but never that a
  specific known resource slot is already in progress merely because its repeated field name and level match.
  Confirm the exact slot from queue identity or from that slot's own build page.
- Construction Queue Reconciliation plans only from confirmed full live status and applies all pending-item
  changes atomically; cache or local timers are never reconciliation evidence.
- Deferred resource-gated waits are re-estimated LIVE on every resource read (jitter included), not left on
  the worker's one-shot ETA: `RefreshDeferredConstructionWaitsAsync` / `RefreshDeferredTroopTrainingWaitsAsync`
  re-read current resources + production, recompute the wait, update the UI timer, and release when the live
  threshold is actually met (e.g. farming income makes a "build at 80%" fire at the real time, not a stale
  20h). Both are triggered from `CacheVillageStatus`, so they run for ANY read village, not only the selected
  one. The troop recompute needs storage capacities — with capacity 0 the eval falsely reports "ready", so a
  light current-page read (no caps) fills caps/production from the village cache but keeps the LIVE current
  resources; buildings are never cache-filled (empty is handled leniently, a stale list could wrongly exclude).
- Village scan finishes ready, automation-enabled work for the freshly read village before applying the
  inter-village delay. Task permission still comes from village Auto and group settings. Sweep wait reconciliation
  is awaited before selection so a newly released task runs during the same visit, and selection requires the exact
  canonical village key.
- A manual Village scan "Scan now" clears the persisted round deadline. An active continuous loop consumes a
  forced-sweep request at its next safe boundary; without an active loop the scan runs in its own manual operation
  scope and does not start the full continuous loop.
- Account scan uses a new transient scan scope on every dialog open (Dorf1 and Dorf2 selected by default) and reuses
  the Village scan page readers. It must not persist those one-off choices or alter the sweep schedule.
  Villages are visited in randomized order and the browser remains on the final naturally scanned village; do not
  add a return-to-start navigation.
- A Village scan Dorf1 read is authoritative for the visible `.buildingList` construction queue and the
  active village population in `#sidebarBoxActiveVillage .population span`. Both update cache/UI and queue
  decisions even when Dorf2 scanning is disabled.
- The same Dorf1 sweep visit checks the existing Official Questmaster and Daily Quest claimable markers. When the
  corresponding auto-collect setting and village automation allow it, `collect_tasks` and
  `collect_daily_quests` run before other village work; afterward the selected sweep scope is re-read because
  rewards can change resources.
- After every building-mutation task the desktop ALWAYS re-reads the full dorf1+dorf2 for the just-worked
  village (`RefreshConstructionStatusAfterBuildingMutationAsync` → `RefreshConstructionStatusAsync`, then
  `CacheVillageStatus`). Do not restore the old QueuedOrInProgress/AlreadySatisfied storage-only quick-skip:
  an upgrade reports QueuedOrInProgress on every climb pass and a build that finishes while the loop is on
  another village returns AlreadySatisfied, so skipping the dorf2 read froze the cached building level (a
  Marketplace shown as 12 in the UI while it had reached 20 in-game).
- Same rule for resource fields (dorf1): after a resource-upgrade task,
  `RefreshResourceStatusAfterResourceMutationAsync` re-reads the just-worked village's fields
  (`resourceOnly:true, forceCurrentVillage:true`) and `CacheVillageStatus`es them, repainting the resource
  UI only when it is the selected village. Do not reinstate a log-line "fast update" of the displayed rows:
  it patched the SELECTED village from another village's log lines and never cached, so field levels went
  stale / cross-contaminated in a multi-village account.
- Production-bonus (Advantages tab) scan reads the running bonus's percent AND `.timerReact` countdown from
  its `.bonusInfo` "+N% active for:" label — NOT from `.bonusDuration` (that holds only the auto-prolong
  checkbox). Timers come in a long form with a day suffix ("5d 15:52:56"); `ParseTimerToSeconds` extracts
  the `Nd` days then parses the `hh:mm:ss` remainder.
- Construction start-delay transition memory is village-scoped by `data-did` or coordinates, never display name;
  duplicate village names must not share a humanize deadline.
- Persisted account analysis may seed the stable village list. Cold start without a snapshot reads the profile;
  later full logins merge the live sidebar so new/renamed villages are found without another profile visit.
- A transient village refresh that returns only part of an already verified list must merge fresh rows into the
  existing list instead of shrinking the Dashboard; only an explicitly complete login list may remove villages.
- New-account analysis is account+server scoped. A pending first-login analysis forces hero inventory, hero
  attributes, and new-village startup until all three succeed; legacy account snapshots are already initialized.
- Browser activity statistics are account-scoped: lifetime counters persist; session counters do not.
- Build troops `% resources` checkboxes use OR semantics: at least one resource must be selected, any selected
  resource at or above the percentage threshold releases training, and deferred waits use the earliest selected
  resource ETA. This trigger never replaces the normal all-resource affordability, NPC, or hero-resource checks.
- Build troops `maximum` amount mode must click Travian's numeric `.details .cta a[href='#']` shortcut beside the
  selected troop input and verify Travian filled the advertised amount; do not type that maximum manually. The
  existing paced Train-button click remains the submit action after the shortcut succeeds.
- Dashboard B/S/W troop indicators represent effective per-village Build troops configuration, never training
  queue activity: green means Auto + Build troops + that building toggle are enabled and the building exists;
  amber means effectively enabled but the building is missing or its status is unknown; muted means disabled.
- Map SQL `Skip own villages` resolves the in-game owner name from the active page's `.content > .playerName`;
  never substitute the login email/account key. Add the resolved name to the normalized ignored-player filter so
  every village owned by that player is excluded, and fail the import safely when the checked filter cannot resolve it.
- Bulk messages must classify every Send as verified sent, one missing player, or a visible/timeout error. Remove
  missing players one at a time and retry the same batch; an emptied batch continues to later batches. Cache only
  recipients from a verified send. The analysis preview shows the summed map.sql village population per player in
  the exact selected send order.
- Farm-list exact timers get a 5-15s render margin; unreadable disabled timers use an estimated 60s wait.
- "Send toggled lists" sends selected farm lists ONE AT A TIME via `SendFarmListsSequentiallyAsync`: click each list's Start,
  then wait for that list's `.farmListStatus` "N/M being raided" numerator to rise (or its Start to disable)
  before the next individual click so a failed list is detected before advancing.
  The wait between clicks is the "Send farmlists" action pacing (`FarmListStepDelayMin/MaxSeconds`, default
  1-4s, on the Settings pacing tab). "Send all lists" instead performs one click on Travian's
  `button.startAllFarmLists` control, using the established real-click-with-JS-fallback flow.
  `ContinuousFarmDispatchDelay` (minutes) is the gap between whole rounds,
  not between individual lists.
- Farm-list rows dedupe/merge by stable `lid` (data-list), never by display name — two villages can hold
  same-named lists that a name key would collapse into one row/group. Rows are grouped in the UI by the owning
  `.villageWrapper` ordinal (read per analyze), not by name, so two villages that share a display name stay in
  separate groups; the heading label is the village name plus coordinates. The farm page exposes no village
  id/coordinates on the wrapper, so coordinates are resolved from the known village list by name and only when
  that name is unique (a duplicated name is ambiguous → name only). A village rename just re-groups next read.
- Moving a loss target uses the live-verified Official route `/build.php?id=39&gid=16&tt=99` and row-edit
  contract: `td.openContextMenu` → `.entry.edit` → `.dialog.basic.slotDialog`, then
  `select[name='listId']`, `input[name='isActive']`, and `button.save`. Keep the list `lid` as the stable
  destination identity; display name is only for rebind/recreation when a configured list disappears.
- Analyzed farm lists persist per account (`FarmListsSnapshotPath`) and are restored into the panel at
  startup / account switch so it is never blank; restored timers are re-based on the capture time and
  `_lastFarmListsAnalysisAt` stays `MinValue` so a real re-analyze still fires when due.
- Never stack Add-target dialogs: a canceled/failed add-farms run can leave the dialog open, and the reopen
  dispatch fires even behind an overlay, so opening a new one produces two stacked dialogs whose top
  `#dialogOverlay` intercepts every click on the form inputs (coordinate click times out). `OpenAddRaidFormAsync`
  closes any lingering dialog before opening, and a single target's fill/save exception is skipped (bounded
  consecutive-failure abort) instead of failing the whole batch.
- Reused Add-target dialogs must replace X/Y through the traced input path and re-read both fresh fields as an exact
  pair before validation. Retry replacement only a bounded number of times and never click Save while either
  coordinate differs from the requested value.
- Program-created farm lists carry the account-scoped Create-popup preference `Only create reports with losses`,
  defaulting enabled when absent. Before Create, set and verify `#createFarmListForm input[name='onlyLosses']` with
  a real label/input click; a missing or unverifiable checkbox is logged but must not block list creation.
- Hero attribute priority is execution-authoritative from the latest saved account settings. A queued
  `hero_manage` or `spend_hero_attribute_points` payload is only a snapshot and must never overwrite a reorder
  the user made in the UI while the task was waiting.
- Hero runtime state is published as one structured Worker update (`HeroRuntimeStatus`). The Hero page and the
  Village overview icon must consume that same update so away/dead/reviving state cannot diverge between views.
- Hero Attributes navigation is required only when the sidebar signals new points or no known attribute snapshot
  exists. Successful point allocation invalidates memory and disk, and incomplete DOM reads never overwrite a valid
  snapshot. Hero HP uses the global SVG first and opens Attributes only when that live signal is unavailable.
- Hero inventory resources are an account+server persisted last-known snapshot. Quick re-login, process restart,
  and account switching restore it; incomplete inventory reads never replace it with fabricated zeroes. When no
  snapshot has ever been captured, construction/resource actions may open their existing resource-transfer dialog,
  read the live inventory, and close it without transferring before continuing the original action.
- React task-tab changes use Action pacing's click delay before the DOM click. Do not put the delay after the
  General/Village tab click; that makes a Collect-to-tab transition effectively instantaneous.
- Bonus-video failures use shared protected timing, typed cooldowns, account proxy routing, and sanitized logs.
  See [bonus-video ADR](adr/2026-07-18-bonus-video.md).
- One `activate_production_bonus` run is a contiguous four-resource batch: after its initial cooldown gate,
  attempt every resource found activatable before returning control to other automation. A failure or newly
  created internal video cooldown for one resource must not stop the remaining resources in that same batch.
- Diagnostics use shared busy/cancel behavior, sanitize settings/logs/paths/URLs/auth/proxy data, and never present
  partial output as a successful archive. Screenshots may contain visible game data.
- The Dashboard active-village border represents verified live browser state only. Queue selection/Running state
  must never pre-mark a task's target village; update it only after a successful browser village verification.
- Incoming Attack monitoring observes only real Dorf1 reads: the active village requires the hostile red
  `img.att1` marker inside `.villageInfobox.movements #movements` (movement labels and `def1`/`att2` must never
  signal an attack), while a Plus village overview uses
  `.listEntry.village.attack[data-did]`. A nullable signal list means "Dorf1 was not read" and must preserve
  prior signals; an empty list is authoritative only for the active Dorf1 village. Rally Point details are read
  only after exactly `button.iconFilterActive img.subFilterCategory1` is active and categories 2/3 are inactive.
  Filter/read failures never clear known attacks. Exact movements persist per account+world, use the Travian
  movement id when available, and expire at their server-derived absolute arrival before a safe recheck.
- Construction timers shown in the village overview are Travian's raw slot finishes. Scheduling, loop wake-up,
  and `Next task` use the effective availability time: raw finish plus the already persisted construction-humanize
  delay (and existing race buffer). Forecasts must reuse the live selector without mutating queue, rotation, or
  pacing state; normal construction navigation must not start exactly when the raw timer expires. Select and persist
  the normal construction delay before navigating to the task village; the worker then consumes that one-shot decision
  without randomizing again. Login-fill and pre-sleep-fill keep their explicit early-fill exceptions. Login never
  forces or reschedules a Village scan: it may fill only the live-verified browser village, while an independently due
  scan may fill free slots as it naturally visits each village. Full slots retain their persisted queue-humanize extra
  so later navigation still waits for the effective deadline.
- An automation run captures Worker's actual `BrowserGeneration`; never mirror or synthesize that generation in
  Desktop. Runtime-item reconciliation identifies village scope with `BotOptionPayloadKeys.TargetVillageKey` and
  must preserve an existing pending item's authoritative `NextAttemptAt` when refreshing payload or priority.

## Target architecture

- Smaller domain services for construction, farming, hero, map, messages, and account state.
- One deep Desktop orchestration module owns Continuous Loop and Auto Queue policy and runtime state;
  `LoopController` retains lifecycle/cancellation and Worker retains Official Travian browser actions.
- Pure fixture-tested parsers/calculators independent of Playwright.
- Thin browser adapters with explicit timeouts, cancellation, and result states.
- ViewModels exposing commands/state without browser or filesystem details.
- Central path, persistence, diagnostics, and release-packaging services.
- Fast domain tests, fixture-based parsing tests, and limited live smoke checks.

## Architecture decisions

- [UI theme](adr/2026-06-03-ui-theme.md)
- [Multi-village state](adr/2026-06-05-multi-village.md)
- [Dashboard overview](adr/2026-06-06-dashboard-overview.md)
- [Shutdown cleanup](adr/2026-06-08-shutdown-cleanup.md)
- [Farmlists and Travco](adr/2026-06-09-farmlists-and-travco.md)
- [Construction queue](adr/2026-06-20-construction-queue.md)
- [Map oasis scan](adr/2026-06-20-map-oasis-scan.md)
- [Smithy and troop training](adr/2026-06-20-smithy-troop-training.md)
- [Town Hall celebration](adr/2026-06-20-town-hall-celebration.md)
- [TravianClient seams](adr/2026-06-25-travianclient-seams.md)
- [Browser session and login](adr/2026-07-18-browser-session-and-login.md)
- [Bonus video](adr/2026-07-18-bonus-video.md)
- [Continuous automation orchestration](adr/2026-08-14-continuous-automation-orchestration.md)

## Arkiverad historik

Äldre beslut och detaljerad historik finns i:

- [Pre-compression snapshot, 2026-07-14](history/engineering-notes-2026-07-14-pre-compression.md)
- [Engineering notes archive](history/engineering-notes-archive.md)

Before deleting or shortening a rule, confirm that its detail exists in the snapshot, archive, or an ADR.
