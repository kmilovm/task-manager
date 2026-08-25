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
already states the rules, with tests written before the code.

The three failure modes this guards against are the ones these tools reliably hit: quietly
breaking a layer boundary, quietly dropping a business rule that was never written down, and
producing tests that assert the implementation instead of the behaviour.

There is a fourth, subtler one, and it is the reason for the closing report in the prompt below:
**unattended work does not break the build, it accumulates small decisions that compile.** Drift is
invisible in a green run, so the agent is required to surface it rather than the reviewer having to
excavate it.

---

## The prompt

> You are adding a new feature to an existing .NET 10 + Angular 19 codebase that follows Clean
> Architecture. Your job is to produce a complete vertical slice for the entity described in the
> attached specification.
>
> **Read first, in this order:**
> 1. `docs/architecture.md` — the dependency rule, the anatomy of a feature, and the conventions.
> 2. `docs/genai.md` — the corrections a previous run of this protocol needed. Do not repeat them.
> 3. `docs/specs/README.md` — how specifications drive tests.
> 4. The `Users` and `Tasks` features, end to end, as the reference implementation: the entities,
>    the services, the validators, the repositories, the endpoints, and all five test projects.
> 5. The entity specification you have been given.
>
> **Hard constraints.**
> - Do not modify an existing feature. You add files; you touch existing files only at the
>   registration points listed below.
> - Do not add a NuGet or npm package. If you believe one is required, stop and say so.
> - Tests come before production code, always, and you show the failing run before implementing.
> - No business rule in an endpoint. No `DateTimeOffset.UtcNow` outside the clock adapter. No
>   `IQueryable` returned from a repository.
> - A record owned by another user is reported as 404, never 403.
> - The build has `TreatWarningsAsErrors`. A warning is a failure.
> - Do not create configuration files for your own tooling anywhere in the repository, not even
>   temporarily. If running something requires one, say so and stop.
>
> **The registration points**, and nothing else, may be edited:
> `Application/Abstractions/Ports.cs` · `Application/DependencyInjection.cs` ·
> `Infrastructure/DependencyInjection.cs` · `Persistence/AppDbContext.cs` ·
> `Persistence/DatabaseSeeder.cs` · `Api/Program.cs` · `client/src/app/app.routes.ts` ·
> `tests/TaskManager.Api.Tests/ApiFactory.cs` (shared harness, not a feature) · and, for a child
> entity, **one additive navigation link in the parent feature's list template** — a feature nobody
> can reach is not a delivered feature.
>
> **Work in this order.**
>
> *Phase 0 — Plan.* Produce, and write no code until it is approved: the files you will add or
> change; the order of the slices; which test covers which named scenario; every business rule you
> found and where you will enforce it; and every ambiguity, with the reading you propose. Then stop.
>
> *Phases 1–5 — Domain, Application, Persistence, API, Frontend.* Once the plan is approved these
> run to completion without stopping. Each one: write the tests, show the failing run, implement,
> show the passing run. Generate the migration with the CLI, never by hand, and apply it to a real
> SQL Server — a suite green on SQLite proves nothing about a migration. The frontend ships with
> tests: Karma and Jasmine are already configured, `npm run test:ci`.
>
> *Phase 6 — Close.* Update the specification's traceability table with the real test names, and
> `docs/requirements-coverage.md` if a row changes.
>
> **Report at every phase**: the command you ran and its actual output. Never report a suite as
> passing without showing the run.
>
> **Two things are required in your closing report.**
>
> 1. **Prove at least one test bites.** A suite that goes green on the first implementation run is a
>    claim, not evidence. Delete one guard that enforces a business rule, re-run, show the test that
>    fails, restore the file, re-run green. Say which rule you probed and whether anything survived
>    the mutation that should not have.
> 2. **A numbered list of every decision you made that nobody reviewed.** Design choices, naming
>    vocabulary, indexes, anything you resolved alone and then built on. This is not a confession;
>    it is the deliverable that makes running unattended safe.

---

## Review gates

Applied by a human, at the plan and again at the end. These are checks, not suggestions; a failed
check sends the work back.

| Gate | What is checked |
|------|-----------------|
| Layering | No reference to EF Core or ASP.NET from Domain or Application. `git grep` proves it |
| Rules | Every business rule in the specification appears in exactly one place, and that place is the domain or the application layer. A rule that can only live in the mapping — a delete cascade, say — must be named as the exception rather than passed over |
| Ownership | The ownership check sits in the service, returns not-found, and a test compares the foreign response against the missing response rather than against a literal string |
| Dates | No `DateTimeOffset.UtcNow` outside `SystemClock`; date-only comparisons use `DateOnly` |
| HTTP | 201 with `Location`, 204 on delete, 404 not 403, `ProblemDetails` on every error path |
| Tests | Assertions are on observable behaviour, and on what the page renders rather than on control state. A test that passes before its implementation exists is rejected as broken |
| Migration | Generated by the CLI, applied to a real SQL Server, and the schema read back from `sys.columns`, `sys.indexes` and `sys.foreign_keys` including the delete rule |
| Warnings | `dotnet build` and `npm run build` are both clean |
| Drift | The closing list of unreviewed decisions has been read, and each item accepted or sent back |

---

## Running it

1. Write the entity specification from `entity-spec-template.md` — a few minutes of typing. This is
   the step that matters: the agent executes a specification, it does not design one.
2. Give the agent this document, the specification and the repository.
3. Review the plan. Approve it, or correct it — the corrections are the interesting part, and this
   is the only place where judgement is cheap. A design fixed in prose costs a paragraph; the same
   fix after four files exist costs a rewrite.
4. Let phases 1 to 5 run.
5. Read the closing report: the mutation probe, and the unreviewed decisions.
6. Show the new endpoints in Swagger and the new screen in the SPA.

**Measured on a `ChecklistItem` entity** — a child of `TaskItem`, with a cascade, a nested route and
transitive ownership — one full run took about thirty minutes: five for the plan, one and a half for
the domain, four for the application, three for persistence, five and a half for the API, and nine
for the frontend. The frontend is the long pole. Budget accordingly, and note that the plan is
mostly reading, which is exactly what makes the rest correct.

If a phase goes wrong, that is not a failed run — the recovery is the demonstration. Read the
failure, correct the specification or the constraint that allowed it, and re-run.

**When to keep a gate between every phase instead:** an entity whose rules you are unsure of, a
change that touches something shared, or the first time you run this against a codebase. Speed is
worth having only once the shape is known.
