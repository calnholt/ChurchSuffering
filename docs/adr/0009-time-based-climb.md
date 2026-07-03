# The active Climb uses capped time and generated slots

## Status

Accepted (2026-06-20)

## Context

The active mid-run progression layer no longer uses spatial topology to determine available fights, shops, or events. Keeping the former run-map model as canonical would conflict with the Climb presented to players and leave event timing, the Final encounter, and non-combat opportunities without one coherent domain model.

Some Location and run-map code remains compiled while the transition is completed. Its continued presence is an implementation constraint, not a second definition of Climb behavior.

## Decision

1. **Active model**: the active Climb is a capped progression meter with generated Shop, Encounter, and Event slots presented in their respective columns.

2. **Progression**: Climb choices advance Climb time by their defined costs. Climb time is progression toward the run endpoint, not real elapsed time.

3. **Endpoint**: reaching maximum Climb time triggers the Final encounter. The Final encounter takes precedence over newly crossed event appearances.

4. **Legacy scope**: dormant Location and run-map code may remain compiled temporarily, but it does not define domain behavior and must not be used for new active-Climb features.

5. **Supersession**: this decision supersedes the spatial assumptions of ADR 0003 and the landmark-specific portion of ADR 0008. ADR 0008's Climb terminology and routing decisions remain in force.

## Consequences

- **Positive**: Climb opportunities, event timing, and Final-encounter routing share one progression model.

- **Negative**: Compiled legacy symbols can appear to describe active behavior unless code and documentation explicitly identify them as dormant.
