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

## Conventions

- `Given` describes state that already exists, `When` a single action, `Then` an observable outcome.
- Times are UTC. "Today" means the current UTC date.
- Error responses follow RFC 7807 (`application/problem+json`).
