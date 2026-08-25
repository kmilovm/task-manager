# Entity specification — TaskItem

Input for the entity agent. See `../entity-agent.md` for the protocol.

## Identity

- **Entity name (singular):** `TaskItem`
- **Feature folder (plural, PascalCase):** `Tasks`
- **One sentence describing what it is for:** a unit of work a user has to do, with a status and
  an optional due date.

## Ownership and relationships

- **Owner:** yes, every task belongs to exactly one `User`.
- **Parent entity:** none.
- **On deleting the parent:** deleting a user is out of scope, so no cascade is configured.
- **Reachable how:** its own top-level route, `/api/tasks`.

## Fields

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `Id` | `Guid` | yes | primary key, assigned by the factory |
| `Title` | `string` | yes | trimmed, 1 to 200 characters |
| `Description` | `string` | no | at most 2000 characters |
| `Status` | `TaskItemStatus` | yes | see enums |
| `DueDate` | `DateOnly` | no | when set at creation, not earlier than today in UTC |
| `CreatedAt` | `DateTimeOffset` | yes | supplied by the caller from `IClock` |
| `CompletedAt` | `DateTimeOffset` | no | set when the status becomes `Done`, cleared otherwise |
| `OwnerId` | `Guid` | yes | the `User` that owns the task |

## Enums

| Name | Values | Default on create |
|------|--------|-------------------|
| `TaskItemStatus` | `Pending`, `InProgress`, `Done` | `Pending` |

## Business rules

Stated as BR-201 to BR-209 in `docs/specs/US-002-task-management.md`, which is the single source
of truth. Do not restate them here; read them there.

## Operations

- [x] Create
- [x] Read one
- [x] Read many
- [x] Update
- [x] Delete
- [x] Filters — `status`
- [x] Search — `title`

## Ordering

Due date ascending with undated tasks last, then creation time descending (BR-209).

## Acceptance criteria

`docs/specs/US-002-task-management.md`. Twenty-one named scenarios; every one needs a test named
in that file's traceability table.

## Out of scope

Sub-tasks, attachments, comments, labels, reminders, sharing or assigning to another user,
recurring tasks, soft delete and audit trail, server-side pagination.
