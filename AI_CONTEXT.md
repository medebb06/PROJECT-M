# PROJECT M — PROJECT CONTEXT

## Project overview
- Engine: Unity
- Game: 2D action platformer
- Theme: Dark samurai / ronin combat
- Primary goal: A small, polished game with responsive, satisfying combat.
- Main priorities: Input responsiveness, combat feel, clear enemy telegraphs, readable feedback.

## Core combat direction
- Parry is a core defensive mechanic.
- Enemy posture/balance is important to combat.
- Executions are used against vulnerable enemies.
- Dash and block support combat and positioning.
- Combat should reward timing and deliberate decisions.
- Avoid unnecessary systems that do not meaningfully improve moment-to-moment gameplay.

## Architecture
- Use the existing player state machine and combat architecture.
- Player combat has used ICombatState, AttackState, and PlayerCombatController.
- Enemy behavior has used EnemyController and IEnemyState.
- Treat these names as historical context: inspect the actual project before relying on them.
- Preserve working code and existing serialized references.

## Design decisions
- Prioritize a small number of meaningful mechanics.
- Enemy encounters should be readable and coordinated.
- Avoid overwhelming the player with simultaneous enemy attacks.
- Do not reintroduce removed progression, shop, charm, weapon, or procedural-generation systems without explicit approval.

## Development workflow
- Inspect before editing.
- Make focused changes.
- Explain required Unity Inspector setup.
- Test when possible.
- Record task results in AI_HANDOFF.md.

## Important
This document contains project direction and historical context, not a guarantee that every listed script or feature currently exists.
Inspect the current repository and Unity project before modifying code.
When current files conflict with this document, report the discrepancy and confirm before making destructive changes.