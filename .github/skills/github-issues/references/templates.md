# Issue Templates

Copy and customize these templates for issue bodies.

## Bug Report Template

```markdown
## Description
[Clear description of the bug]

## Steps to Reproduce
1. [First step]
2. [Second step]
3. [And so on...]

## Expected Behavior
[What should happen]

## Actual Behavior
[What actually happens]

## Environment
- Browser: [e.g., Chrome 120]
- OS: [e.g., macOS 14.0]
- Version: [e.g., v1.2.3]

## Screenshots/Logs
[If applicable]

## Additional Context
[Any other relevant information]
```

## Feature Request Template

```markdown
## Summary
[One-line description of the feature]

## Motivation
[Why is this feature needed? What problem does it solve?]

## Proposed Solution
[How should this feature work?]

## Acceptance Criteria
- [ ] [Criterion 1]
- [ ] [Criterion 2]
- [ ] [Criterion 3]

## Alternatives Considered
[Other approaches considered and why they weren't chosen]

## Additional Context
[Mockups, examples, or related issues]
```

## Task Template

```markdown
## Objective
[What needs to be accomplished]

## Details
[Detailed description of the work]

## Checklist
- [ ] [Subtask 1]
- [ ] [Subtask 2]
- [ ] [Subtask 3]

## Dependencies
[Any blockers or related work]

## Notes
[Additional context or considerations]
```

## Minimal Template

For simple issues:

```markdown
## Description
[What and why]

## Tasks
- [ ] [Task 1]
- [ ] [Task 2]
```

## Deferred / Future-state Template

For work being parked rather than done now. If it carries a revisit condition, also add it
as a row in the **Future-state register** in `Docs/RVS_Plan.md`.

```markdown
## Summary
[One-line description of the work]

## Why this is deferred, not scheduled
[What makes it not worth doing now]

## Revisit trigger
[The specific condition that should bring this back: an event, a metric threshold,
a dependency shipping, a date, or a repeated ask. One primary; a second is optional.
"Someday" is not a trigger.]

## Lands as
[Where this goes when the trigger fires: a Spec requirement + issue, an infra issue,
a decision issue, and so on.]

## Origin
[The issue / PR / decision this was spun off from]
```
