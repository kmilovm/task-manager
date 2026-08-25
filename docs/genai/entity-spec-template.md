# Entity specification template

The input the entity agent takes. Everything the agent needs to decide is stated here, so that
nothing is left to its judgement and every generated rule can be traced back to a line you wrote.

Copy this file, fill it in, and hand it to the agent together with `entity-agent.md`.

---

## Identity

- **Entity name (singular):**
- **Feature folder (plural, PascalCase):**
- **One sentence describing what it is for:**

## Ownership and relationships

- **Owner:** every record belongs to a `User`. Yes / No — and if no, say what governs access.
- **Parent entity:** none, or the entity it hangs from.
- **On deleting the parent:** cascade, restrict, or orphan.
- **Reachable how:** its own top-level route, or nested under the parent.

## Fields

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| | | | |

Types available without adding a package: `string`, `int`, `decimal`, `bool`, `Guid`,
`DateOnly` (a calendar date), `DateTimeOffset` (an instant), or an enum you declare below.

## Enums

| Name | Values | Default on create |
|------|--------|-------------------|
| | | |

## Business rules

Numbered, one rule per line, phrased as something that is always true. These become the
invariants and the tests, so a rule that is not written here will not exist in the code.

| ID | Rule |
|----|------|
| BR-1 | |

## Operations

Tick what the feature exposes. Anything not ticked is not generated.

- [ ] Create
- [ ] Read one
- [ ] Read many
- [ ] Update
- [ ] Delete
- [ ] Filters — list them: 
- [ ] Search — say which field:

## Ordering

How the list comes back by default.

## Acceptance criteria

Either a reference to an existing specification under `docs/specs/`, which then stays the single
source of truth, or Gherkin written here in the same style. Scenarios must be named, because the
agent has to map each one to a named test.

```gherkin
Feature: 
  Scenario: 
    Given 
    When 
    Then 
```

## Out of scope

What the agent must not build, so it does not improvise.
