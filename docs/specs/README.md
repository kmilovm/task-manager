# Specifications

The application is built spec-first: every behaviour is described here before any production
code is written, the acceptance criteria are expressed in Gherkin, and each scenario is mapped
to the automated test that proves it.

| ID | Specification | Status |
|----|---------------|--------|
| US-001 | [Account access](US-001-account-access.md) | Implemented |
| US-002 | [Personal task management](US-002-task-management.md) | Implemented |

## Working agreement

1. A change starts as a scenario in one of the specs above.
2. The scenario is translated into a failing test (unit test for a business rule, integration
   test for an endpoint).
3. Production code is written until the test passes, then refactored.
4. The traceability table at the bottom of each spec is updated with the test name.

## Where a specification lives

A specification lives here, in `docs/specs/`, and nowhere else. That holds however it was written:
a story typed by hand, or one filled in from
[`../genai/entity-spec-template.md`](../genai/entity-spec-template.md) to drive the entity agent.
If it carries its own Gherkin, it is the source of truth and it belongs in this folder with the
next `US-` number and a row in the table above.

`docs/genai/specs/` holds only the agent's *input*: identity, field types, operations and ordering,
pointing at the specification here for the rules and the acceptance criteria. That split exists so
the same business rule is never written down twice.

## Conventions

- `Given` describes state that already exists, `When` a single action, `Then` an observable outcome.
- Times are UTC. "Today" means the current UTC date.
- Error responses follow RFC 7807 (`application/problem+json`).
