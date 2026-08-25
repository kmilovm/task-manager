# Entity agent

An operating manual for a GenAI coding agent that adds a complete vertical slice — domain,
application, persistence, API and frontend, with tests at every level — to this codebase.

It is written to be tool-agnostic. Paste it into whichever agent you use, together with the
entity specification and the repository itself as context.

---

## Why it is shaped this way

Generated code is only as good as the context it is given, and a written specification is a far
better context pack than a conversation. So the agent is never asked to invent an architecture.
It is asked to reproduce a vertical slice that already exists, against a specification that
already states the rules, with tests written before the code and a human gate between phases.

The three failure modes this guards against are the ones these tools reliably hit: quietly
breaking a layer boundary, quietly dropping a business rule that was never written down, and
producing tests that assert the implementation instead of the behaviour.

---

## The prompt

> You are adding a new feature to an existing .NET 10 + Angular 19 codebase that follows Clean
> Architecture. Your job is to produce a complete vertical slice for the entity described in the
> attached specification.
>
> **Read first, in this order:**
> 1. `docs/architecture.md` — the dependency rule, the anatomy of a feature, and the conventions.
> 2. `docs/specs/README.md` — how specifications drive tests.
> 3. The `Users` feature, end to end, as the reference implementation: the entity, the service,
>    the validators, the repository, the endpoints, and all four test projects.
> 4. The entity specification you have been given.
>
> **Hard constraints.**
> - Do not modify any existing feature. You add files; you touch existing files only where the
>   architecture document says a feature registers itself (the ports file, the DI registration,
>   the `DbContext`, the Angular routes).
> - Do not add a NuGet or npm package. If you believe one is required, stop and say so.
> - Tests come before production code, always, and you show the failing run before implementing.
> - No business rule in an endpoint. No `DateTimeOffset.UtcNow` outside the clock adapter. No
>   `IQueryable` returned from a repository.
> - A record owned by another user is reported as 404, never 403.
> - The build has `TreatWarningsAsErrors`. A warning is a failure.
>
> **Work in this order, stopping for approval between phases.**
>
> *Phase 0 — Plan.* Produce, and do not write any code until it is approved: the list of files you
> will add or change; the order of the slices; which test covers which numbered scenario of the
> specification; every business rule you found in the specification and where you will enforce it;
> anything in the specification you found ambiguous, with the reading you propose.
>
> *Phase 1 — Domain.* Write the entity tests from the specification's invariants, show them
> failing, then write the entity. Run the domain suite.
>
> *Phase 2 — Application.* Write the validator tests and the service tests using NSubstitute
> fakes and a frozen clock, show them failing, then write the contracts, the validators, the port
> and the service. Register the service in `AddApplication`. Run the application suite.
>
> *Phase 3 — Persistence.* Write the repository tests against SQLite in memory, show them failing,
> then write the entity configuration, the repository and the `DbSet`. Register the repository in
> `AddInfrastructure`. Generate the migration with the CLI; never hand-write one. Run the
> infrastructure suite.
>
> *Phase 4 — API.* Write the endpoint tests against `WebApplicationFactory`, show them failing,
> then write the endpoint group and map it in `Program`. Run the whole backend suite and the build.
>
> *Phase 5 — Frontend.* Add the feature folder: models, typed HTTP service, signal-based store,
> list component and form component. Add the route behind the auth guard. Run `ng build` and
> report that it is warning-free.
>
> *Phase 6 — Close.* Update the specification's traceability table with the real test names, and
> update `docs/requirements-coverage.md` if a row changes. Report the final suite output and the
> list of files added.
>
> **Report at every phase**: the command you ran, its actual output, and — when a test failed for
> a reason you did not expect — what you changed and why. Never report a suite as passing without
> showing the run.

---

## Review gates

Applied by a human between phases. These are checks, not suggestions; a failed check sends the
phase back.

| Gate | What is checked |
|------|-----------------|
| Layering | No reference to EF Core or ASP.NET from Domain or Application. `dotnet list <project> reference` proves it |
| Rules | Every business rule in the specification appears in exactly one place, and that place is the domain or the application layer |
| Ownership | The ownership check sits in the service, returns not-found, and has a test that a second user cannot read, update or delete the record |
| Dates | No `DateTimeOffset.UtcNow` outside `SystemClock`; date-only comparisons use `DateOnly` |
| HTTP | 201 with `Location`, 204 on delete, 404 not 403, `ProblemDetails` on every error path |
| Tests | Assertions are on observable behaviour. A test that passes before its implementation exists is rejected as broken |
| Migration | Generated by the CLI, and applying it to an empty database reproduces the schema |
| Warnings | `dotnet build` and `ng build` are both clean |

---

## Running it live

1. Write the entity specification (`entity-spec-template.md`) — three minutes of typing.
2. Give the agent this document, the specification and the repository.
3. Approve the plan, or correct it. The corrections are the interesting part.
4. Let it run the phases, watching the test output.
5. Show the new endpoints in Swagger and the new screen in the SPA.

If a phase goes wrong, that is not a failed demo — the recovery is the demonstration. Read the
failure, correct the specification or the constraint that allowed it, and re-run the phase.
