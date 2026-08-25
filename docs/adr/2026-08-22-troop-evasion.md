# Troop evasion deadlines

## Status

Accepted — 2026-08-22

## Context

Troop evasion must react close to an incoming hostile attack, while normal queue actions are FIFO-like and browser
submits are atomic. Raid and Attack movements may also return too early and expose the troops before the threat lands.

## Decision

Troop Evasion consumes the existing Incoming Attack monitor and is scheduled as coalesced high-priority browser work
at safe task boundaries. Settings and successful protection windows are persisted per account and world. The Worker
owns the verified two-stage Rally Point form: the first Send builds a confirmation, and final Confirm is clicked once
only while the triggering attack is still in the future. Reinforcement confirms immediately. Raid/Attack waits until
`now + 2 × one-way travel >= triggering arrival + 15 seconds`; a more urgent village may cancel that wait and rebuild
later. Whole-attempt retries occur at one minute and thirty seconds before arrival.

The protection window is anchored to the first triggering arrival. Later attacks inside it do not cause another send;
the accepted consequence is that Raid/Attack troops may return before one of those later attacks.

## Consequences

- Evasion is not represented as a normal queue group and ignores Village Auto.
- Pause, account/configuration/browser-generation changes cancel any unconfirmed attempt.
- Only a verified final submission creates persisted protection state.
- Manual Validate submits only the first form stage and never clicks final Confirm.
