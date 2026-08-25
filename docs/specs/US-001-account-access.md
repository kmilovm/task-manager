# US-001 — Account access

## Story

**As a** new user of the task manager
**I want to** register an account and sign in with it
**so that** my tasks are private to me and available every time I come back.

## Context

The task list is personal. Without an identity there is no way to decide which tasks belong to
whom, so authentication is a prerequisite for US-002. The API is stateless and consumed by a
single-page application, so a bearer token is issued at login instead of a server-side session.

## Business rules

| ID | Rule |
|----|------|
| BR-101 | An email address identifies exactly one account. Comparison is case-insensitive. |
| BR-102 | A password must be at least 8 characters long and contain a letter and a digit. |
| BR-103 | Passwords are stored as BCrypt hashes. The hash never leaves the infrastructure layer. |
| BR-104 | A successful login returns a JWT that expires 60 minutes after it is issued. |
| BR-105 | A failed login gives the same message whether the email exists or not, so the endpoint cannot be used to enumerate accounts. |
| BR-106 | Every endpoint under `/api/tasks` requires a valid, unexpired bearer token. |
| BR-107 | An account carries a display name. It is required, is trimmed, and is between 1 and 100 characters. |

## Acceptance criteria

```gherkin
Feature: Account access
  In order to keep my task list private
  As a user of the task manager
  I want to register and sign in

  Background:
    Given the account "ada@example.com" already exists with name "Ada Lovelace" and password "Passw0rd!"

  Scenario: Registering with a new email address
    When I register with email "grace@example.com", name "Grace Hopper" and password "Passw0rd!"
    Then my account is created
    And my display name is "Grace Hopper"
    And I receive an access token
    And the response does not contain my password or its hash

  Scenario: The display name is trimmed
    When I register with email "grace@example.com", name "  Grace Hopper  " and password "Passw0rd!"
    Then my display name is "Grace Hopper"

  Scenario: Registering without a display name
    When I register with email "grace@example.com", name "" and password "Passw0rd!"
    Then the request is rejected with status 400
    And the validation error mentions the field "displayName"

  Scenario: Registering with an email that is already taken
    When I register with email "ada@example.com", name "Ada Byron" and password "Passw0rd!"
    Then the request is rejected with status 409
    And the message is "An account with this email already exists."

  Scenario Outline: Registering with a password that is too weak
    When I register with email "grace@example.com", name "Grace Hopper" and password "<password>"
    Then the request is rejected with status 400
    And the validation error mentions the field "password"

    Examples:
      | password  | reason              |
      | short1    | fewer than 8 chars  |
      | password  | no digit            |
      | 12345678  | no letter           |

  Scenario: Signing in with valid credentials
    When I sign in as "ada@example.com" with password "Passw0rd!"
    Then I receive an access token
    And the token identifies me as "ada@example.com"
    And the token expires in 60 minutes

  Scenario: Reading my own profile
    Given I am signed in as "ada@example.com"
    When I request my profile
    Then I see the email "ada@example.com" and the name "Ada Lovelace"
    And the response does not contain my password hash

  Scenario: Signing in with a wrong password
    When I sign in as "ada@example.com" with password "wrong-password"
    Then the request is rejected with status 401
    And the message is "Invalid email or password."

  Scenario: Signing in with an email that does not exist
    When I sign in as "nobody@example.com" with password "Passw0rd!"
    Then the request is rejected with status 401
    And the message is "Invalid email or password."

  Scenario: Email address is matched regardless of casing
    When I sign in as "ADA@Example.com" with password "Passw0rd!"
    Then I receive an access token

  Scenario: Reaching a protected endpoint without a token
    When I request my task list without an access token
    Then the request is rejected with status 401

  Scenario: Reaching a protected endpoint with an expired token
    Given my access token expired an hour ago
    When I request my task list
    Then the request is rejected with status 401

  Scenario: Reaching the public health endpoint without a token
    When I request "/api/health" without an access token
    Then the request succeeds with status 200
```

## Out of scope

Refresh tokens, password reset, email confirmation, social login, roles and permissions,
account deletion, rate limiting on the login endpoint.

## Traceability

| Scenario | Test |
|----------|------|
| Registering with a new email address | `AuthServiceTests.RegisterAsync_WithNewEmail_CreatesUserAndReturnsToken` |
| The display name is trimmed | `UserTests.Register_TrimsDisplayName` |
| Registering without a display name | `RegisterRequestValidatorTests.Validate_WithEmptyDisplayName_Fails` |
| Registering with an email that is already taken | `AuthServiceTests.RegisterAsync_WithExistingEmail_ThrowsEmailAlreadyInUse` |
| Registering with a password that is too weak | `RegisterRequestValidatorTests.Validate_WithWeakPassword_Fails` |
| Signing in with valid credentials | `AuthServiceTests.LoginAsync_WithValidCredentials_ReturnsToken` |
| Reading my own profile | `AuthEndpointTests.GetMe_WhenSignedIn_ReturnsProfile` |
| Signing in with a wrong password | `AuthServiceTests.LoginAsync_WithWrongPassword_ThrowsInvalidCredentials` |
| Signing in with an email that does not exist | `AuthServiceTests.LoginAsync_WithUnknownEmail_ThrowsInvalidCredentials` |
| Email address is matched regardless of casing | `AuthServiceTests.LoginAsync_IsCaseInsensitiveOnEmail` |
| Reaching a protected endpoint without a token | `TaskEndpointTests.GetTasks_WithoutToken_ReturnsUnauthorized` |
| Reaching a protected endpoint with an expired token | `TaskEndpointTests.GetTasks_WithExpiredToken_ReturnsUnauthorized` |
| Reaching the public health endpoint without a token | `HealthEndpointTests.GetHealth_WithoutToken_ReturnsOk` |
