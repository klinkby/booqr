# IRepository Testing Guidelines (Infrastructure Tests)

These guidelines codify how we author and maintain integration tests for repository implementations in Klinkby.Booqr.Infrastructure. They adapt the ICommand testing guidelines where applicable and focus on database interactions, transactions, and persistence invariants.

- Scope: All repository tests under tests/Klinkby.Booqr.Infrastructure.Tests
- Tooling: xUnit, AutoFixture (AutoData), Testcontainers for PostgreSQL, Microsoft.Extensions.Time.Testing (FakeTimeProvider), DI via ServiceCollection, NullLogger<T>

## 1) Test naming and structure

- Use AAA and Given/When/Then naming:
  - Method name: GIVEN_<Scenario>_WHEN_<Action>_THEN_<Expected>
- Prefer Fact for single scenario; Theory + [IntegrationAutoData] for parameterized/auto-generated domain records.
- Keep arrange and assert blocks focused; avoid comments when names make intent obvious.

## 2) Test fixture and dependency setup

- Use the shared ServiceProviderFixture to get real implementations wired via DI:
  - Resolve repositories and ITransaction via serviceProvider.Services.GetRequiredService<...>().
  - The fixture provides a Testcontainers PostgreSQL instance, FakeTimeProvider as TimeProvider, and NullLogger<T>.
- Do NOT mock repositories in Infrastructure tests. These are integration tests against a real database.

## 3) Transactions and isolation

- Wrap each test in an explicit transaction and roll it back in finally to isolate data:
  - await _transaction.Begin(...);
  - try { /* test body */ } finally { await _transaction.Rollback(...); }
- Only call Commit if the test explicitly asserts persisted state across commits; the default is Rollback to keep the database clean.
- If Begin/Rollback take CancellationToken in your test, use CancellationToken.None for clarity and consistency.

## 4) Deterministic data with AutoFixture

- Use [IntegrationAutoData] for Theory tests. It provides deterministic defaults:
  - DateTime t0: fixed Utc date/time for reproducibility
  - TimeSpan: 1 hour by default
  - Domain records (Location, User, Service, Booking, CalendarEvent) customized to reasonable defaults (e.g., non-deleted, valid Zip, Customer role)
- Specialize only the fields relevant to the scenario using record with-syntax:
  - employee = employee with { Role = UserRole.Employee };
  - evt = evt with { EndTime = startDate.Add(duration) };

## 5) Creating related data via repositories

- Create dependent rows using the appropriate repositories instead of inlining SQL:
  - var locationId = await _locations.Add(location);
  - var employeeId = await _users.Add(employee with { Role = UserRole.Employee });
  - Use returned ids to compose entities for the SUT repository (e.g., CalendarEvent.EmployeeId, Booking.ServiceId/CustomerId).

## 6) Assertions on persisted state

- For Add/GetById roundtrips, assert that server-generated fields are preserved and others match expectations:
  - Assert.InRange(newId, 1, int.MaxValue);
  - Assert.Equal(expected with {
      Id = actual!.Id,
      Created = actual.Created,
      Modified = actual.Modified,
      Version = actual.Version
    }, actual);
- For lookups by alternate keys (e.g., GetByEmail), assert the relationship between results (ids should match).

## 7) Constraints and database errors

- When asserting constraint violations, capture PostgresException and assert specific ConstraintName values:
  - var ex = await Assert.ThrowsAsync<PostgresException>(...);
  - Assert.Equal("no_overlapping_events", ex.ConstraintName);
  - Other examples: "valid_time_range".
- Prefer precise, named constraints over generic exception type checks to keep tests robust yet meaningful.

## 8) Soft-delete flows (Delete/Undelete)

- When a repository supports soft-delete:
  - Exercise Delete and Undelete within the transaction and assert subsequent operations behave as expected.
  - Example: delete a user, then undelete, then verify it can be read back or used by related operations.

## 9) Time handling

- Compute time ranges from the deterministic t0 provided by [IntegrationAutoData].
- For calendar use-cases, ensure EndTime is strictly greater than StartTime; use duration to build adjacent or overlapping intervals intentionally.
- Avoid DateTime.UtcNow inside tests; rely on the fixture’s FakeTimeProvider and t0.

## 10) Patterns for calendar overlap tests

- To test adjacency vs overlap:
  - Overlap: create one event [start, end), then add/modify another with the same range for the same employee → expect constraint failure ("no_overlapping_events").
  - Adjacent (allowed): end exactly at the next start or vice versa for the same employee → should succeed.
  - Different employees with overlapping times at the same location → should succeed unless other constraints apply.

## 11) CancellationToken usage

- When repository methods accept CancellationToken, pass CancellationToken.None in tests unless the scenario validates cancellation.
- Keep token usage consistent within a test.

## 12) What does NOT apply from ICommand guidelines

- No Moq verifications (Times.Once/Never) — Infrastructure tests do not mock repositories.
- No authorization/ClaimsPrincipal paths — repositories operate at the persistence layer without user identity concerns.
- No command-specific side-effects or transaction commit/rollback verification — we focus on repository behavior and DB invariants, not service-level transaction orchestration.

## 13) When to add new tests

- New constraints added in migrations (unique indexes, check constraints) → add tests asserting ConstraintName on violation.
- New repository methods (e.g., GetByX, Update, complex queries) → add roundtrip tests and minimal edge cases (not found, invalid inputs, deleted state interactions).
- Bug fixes related to mapping or serialization → add a test that reproduces and guards against regression.

## 14) Practical checklist before finishing a test

- Naming: GIVEN_/WHEN_/THEN_ reflects scenario clearly.
- Arrange:
  - Uses [IntegrationAutoData], resolves repositories/ITransaction from ServiceProviderFixture.
  - Creates dependent entities via repositories, sets roles where relevant.
- Transaction:
  - Explicit Begin in Arrange; Rollback in finally.
- Time:
  - Times derived from t0; durations explicit.
- Act/Assert:
  - For success: assert persisted-readback equality with server-generated fields taken from actual.
  - For failure: assert PostgresException.ConstraintName precisely.
  - For soft-delete: exercise Delete/Undelete effects.

Following these practices keeps Infrastructure repository tests deterministic, isolated, and precise about database-level behavior while remaining concise and maintainable.
