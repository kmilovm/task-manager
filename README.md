# Task Manager

A small task manager built as a technical exercise: an ASP.NET Core Web API over SQL Server with a
Clean Architecture layout, and an Angular single-page client.

Signed-in users create, list, filter, edit and delete their own tasks. A task is owned by exactly
one account and is invisible to everyone else.

---

## Running it

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/) or newer
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running

### 1. Start SQL Server

```bash
docker compose up -d
```

SQL Server 2022 listens on **port 14333**, not the usual 1433, so it does not collide with a local
SQL Server instance if you have one. Give it a few seconds on first run; `docker compose ps` shows
`healthy` when it is ready.

### 2. Start the API

```bash
dotnet run --project src/TaskManager.Api
```

In Development the API applies any pending migrations and seeds the demo data on startup, so there
is no separate database step. It listens on **http://localhost:5163**, with Swagger at
**http://localhost:5163/swagger**.

### 3. Start the client

```bash
npm --prefix client ci
```

```bash
npm --prefix client start
```

Open **http://localhost:4200**.

Use the npm scripts rather than a global `ng`. The CLI version then comes from `package-lock.json`
and is the same for everyone; a globally installed `ng` is an undeclared dependency that may be
absent, or bound to a different Node version.

### Demo credentials

Both accounts use the password `Passw0rd!`.

| Email | Tasks |
|-------|-------|
| `ada@example.com` | three, one of each status |
| `grace@example.com` | one |

Signing in as Grace and looking for Ada's tasks is the quickest way to see the ownership rule: they
are not filtered out of the list, they do not exist as far as her session is concerned, and
requesting one by id returns 404 rather than 403.

---

## Testing

```bash
dotnet test
```

```bash
npm --prefix client run test:ci
```

327 tests: 259 on the backend across four projects, 68 on the client. The backend suite needs no
container — infrastructure and API tests run against SQLite in memory, while production uses SQL
Server. The client suite runs in headless Chrome.

Line coverage of hand-written backend code is **95.7%** (Application 100%, Infrastructure 100%,
Api 99.2%, Domain 89.4%):

```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

`coverlet.runsettings` excludes generated code and migrations. Without those exclusions the figure
is 67%, dragged down by a 383-line file emitted by a source generator.

### Test-driven development

The history is the evidence. Each slice was built as a red/green pair — a commit adding failing
tests, then the commit that makes them pass:

```bash
git log --oneline
```

---

## How it is put together

```
src/TaskManager.Domain          entities, value objects, invariants — no project references
src/TaskManager.Application     use cases, DTOs, validators, ports        -> Domain
src/TaskManager.Infrastructure  EF Core, repositories, JWT, hashing       -> Application
src/TaskManager.Api             endpoints, auth pipeline, error mapping   -> Application
client                          Angular 19 SPA
```

Dependencies point inward. `TaskManager.Api` references `TaskManager.Infrastructure` only to wire
dependency injection. The rule is checked, not asserted: `TaskManager.Domain` and
`TaskManager.Application` contain zero references to EF Core or ASP.NET.

`docs/architecture.md` has the full picture — the anatomy of a feature file by file, the
conventions, and what was deliberately left out and why.

### The API

| Endpoint | Auth | Returns |
|---|---|---|
| `POST /api/auth/register` | anonymous | 201 + `Location` |
| `POST /api/auth/login` | anonymous | 200 with a bearer token |
| `GET /api/auth/me` | **required** | 200, the caller's profile |
| `GET /api/auth/public` | anonymous | 200 |
| `GET /api/health` | anonymous | 200 |
| `GET /api/tasks?status=&search=` | **required** | 200 |
| `GET /api/tasks/{id}` | **required** | 200 |
| `POST /api/tasks` | **required** | 201 + `Location` |
| `PUT /api/tasks/{id}` | **required** | 200 |
| `DELETE /api/tasks/{id}` | **required** | 204 |

Errors are RFC 7807 `application/problem+json`. Validation failures carry an `errors` object keyed
by field in camelCase. A task owned by somebody else is reported as **404, never 403**, so the API
does not confirm the existence of records the caller cannot see.

`PUT` is a total replacement: a null or absent `description` or `dueDate` means *cleared*, not
*unchanged*.

---

## Documentation

| Document | What it is |
|---|---|
| [`docs/specs/`](docs/specs/) | The user stories, their business rules, Gherkin acceptance criteria, and a table tracing every scenario to the test that proves it |
| [`docs/architecture.md`](docs/architecture.md) | The dependency rule, the anatomy of a feature, the conventions |
| [`docs/requirements-coverage.md`](docs/requirements-coverage.md) | Every requirement of the exercise mapped to the file that satisfies it |
| [`docs/genai.md`](docs/genai.md) | How the `Tasks` feature was produced with a GenAI agent, what had to be corrected, and what the process does not give you |
| [`docs/genai/`](docs/genai/) | The agent's operating manual and the entity specification template it consumes |

Development was specification-first: the rules and the Gherkin scenarios were written before any
code, and the test names are derived from them.

---

## Decisions worth knowing about

**Minimal APIs rather than MVC controllers.** The brief lists "ASP.NET MVC, Web API". Minimal APIs
are part of ASP.NET Core Web API and cover what is actually being assessed — correct verbs, bound
parameters, explicit return types, and a clean separation from the business layer — without a
class-per-feature and a filter pipeline this application has no use for. Server-side Razor views
would be redundant given the brief separately asks for a SPA.

**"A second API" is a second routed surface, not a second process.** `/api/auth` has its own
endpoint group, DTOs, service and authorisation profile. A separate deployable would add
operational cost without changing the design under review.

**No `IUnitOfWork`, no MediatR, no CQRS, no generic repository, no AutoMapper.** `DbContext` is
already a unit of work, and nothing here spans two entities. Every abstraction in this codebase has
to justify itself with a test that could not otherwise be written.

**Business rules live in the domain and application layers, never in an endpoint.** The ownership
check is in `TaskService`, not in the route handler, so it holds for any future entry point and is
unit-testable without HTTP.

**Instants come from an injected clock.** There is no `DateTimeOffset.UtcNow` outside
`SystemClock`, which is what lets tests assert exact timestamps instead of tolerating a window.

---

## Known limitations

Stated rather than hidden.

- **Out of scope by choice**, and listed as such in the specs: refresh tokens, password reset,
  roles, sub-tasks, sharing, soft delete and audit trail, server-side pagination.
- **`?status=1` works as well as `?status=InProgress`.** Numeric ordinals bind, which
  `Enum.IsDefined` permits. Responses always serialise the string form.
- **The access token is kept in `localStorage`.** That is the common choice for a SPA with no
  backend-for-frontend, and it is exposed to cross-site scripting: any injected script can read it.
  The stronger design is an `httpOnly`, `SameSite` cookie issued by the API plus CSRF protection,
  which the browser will not hand to a script at all. That is a different authentication design
  rather than a patch, so it is named here rather than half-done.
- **Nothing is transactional across two entities**, because no use case needs it. A rule that
  counted rows before inserting would not be race-safe as written.
- **The demo signing key is in `appsettings.Development.json`** and is clearly marked as such. A
  real deployment supplies it from configuration or a secret store.
