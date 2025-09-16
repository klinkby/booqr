# ICommand Testing Guidelines (Updated)

These guidelines consolidate lessons learned from recent ICommand unit tests to improve efficiency and precision.

- Target: all ICommand implementations in Application layer
- Tooling: xUnit, Moq, AutoFixture, Microsoft.Extensions.Time.Testing (FakeTimeProvider), NullLogger<T>

## 1) Test Naming and Shape

- Use AAA and Given/When/Then naming:
  - Method name: GIVEN_<Scenario>_WHEN_<Action>_THEN_<Expected>
- Prefer Fact for single scenario; Theory for parameterized variations.
- Where helpful, use the provided ApplicationAutoDataAttribute to seed common values (ClaimsPrincipal, dates, ids).

## 2) Common Arrange Patterns

- Instantiate the command with mocks and NullLogger<T>.Instance — do not mock ILogger.
- Build requests as records and always set User for authenticated commands:
  - request = request with { User = testUser };
- Use a small helper for test users to control roles precisely:
  - CreateUser(id, roles...) e.g. Employee/Admin or none for Customer.

## 3) Authorization and Privacy Invariants

For commands operating on “my” resources (GetMyBookings*, GetMyBookingById, DeleteBooking):

- Customers can only access their own resources.
  - If user.Id != targetUserId -> UnauthorizedAccessException.
  - Verify repository is NOT called (Times.Never) when unauthorized.
- Employees/Admins may access any user’s resources.
  - Verify repository is called with the target user id.
- When UnauthorizedAccessException is part of the flow inside a transaction, verify Rollback is called and Commit is not.

## 4) Transactions (ITransaction)

When a command explicitly uses ITransaction:

- Verify Begin is called (optionally with specific IsolationLevel if used).
- On success: Commit is called once, Rollback is not.
- On exception: Rollback is called once, Commit is not.
- When an early return happens (e.g., “not found”) follow the implementation semantics:
  - Assert the actual behavior (e.g., may skip Commit when nothing changed).

## 5) Time Defaults and Ranges

For commands that accept FromTime/ToTime and use AutoData (e.g., GetVacancyCollection, GetMyBookings):

- Do not instantiate FakeTimeProvider in tests, just take a `DateTime t0` parameter, assumed to be UtcNow, and calculate from there.
- Assert exact values forwarded to repositories:
  - FromTime default: now - 1 day
  - ToTime default: DateTime.MaxValue
- Verify flags/filters are forwarded precisely (e.g., available = true, booked = false for vacancies).

## 6) Async Enumerables

- Use a local async iterator helper to produce IAsyncEnumerable<T> in tests.
- Materialize results with ToListAsync() when asserting contents.

## 7) Precision in Moq Verifications

- Validate both return values and interactions.
- Use Times.Once/Times.Never consistently.
- Match arguments using It.Is<T>(predicate) for strong assertions on values passed to repositories.
- For CancellationToken parameters, prefer It.IsAny<CancellationToken>().

## 8) Edge Cases to Always Cover

- Null request -> ArgumentNullException.
- Not found -> return null/false (per command) and verify no side effects.
- Conflicts/business guards -> throws specific exception (e.g., InvalidOperationException), and verify no unintended calls.

## 9) Mapping and Side-Effects

- Where commands map inputs to domain entities (e.g., SignUpCommand):
  - Assert trimming of string inputs.
  - Assert correct defaults (e.g., Role = Customer).
  - Verify security-sensitive transforms (e.g., BCrypt.EnhancedHashPassword) with EnhancedVerify.
- For composite flows (e.g., DeleteBooking reopens vacancy via AddVacancyCommand):
  - Verify critical downstream repository interactions (e.g., calendar.Delete, calendar.Add).

## 10) Consistent Naming in Tests

- Mocks: _repo, _calendar, _bookings, _transaction, etc.
- System under test: sut.
- Request variable: request.
- Helper creators: CreateUser, Yield.

## 11) Minimalism and Signal

- Keep comments minimal; favor clear names and assertions.
- Focus on the command’s contract and its interaction with dependencies — not implementation details that might churn.

Adhering to these practices should keep our ICommand tests fast, deterministic, and highly precise about observable behavior and security guarantees.
