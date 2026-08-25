# US-002 — Personal task management

## Story

**As a** signed-in user
**I want to** create, review, update and delete my own tasks
**so that** I always know what I have to do and when it is due.

## Context

This is the core use case of the application. A task is owned by exactly one user and is never
shared, which keeps the authorisation rule simple: the owner is the only actor. Ownership is
enforced in the application layer rather than in the controller so that the rule holds for any
future entry point and can be unit tested without HTTP.

## Business rules

| ID | Rule |
|----|------|
| BR-201 | A task has an identifier, a title, an optional description, a status, an optional due date and an owner. |
| BR-202 | The title is required, is trimmed, and is between 1 and 200 characters. |
| BR-203 | The description is optional and at most 2000 characters. |
| BR-204 | The status is one of `Pending`, `InProgress`, `Done`. A new task starts as `Pending`. |
| BR-205 | A due date is optional. When supplied at creation time it must not be earlier than today (UTC). |
| BR-206 | Moving a task to `Done` stamps its completion time. Moving it back out clears that stamp. |
| BR-207 | A user can only read, change or delete tasks they own. Tasks owned by somebody else are indistinguishable from tasks that do not exist. |
| BR-208 | Deleting a task removes it permanently. |
| BR-209 | The list is ordered by due date ascending with undated tasks last, then by creation time descending. |
| BR-210 | The past-due-date rule applies only when a task is created. An existing task may be moved to a date in the past, so a slipped deadline can be recorded honestly. |
| BR-211 | Completion time records when the work was finished, not when the record was last touched: updating a task that is already `Done` keeps its original completion time. |

## Acceptance criteria

```gherkin
Feature: Personal task management
  In order to keep track of my work
  As a signed-in user
  I want to manage my own tasks

  Background:
    Given I am signed in as "ada@example.com"
    And the following tasks exist for me:
      | title               | status     | dueDate    |
      | Write the report    | Pending    | 2030-01-10 |
      | Review the deck     | InProgress | 2030-01-05 |
      | Archive last sprint | Done       |            |
    And a task "Quarterly forecast" exists for "grace@example.com"

  Scenario: Creating a task with only a title
    When I create a task titled "Book the meeting room"
    Then the task is created with status "Pending"
    And the task has no due date
    And the task is owned by me

  Scenario: Creating a task with a description and a due date
    When I create a task titled "Prepare invoices" with description "Q1 batch" due "2030-03-31"
    Then the task is created with status "Pending"
    And the task due date is "2030-03-31"

  Scenario: Creating a task without a title
    When I create a task titled ""
    Then the request is rejected with status 400
    And the validation error mentions the field "title"

  Scenario: Creating a task with a title longer than 200 characters
    When I create a task with a title of 201 characters
    Then the request is rejected with status 400
    And the validation error mentions the field "title"

  Scenario: The title is trimmed
    When I create a task titled "  Call the supplier  "
    Then the stored title is "Call the supplier"

  Scenario: Creating a task due in the past
    When I create a task titled "Late task" due "2020-01-01"
    Then the request is rejected with status 400
    And the message is "Due date cannot be in the past."

  Scenario: Moving an existing task to a date in the past
    When I update "Write the report" with due date "2020-01-01"
    Then the task due date is "2020-01-01"

  Scenario: Listing my tasks
    When I request my task list
    Then I see 3 tasks
    And I do not see "Quarterly forecast"

  Scenario: Tasks are ordered by due date with undated tasks last
    When I request my task list
    Then the titles are in the order "Review the deck", "Write the report", "Archive last sprint"

  Scenario: Filtering by status
    When I request my task list filtered by status "InProgress"
    Then I see 1 task
    And I see "Review the deck"

  Scenario: Searching by title
    When I search my task list for "report"
    Then I see 1 task
    And I see "Write the report"

  Scenario: Searching is case-insensitive
    When I search my task list for "REPORT"
    Then I see "Write the report"

  Scenario: Reading a single task
    When I request the task "Write the report"
    Then I see its title, description, status, due date and creation time

  Scenario: Updating a task
    When I update "Write the report" with title "Write the annual report" and status "InProgress"
    Then the stored title is "Write the annual report"
    And the stored status is "InProgress"

  Scenario: Completing a task records when it was completed
    When I update "Write the report" with status "Done"
    Then the task is marked as completed
    And the completion time is set

  Scenario: Reopening a completed task clears the completion time
    When I update "Archive last sprint" with status "Pending"
    Then the completion time is empty

  Scenario: Updating a task that is already done keeps its completion time
    When I update "Archive last sprint" with title "Archive the last sprint"
    Then the completion time is unchanged

  Scenario: Clearing a due date
    When I update "Write the report" with no due date
    Then the task has no due date

  Scenario: Deleting a task
    When I delete "Review the deck"
    Then my task list has 2 tasks
    And requesting "Review the deck" is rejected with status 404

  Scenario: Reading a task that belongs to somebody else
    When I request "Quarterly forecast"
    Then the request is rejected with status 404

  Scenario: Updating a task that belongs to somebody else
    When I update "Quarterly forecast" with title "Hijacked"
    Then the request is rejected with status 404
    And the task keeps the title "Quarterly forecast"

  Scenario: Deleting a task that belongs to somebody else
    When I delete "Quarterly forecast"
    Then the request is rejected with status 404
    And the task still exists

  Scenario: Requesting a task that does not exist
    When I request the task "00000000-0000-0000-0000-000000000000"
    Then the request is rejected with status 404
```

## Out of scope

Sub-tasks, attachments, comments, labels, reminders and notifications, sharing or assigning a
task to another user, recurring tasks, soft delete and audit trail, server-side pagination.

## Traceability

| Scenario | Test |
|----------|------|
| Creating a task with only a title | `TaskItemTests.Create_WithTitleOnly_StartsAsPendingWithoutDueDate` |
| Creating a task with a description and a due date | `TaskServiceTests.CreateAsync_WithFullPayload_PersistsTask` |
| Creating a task without a title | `CreateTaskRequestValidatorTests.Validate_WithEmptyTitle_Fails` |
| Creating a task with a title longer than 200 characters | `CreateTaskRequestValidatorTests.Validate_WithTooLongTitle_Fails` |
| The title is trimmed | `TaskItemTests.Create_TrimsTitle` |
| Creating a task due in the past | `TaskItemTests.Create_WithPastDueDate_Throws` |
| Moving an existing task to a date in the past | `TaskItemTests.Update_WithAPastDueDate_IsAllowed` |
| Listing my tasks | `TaskServiceTests.GetAllAsync_ReturnsOnlyTasksOwnedByCaller` |
| Tasks are ordered by due date with undated tasks last | `TaskRepositoryTests.ListAsync_OrdersByDueDateThenCreatedAt` |
| Filtering by status | `TaskRepositoryTests.ListAsync_FiltersByStatus` |
| Searching by title | `TaskRepositoryTests.ListAsync_FiltersBySearchTerm` |
| Searching is case-insensitive | `TaskRepositoryTests.ListAsync_SearchIsCaseInsensitive` |
| Reading a single task | `TaskEndpointTests.GetTask_WhenOwned_ReturnsTask` |
| Updating a task | `TaskServiceTests.UpdateAsync_WhenOwned_AppliesChanges` |
| Completing a task records when it was completed | `TaskItemTests.ChangeStatus_ToDone_SetsCompletedAt` |
| Reopening a completed task clears the completion time | `TaskItemTests.ChangeStatus_FromDone_ClearsCompletedAt` |
| Updating a task that is already done keeps its completion time | `TaskItemTests.ChangeStatus_WhenAlreadyDone_KeepsCompletedAt` |
| Clearing a due date | `TaskItemTests.Update_WithNullDueDate_ClearsDueDate` |
| Deleting a task | `TaskServiceTests.DeleteAsync_WhenOwned_RemovesTask` |
| Reading a task that belongs to somebody else | `TaskServiceTests.GetByIdAsync_WhenOwnedByAnotherUser_ThrowsNotFound` |
| Updating a task that belongs to somebody else | `TaskServiceTests.UpdateAsync_WhenOwnedByAnotherUser_ThrowsNotFound` |
| Deleting a task that belongs to somebody else | `TaskServiceTests.DeleteAsync_WhenOwnedByAnotherUser_ThrowsNotFound` |
| Requesting a task that does not exist | `TaskEndpointTests.GetTask_WhenMissing_ReturnsNotFound` |
