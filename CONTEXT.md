# Tbot Ultra

Tbot Ultra automates actions on Official Travian worlds while keeping account- and world-specific game state separate.

## Language

**Map Oasis Scan**:
A resumable search of Official map areas for oases matching a selected scope and filter.
_Avoid_: Map crawl, oasis scrape

**Map Oasis Checkpoint**:
The account- and world-specific saved progress and partial results of one Map Oasis Scan. It resumes a matching scan after an unexpected failure; user cancellation produces a partial result and clears the checkpoint.
_Avoid_: Scan cache, temporary scan state

**Map Oasis Scan Result**:
The list of oases found by a Map Oasis Scan together with completion information. The oasis list remains the input to Farm Lists and other downstream uses.
_Avoid_: Map data response, scan output

**Construction Queue Reconciliation**:
The application of a confirmed live village construction status to pending construction queue items, including satisfied targets, slot rebinding, and required dependencies.
_Avoid_: Queue repair, build queue sync

**Village Status Round**:
One randomized pass through every known village that reads the enabled live game state, updates the per-village status cache and lets already-enabled automation react before moving to the next village. It leaves the browser on the final naturally visited village; the next round is scheduled separately.
_Avoid_: Village scan loop, village refresh

**Send Troops**:
The verified Official Travian flow that opens the Rally Point troop form and prepares troop dispatch for combat or Farm Lists.
_Avoid_: Manual Farming, Natar farming

**Manual Farming**:
The removed desktop-only manual farming UI and its saved preferences. It is not Send Troops, Farm Lists, Catapults, or Reinforcements.
_Avoid_: Manual attack flow

**Sleep Snapshot**:
What was actually running (logged-in state, continuous loop, queue auto-run) when session sleep began, captured so the next wake restores the same state instead of always starting the continuous loop. Modeled by the `SleepSnapshot` record; the pure wake decisions live in `SessionWakeDecisions`.
_Avoid_: pre-sleep flags, wake state

**New Account Analysis**:
The account-and-world-specific first-login initialization that reads hero inventory, hero attributes, and missing
new-village status. It remains pending until all three reads succeed.
_Avoid_: every-login analysis, hero inventory cache

**Update Notification Acknowledgement**:
The application-wide saved release version that the user has already handled in the new-version notification.
It suppresses only that release; a later release is eligible to notify again.
_Avoid_: update mute, per-account update state
