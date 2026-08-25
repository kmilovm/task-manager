# Building a feature with a GenAI agent

The exercise asks for the prompt used to generate a task management API, a sample of what came
out, and an account of how the output was validated and corrected. This is that account.

It is not a hypothetical. **The entire `Tasks` feature in this repository — domain, application
services, persistence, endpoints, the Angular screens and 172 of the 256 backend tests — was
produced by driving an agent through the protocol below.** The 68 frontend tests were written by
hand afterwards, for the reason given in §7. The `Users` feature was written by hand first, on
purpose, so there would be a reference implementation to point the agent at and a fair comparison
between the two halves of the same codebase.

---

## 1. The approach, and why it is shaped this way

The quality of generated code is bounded by the quality of its context. A chat prompt is a poor
context pack; a written specification is a good one. So the order of work was:

1. Write the specification first — `docs/specs/US-002-task-management.md`: eleven numbered
   business rules and twenty-three named Gherkin scenarios.
2. Write down the conventions the code already follows — `docs/architecture.md`: the dependency
   rule, the anatomy of a vertical slice file by file, and the three rules that actually get
   broken in practice.
3. Only then involve the agent, and forbid it from designing anything. Its job is to reproduce an
   existing slice against a specification that already states the rules.

The three failure modes this guards against are the ones these tools reliably hit: quietly
breaking a layer boundary, quietly dropping a business rule that was never written down, and
producing tests that assert the implementation rather than the behaviour.

The full operating manual is `docs/genai/entity-agent.md`, and the specific input for this feature
is `docs/genai/specs/task.md`. It is written to be tool-agnostic — nothing in it depends on which
assistant you paste it into.

---

## 2. The prompt

The prompt has three parts: a reading list, a set of hard constraints, and a phased procedure with
a human gate between phases. Reproduced from `docs/genai/entity-agent.md`:

> You are adding a new feature to an existing .NET 10 + Angular 19 codebase that follows Clean
> Architecture. Your job is to produce a complete vertical slice for the entity described in the
> attached specification.
>
> **Read first, in this order:** `docs/architecture.md`; `docs/specs/README.md`; the `Users`
> feature end to end as the reference implementation; the entity specification you have been given.
>
> **Hard constraints.**
> - Do not modify any existing feature. You add files; you touch existing files only where the
>   architecture document says a feature registers itself.
> - Do not add a NuGet or npm package. If you believe one is required, stop and say so.
> - Tests come before production code, always, and you show the failing run before implementing.
> - No business rule in an endpoint. No `DateTimeOffset.UtcNow` outside the clock adapter. No
>   `IQueryable` returned from a repository.
> - A record owned by another user is reported as 404, never 403.
> - The build has `TreatWarningsAsErrors`. A warning is a failure.
> - Do not create configuration files for your own tooling anywhere in the repository.
>
> **Work in this order.**
>
> *Phase 0 — Plan.* Produce, and write no code until it is approved: the files you will add or
> change; the order of the slices; which test covers which named scenario; every business rule you
> found and where you will enforce it; every ambiguity, with the reading you propose. Then stop.
>
> *Phases 1–5* — domain, application, persistence, API, frontend. Once the plan is approved these
> run to completion. Each one: write the tests, show the failing run, implement, show the passing
> run.
>
> **Report at every phase**: the command you ran, its actual output, and — when a test failed for
> a reason you did not expect — what you changed and why. Never report a suite as passing without
> showing the run.

Three details in that prompt did most of the work:

**"Do not write any code until the plan is approved."** The plan is where judgement is cheapest to
apply. Correcting a design in prose costs a paragraph; correcting it after four files exist costs
a rewrite.

**"Show the failing run."** This is what makes the difference between test-first and
tests-written-alongside. It also catches vacuous tests — see §4.

**"Stop and say so."** Given an explicit way to refuse, the agent used it instead of improvising.
It never added a package.

The protocol originally gated between every phase. A second run, rehearsing a `ChecklistItem`
entity, was deliberately given three layers to work through unattended so the cost could be
measured. It produced no defect — and the agent's own account of what it did cost is the reason
the protocol now asks for a closing list of unreviewed decisions:

> Unattended layers do not break the build, they accumulate small decisions that compile. The
> failure mode is drift, and drift is invisible in a green run.

The mitigation is one paragraph of reporting rather than five stops, so the protocol's default is
now plan, one approval, then run to completion.

---

## 3. A representative sample of the output

Three files, as generated, after the corrections in §5 were applied.

**The entity** — `src/TaskManager.Domain/Tasks/TaskItem.cs`. Note that `Create` takes no status
argument at all: BR-204 says a new task starts as `Pending`, and the agent made that structural
rather than a default parameter, so no caller can bypass it.

```csharp
public static TaskItem Create(
    string? title,
    string? description,
    DateOnly? dueDate,
    Guid ownerId,
    DateTimeOffset createdAt)
{
    if (ownerId == Guid.Empty)
    {
        throw new DomainException("Owner is required.");
    }

    var normalisedTitle = NormaliseTitle(title);
    var normalisedDescription = NormaliseDescription(description);

    if (dueDate is { } due && due < DateOnly.FromDateTime(createdAt.UtcDateTime))
    {
        throw new DomainException("Due date cannot be in the past.");
    }

    return new TaskItem(Guid.NewGuid(), normalisedTitle, normalisedDescription, dueDate, ownerId, createdAt);
}
```

**The authorisation rule** — `src/TaskManager.Application/Tasks/TaskService.cs`. One private method
is the only gate, and it answers "missing" and "someone else's" identically:

```csharp
private async Task<TaskItem> RequireOwnedAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
{
    var task = await _tasks.GetByIdAsync(id, cancellationToken);

    return task is null || task.OwnerId != ownerId
        ? throw new NotFoundException("Task not found.")
        : task;
}
```

The repository's `GetByIdAsync` deliberately does **not** filter by owner. If it did, the rule
would have two homes and one of them would be silent.

**An endpoint** — `src/TaskManager.Api/Endpoints/TaskEndpoints.cs`. There is no `if` anywhere in
the file. Ownership, validation and not-found all arrive as exceptions that `ApiExceptionHandler`
maps to RFC 7807 responses:

```csharp
group.MapPut("/{id:guid}", async (
        Guid id,
        UpdateTaskRequest request,
        ClaimsPrincipal principal,
        ITaskService tasks,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await tasks.UpdateAsync(principal.GetUserId(), id, request, cancellationToken)))
    .WithSummary("Replaces a task owned by the signed-in account. Omitted optional fields are cleared.")
    .Produces<TaskDto>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status404NotFound);
```

---

## 4. How the suggestions were validated

The short version: **by reading the code and running the system, never by reading the agent's
report.** A report is a claim. Every claim below was checked independently.

**Read the diff, not the summary.** Every file was read line by line at each gate. That is how the
frontend store's coupling to the layout component was found (§5.6) — the build was green and the
tests were green and it would never have surfaced from a summary.

**Check factual claims that are cheap to check.** The agent stated that
`FluentValidation.Transform` no longer exists in version 12 and that a validator therefore cannot
measure a trimmed value. That is exactly the shape of a plausible hallucination, so it was
verified by compiling a probe file that uses `Transform` against the installed package:

```
error CS1061: 'IRuleBuilderInitial<R, string>' does not contain a definition for 'Transform'
```

True. But the cost of checking was thirty seconds, and the cost of not checking is a design
decision justified by a fiction.

**Verify architectural claims mechanically.** "The business logic layer is independent of the data
layer" is a claim a grep can settle:

```
grep -rn "EntityFrameworkCore|Microsoft.AspNetCore" src/TaskManager.Domain src/TaskManager.Application   -> 0
grep -rn "DateTimeOffset.UtcNow" src/ --exclude SystemClock.cs --exclude Migrations/                     -> 0
grep -rn "IQueryable" src/TaskManager.Application src/.../Repositories                                    -> 0
```

**Check the specification, not just the tests.** A script extracts every test name from the
traceability tables in `docs/specs/` and compares it against the test methods that actually exist.
It found four missing names — and one of them was a mistake in the specification, not the code:
a class had been renamed in one spec and not the other.

**Do not trust a green suite about the database.** Infrastructure and API tests run on SQLite in
memory using `EnsureCreated`, which builds the schema from the model and never executes a
migration. A broken migration would leave the entire suite green. So the migration was applied to
the real SQL Server instance and the resulting schema read back from the catalogue:

```
FK_Tasks_Users_OwnerId  ->  Tasks.OwnerId -> Users.Id, delete rule NO_ACTION
IX_Tasks_OwnerId        ->  non-unique
DueDate                 ->  date        Status -> nvarchar(16)      CreatedAt -> datetime2
```

**Drive the running system by hand.** This found two defects that 256 passing tests did not
(§5.5). No test suite is a substitute for exercising the real thing once.

**Measure coverage, and be honest about the number.** 95.7% of hand-written lines — Application
100%, Infrastructure 100%, Api 99.2%, Domain 89.4%. Without excluding generated code the number is
67%, dragged down by a 383-line file emitted by a source generator. `coverlet.runsettings` records
the exclusions so the figure is reproducible rather than quotable only by the person who ran it.

---

## 5. What had to be corrected

The interesting part. These are the actual corrections, in the order they happened.

### 5.1 It bent the design to fit a document instead of proposing a change to the document

The Phase 0 plan put status filtering and title search **in memory**, inside the application
service: load every task belonging to the user, then filter in C#. Its justification was internally
consistent — the traceability table mapped those scenarios to `TaskServiceTests`, and filtering in
SQL would reduce those tests to asserting that an argument was forwarded to a mock.

The reasoning was honest and the conclusion was wrong. A reviewer flags "loads all rows and filters
in memory" in five seconds. The correct move was to propose changing the table, not the design.

Fixed by pushing the filter into the query and moving three scenarios to `TaskRepositoryTests`,
where they are proven against a real database engine.

This is the most instructive failure of the run, because nothing about it looks like an error. It
is a locally-sensible decision that only reads as wrong from one step back — which is the whole
argument for a human gate.

### 5.2 It described a relationship without declaring one

The plan treated `OwnerId` as a plain `Guid` and never mentioned a foreign key or an index. Without
the key, referential integrity exists only in C#; without the index, every list query scans the
table. Both were made explicit requirements before Phase 3.

### 5.3 It recorded business decisions as test names

Two genuine ambiguities in the specification were resolved well: that the past-due-date rule
applies only at creation, and that a task already `Done` keeps its original completion time when
edited. But the agent intended to record both **only as test method names**.

A business decision belongs in the specification. Both were promoted to BR-210 and BR-211 with two
new Gherkin scenarios, and the tests now trace back to written rules.

### 5.4 It could not see across the boundary that keeps it safe

Phase 3 hit a real wall. Implementing BR-209's ordering produced:

```
System.NotSupportedException : SQLite does not support expressions of type 'DateTimeOffset' in
ORDER BY clauses. Convert the values to a supported type, or use LINQ to Objects to order the
results on the client side.
```

The agent handled this well. It fixed it in the **mapping** rather than the query — a value
converter storing instants as UTC — and it explicitly refused to apply the converter only for
SQLite, on the grounds that a provider-specific mapping would mean the test suite exercises a
different schema than production.

But the fix left `Users.CreatedAt` as `datetimeoffset` and `Tasks.CreatedAt` as `datetime2`: two
mappings for one concept. The agent could not see it, because the constraint that keeps it safe —
*do not modify an existing feature* — is exactly what blinds it to cross-cutting consistency.

Fixed by hand: the converter moved to `AppDbContext.ConfigureConventions` so it applies to every
`DateTimeOffset` in the model, plus a migration. **This is a structural limitation of constrained
agents, not a mistake, and it is the clearest argument for why the gate has to be a person who can
see the whole repository.**

### 5.5 Two defects that a green suite could not see

With 256 tests passing, the live API was probed by hand:

```
GET /api/tasks?status=bogus       -> 500   "An error occurred while processing your request."
GET /api/tasks?status=inprogress  -> 500   (right word, wrong case)
GET /api/tasks?status=99          -> 200   []
```

A client typing the wrong case got a server error. An undefined enum value was silently accepted
and returned an empty list indistinguishable from "you have no tasks in progress".

Sent back with a diagnosis to confirm or correct. The agent confirmed the chain and **corrected the
explanation in a way that mattered**: `RouteHandlerOptions.ThrowOnBadRequest` defaults to `true` in
Development and `false` elsewhere. The framework never produced a 400 that we downgraded — in
Development it throws an exception *carrying* `StatusCode = 400` and expects the application to
honour it, and our handler ignored it so the fallback wrote a 500. Its integration tests run in the
`Testing` environment, where the request never throws, so **the suite was structurally incapable of
seeing the defect**. It proved this with a test that forces the option rather than asserting it.

Both fixed: `BadHttpRequestException` is now mapped by its own status code, and `GetAllAsync`
rejects an undefined status with a validation failure keyed `status`.

### 5.6 It coupled a layout component to a feature

The Angular store was registered application-wide, so signing out could leave one account's rows
visible to the next. The agent solved this by calling `tasksStore.reset()` from the shell's sign-out
handler — and flagged the smell itself rather than hiding it.

Fixed by moving the reset into the store as an effect that watches the session. The dependency now
runs feature → core instead of layout → feature, and no component has to remember to clear anything.

### 5.7 It left a tool artefact behind

To run the dev server, the agent created a configuration file for its own tooling inside the
repository. It deleted the file and reported having done so, unprompted, and verification confirmed
the directory never entered version control.

Nonetheless the constraint list now forbids creating tool configuration files at all. **An agent
with shell access will leave traces of its own tooling unless told not to**, and "it cleaned up
after itself this time" is not a control.

### What it got right, including twice over its author

Worth recording, because a write-up that only lists failures is as unbalanced as one that lists none.

- It **rejected an abstraction from the original human plan**. That plan included an `ICurrentUser`
  port. The agent argued it was unnecessary: the endpoint already resolves the `Guid` and passes it
  as a parameter, so the port would add an abstraction, a registration and a piece of ambient state
  to obtain the same value, with no test that could not otherwise be written. It was right.
- It **reversed its own plan** for the same reason, dropping a `TaskListQuery` wrapper record once
  it found the type had no behaviour to justify it.
- It **caught two of its own vacuous tests**. Two Phase 4 tests asserted only a 404 status — which an
  unmatched route also returns — so they could not distinguish routing from the service. It
  strengthened both to assert the problem detail body, and said so rather than banking the pass.
- It **caught its own test error** rather than the code's: a 405 on `POST /api/tasks/{id}` was the
  test using the wrong URL, and it fixed the test, not the route table.
- It was **honest about the limits of its own work**: that the seeder tests are characterization
  tests rather than TDD because the seeder already existed, and that it could not watch one of them
  fail because reverting the code under test was outside its authorised scope.

---

## 6. Edge cases, authentication and validation

**Validation lives in two layers on purpose, with one definition of each number.** FluentValidation
validators run in the application service — not in an endpoint filter — so the rule holds regardless
of entry point and is unit-testable without HTTP. They cite the domain constants
(`TaskItem.MaxTitleLength`) rather than repeating literals, so the boundary check and the invariant
cannot drift. The domain guards again, because a validator is a courtesy to the caller and an
invariant is a guarantee.

One inconsistency survived several reviews before being caught: the validators measured the raw
string while the domain measures the trimmed one, so a 200-character title padded with spaces was
rejected at the boundary although the domain would have accepted it. `FluentValidation.Transform`,
which existed for exactly this, was removed in version 12 (§4). The fix is a shared
`MaximumTrimmedLength` rule that measures what the domain measures, applied at all five call sites,
with tests pinning the padded case. It is worth recording that this was documented as a deliberate
trade-off for a while before it was recognised as a defect — consistency with an existing behaviour
is a comfortable reason to leave something wrong.

**Authentication.** JWT bearer, HS256, sixty-minute lifetime, `ClockSkew` set to zero because the
five-minute default would let a token expired three minutes ago through. `MapInboundClaims` is off
so the claim read is exactly the claim issued. Passwords are BCrypt with an explicit work factor,
and `Verify` catches a malformed hash and returns false rather than turning a corrupt row into a 500.

**Authorisation.** Ownership is enforced in the application service, never in an endpoint, and a
record owned by someone else is reported as **404 rather than 403** so the API does not confirm the
existence of records the caller cannot see. Two tests defend this: one asserts the repository is
queried by id alone, and one compares the foreign-record response against the missing-record
response so that differentiating them fails the build.

**Time.** Every rule that depends on the clock takes it through `IClock`; there is no
`DateTimeOffset.UtcNow` outside the adapter. "Today" for the due-date rule is computed in UTC, with
a test that pins it against local time. Instants are stored as UTC `datetime2` throughout.

**Enumerations at the boundary.** Statuses cross the wire as strings (`"InProgress"`), never
ordinals. An unparseable value returns 400 problem+json naming the parameter; an undefined numeric
value returns 400 with `errors.status`. Both were defects until they were probed by hand (§5.5).

**PUT semantics.** `PUT` is a total replacement: a null or absent `description` or `dueDate` means
*cleared*, never *unchanged*. The alternative — a nullable field meaning "leave alone" — needs a
tri-state sentinel and makes it impossible to clear a value at all. If a partial update is ever
wanted it arrives as `PATCH`.

**Edge cases pinned by tests.** Titles of exactly 200 characters and 201; descriptions of exactly
2000; whitespace-only titles and descriptions; due dates of today and of yesterday; a task moved to
`Done` and back and forward again; a task updated while already `Done`; a `null` due date meaning
cleared; deleting a task and then requesting it; a second user reading, updating and deleting the
first user's task; expired tokens, tokens signed with the wrong key, and no token at all.

---

## 7. What this process does not give you

- **A false claim nearly became a documented gap.** The agent reported that the repository had no
  Angular test harness and that adding one required npm packages, so Phase 5 shipped without tests
  and this document originally recorded that as accepted debt. It was wrong: Karma and Jasmine
  were already in `package.json`, `ng test` was already configured in `angular.json`, and
  `tsconfig.spec.json` already existed. The claim was accepted without checking — the exact failure
  this write-up warns against, committed by the reviewer rather than the agent. The SPA now has 68
  tests, written afterwards by hand.
- **Rehearsing the agent found a defect in the hand-written half.** Running the protocol again on a
  throwaway copy, for a `ChecklistItem` entity, the agent hit a validation message that never
  rendered. The cause was in `FieldErrorComponent`, written by hand in the first half of this
  project: it is `OnPush` and reads `touched` and `errors` off a `FormControl`, which is not a
  signal, so marking a form touched changed no input and the child view was never re-rendered.
  Login, register and the task form were all affected, and no test caught it because all three
  asserted control state rather than what the page shows. The agent could not fix it — a shared
  component is not a sanctioned registration point — so it worked around the defect in its own
  feature and reported the divergence instead. Both the defect and the missing kind of test are
  now fixed.
- **Characterization tests are not TDD.** The seeder tests passed on their first run because the
  seeder already existed. That is correct, but it is a different activity from the red-green pairs
  in the rest of the history.
- **A test that passes before its implementation exists is broken.** This happened twice — once in
  the hand-written half of the codebase and once in the generated half — and both times only the
  discipline of looking at the red run caught it.
- **The gate is the expensive part.** The agent produced a feature in five phases; the corrections
  in §5 came from reading every line, compiling a probe, running a script over the specifications,
  applying a migration to a real database and probing a running API by hand. Most of the value of
  this workflow is in that work, not in the generation.
