---
name: new-backlog-item
description: Add a new item to the backlog (docs/backlog/). Use when the user describes a feature, fix, chore or spike that has no backlog entry, or asks for a story, business problem or acceptance criteria. Reads the index only, never the other item files.
---

# New backlog item

Every feature needs an item before code.

1. Read `docs/backlog/index.md` only. Take the highest `MEP-###` and add one. Do not open other item files.
2. Write `docs/backlog/items/MEP-###.md` from the template below.
3. Add a row to `docs/backlog/index.md` with id, title, status and priority.
4. The item must fit one vertical slice (migration, API, Angular, tests). If it does not, write several items instead and note the order in each.
5. Show the user the item before writing files.

## Template

````markdown
## [MEP-###] Title

**Status:** Backlog
**Priority:** High | Medium | Low

### Business Problem
What problem this solves for the single user, and why it matters. Two to five sentences.

### Acceptance Criteria
```gherkin
Feature: Name
  Scenario: Name
    Given a precondition
    When an action
    Then an observable outcome
```
````

Status values: Backlog, Proposed, In Progress, Blocked, Done, Post-MVP (Backlog).

## Gherkin rules

- One behavior per scenario. Name the scenario for the outcome.
- Concrete values: "3 cups of flour", not "enough flour".
- Observable results only: response fields, status codes, rows, UI text. Never internals.
- Cover the boundary and the failure case, not only the happy path.
- Respect the invariants in `docs/domain.md`: container sizes are user-declared, unresolved recipes never get a `MatchScore`, no silently wrong math, Low-confidence Claude output never applies silently.
- A QA reader must be able to write the test without asking a question.
- Do not gold-plate. State what is out of scope.
