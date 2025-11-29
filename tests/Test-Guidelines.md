# How we test

1. Name tests as GIVEN_{conditions}_WHEN_{action}_THEN_{outcome} with GIVEN/WHEN/THEN in uppercase and PascalCased insertions.
2. Test framework: xUnit v3
3. Mocking: Moq (mock collaborators narrowly and verify behavior precisely)
4. Complex reusable mocks are created in factory methods
5. Auto data generation: AutoFixture (via `AutoFixture.Xunit3` and the `[AutoData]` attribute) instead of hardcoded values
6. Logging: `NullLogger<T>` when we don’t need to verify log output
7. Partition into arrange, act, assert sections
8. Use timeouts when validating hang-prone methods
