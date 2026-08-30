# .NET Testing Conventions

1. **Framework**: xUnit v3 or later (the `xunit.v3` package, never the legacy v2 `xunit` package)
   with AwesomeAssertions for every assertion. Native xUnit asserts (`Assert.Equal`,
   `Assert.True`, ...) are not allowed, use the fluent `.Should()` API. Dynamic test skipping
   (`Assert.Skip`, `Assert.SkipWhen`) is control flow, not an assertion, and stays native:

   ```csharp
   [Fact]
   public void MethodName_Scenario_ExpectedBehavior()
   {
       // Arrange
       int expected = 42;

       // Act
       int actual = GetValue();

       // Assert
       actual.Should().Be(expected);
   }
   ```

2. **Organization**: Arrange-Act-Assert pattern.
3. **Naming**: descriptive names with underscores.
4. **Theory tests**: use `[Theory]` with `[InlineData]`.
