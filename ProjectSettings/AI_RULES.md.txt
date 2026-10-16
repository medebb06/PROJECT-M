# PROJECT M — AI RULES

## Before working
- Read AI_CONTEXT.md and AI_HANDOFF.md before making changes.
- Inspect the existing scripts, references, and project structure before editing.
- Understand how the current implementation works before proposing a rewrite.

## Code safety
- Preserve the existing architecture and working state machines.
- Preserve serialized field names, public APIs, and Inspector references unless a change is necessary.
- Do not rewrite unrelated systems.
- Do not remove features or change game design decisions without asking.
- Avoid creating duplicate scripts or competing implementations.
- Provide complete, consistent scripts when a full replacement is necessary.
- Do not leave the project in a partially implemented state.

## Unity instructions
- Give beginner-friendly, step-by-step instructions.
- Explain where each script belongs and which Inspector references need to be assigned.
- Explain required GameObject, component, prefab, and scene changes.
- Use the Unity and package versions already installed in the project.
- Do not assume a component or API exists without checking the installed version.

## Testing
- Clearly distinguish implemented, tested, and unverified changes.
- Never claim compilation or gameplay has been verified unless it actually has.
- List known issues and possible regressions.

## End of each task
- Update AI_HANDOFF.md.
- Update AI_CONTEXT.md only when architecture or permanent design decisions change.
- List changed files and explain why they changed.
- Do not commit or push to GitHub unless explicitly authorized.
- Never overwrite another agent's uncommitted changes.

## Priority
Preserve working functionality first. Make the smallest safe change that fully solves the requested problem.