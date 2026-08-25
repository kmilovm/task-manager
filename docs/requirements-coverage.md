# Requirements coverage

Every requirement of the exercise statement, and where it is satisfied in this repository.

## Backend

| # | Requirement | Where |
|---|-------------|-------|
| 1 | Database with at least one table for application data | `Tasks` table — `src/TaskManager.Infrastructure/Persistence/Configurations/TaskItemConfiguration.cs` |
| 2 | An additional table to store users | `Users` table — `src/TaskManager.Infrastructure/Persistence/Configurations/UserConfiguration.cs` |
| 3 | Unique identifier (primary key) plus at least two other fields | `Tasks`: `Id` (PK) + `Title`, `Description`, `Status`, `DueDate`, `CreatedAt`, `CompletedAt`, `OwnerId`. `Users`: `Id` (PK) + `Email`, `DisplayName`, `PasswordHash`, `CreatedAt` |
| 4 | ASP.NET Web API with CRUD endpoints | `src/TaskManager.Api/Endpoints/TaskEndpoints.cs` |
| 5 | Appropriate HTTP verbs, parameters and return values | `GET /api/tasks`, `GET /api/tasks/{id}`, `POST /api/tasks` (201 + `Location`), `PUT /api/tasks/{id}` (200), `DELETE /api/tasks/{id}` (204); errors as `application/problem+json` |
| 6 | A second API with user creation and user login | `src/TaskManager.Api/Endpoints/AuthEndpoints.cs` — `POST /api/auth/register`, `POST /api/auth/login` |
| 7 | Authorized and non-authorized endpoints | Non-authorized: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/public`, `GET /api/health`. Authorized: `GET /api/auth/me` and everything under `/api/tasks` |
| 8 | Data access layer providing the CRUD operations | `src/TaskManager.Infrastructure/Persistence/Repositories/` behind the `ITaskRepository` / `IUserRepository` ports declared in the application layer |
| 9 | Business logic layer with all business rules and validation | `src/TaskManager.Domain` (invariants) and `src/TaskManager.Application` (use cases, validators) |
| 10 | Business logic independent of the data layer and the API | `TaskManager.Domain` has no project references; `TaskManager.Application` references only `TaskManager.Domain`. Persistence is reached through interfaces the application layer owns |
| 11 | Unit tests for the data access layer | `tests/TaskManager.Infrastructure.Tests` |
| 12 | Unit tests for the business logic layer | `tests/TaskManager.Domain.Tests`, `tests/TaskManager.Application.Tests` |
| 13 | Unit tests for the API endpoints | `tests/TaskManager.Api.Tests` |

## Frontend

| # | Requirement | Where |
|---|-------------|-------|
| 14 | Integrated with a frontend framework | Angular 19 SPA in `client/`, consuming the API over HTTP with a bearer token interceptor |
| 15 | Responsive | Fluid layout, single column below 768px, card list on small screens and table on large ones |
| 16 | User-friendly | Inline validation messages, loading and empty states, confirmation before delete, keyboard-accessible forms |
| 17 | CRUD operations for the use case | Create, list, filter, edit and delete tasks from the SPA |
| 18 | Structured code, clean components and state | Feature folders, standalone components, a single signal-based store per feature, typed HTTP services. 68 unit tests under `client/src/app`, run with `npm run test:ci` |
| 19 | No warnings in the browser console | Verified manually; strict template checking and strict TypeScript are enabled |

## Submission

| # | Requirement | Where |
|---|-------------|-------|
| 20 | README with setup instructions | `README.md` |
| 21 | Seeded data and credentials for the demo | `src/TaskManager.Infrastructure/Persistence/DatabaseSeeder.cs`, credentials listed in the README |
| 22 | Informal user story, included in the presentation | `docs/specs/US-001-account-access.md`, `docs/specs/US-002-task-management.md` |

## Generative AI section

| # | Requirement | Where |
|---|-------------|-------|
| 23 | The prompt used to generate the API scaffold or implementation | `docs/genai.md` |
| 24 | The output code, or a representative sample | `docs/genai.md` |
| 25 | How the suggestions were validated | `docs/genai.md` |
| 26 | How the output was corrected or improved | `docs/genai.md` |
| 27 | How edge cases, authentication and validation were handled | `docs/genai.md` |

## Interpretation notes

Two points in the statement admit more than one reading. The choices made here are:

- **"ASP.NET MVC, Web API"** — the API is built with minimal APIs grouped by feature
  (`MapGroup`) returning `TypedResults`. Minimal APIs are part of ASP.NET Core Web API and cover
  what the criteria actually assess: correct verbs, bound parameters, explicit return types and a
  clean separation from the business layer. The MVC controller stack was not used because it adds
  a class-per-feature and a filter pipeline this application has no requirement for, and
  server-side Razor views are irrelevant here since the statement asks for a separate SPA.
- **"a second API"** — implemented as a second, independently routed API surface
  (`/api/auth`) with its own endpoint group, DTOs, service and authorisation profile, rather than a
  second host process. A separate deployable would add operational cost without changing the
  design of the exercise.
