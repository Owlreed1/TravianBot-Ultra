# ADR: Demolition Sequence

## Decision

Demolition is a dedicated queue group. A Worker execution starts at most one Official Travian demolition level, reads `table#demolish .timer[value]`, and defers the same village-scoped queue item until the server timer plus one persisted random delay has elapsed.

## Consequences

The browser is never held while Travian demolishes. Queue deadlines survive restart, and Stop demolition removes only bot work that has not already been accepted by Travian.
