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

## Microsoft.Testing.Platform and coverage

A test project on `xunit.v3` 4.0.0 or later is MTP-based. The .NET 10 SDK and later refuse to run such a test project through the VSTest target, so it also carries:

- a root **`global.json`** declaring `{"test": {"runner": "Microsoft.Testing.Platform"}}`, which is what selects the driver `dotnet test` runs the project through,
- **`Microsoft.Testing.Extensions.CodeCoverage`** at **18.9.0 or later**, in place of `coverlet.collector`, whose VSTest data collector MTP ignores without failing,
- no **`xunit.runner.visualstudio`**, the VSTest adapter MTP replaces.

A project not yet MTP-based keeps the VSTest collector. That lagging state is a migration owed rather than drift, until its own `xunit.v3` bump forces the move.

**The version floor is load-bearing rather than cautionary.** Below 18.1.0 the extension is built against Microsoft.Testing.Platform 1.x. An 18.0.x resolution is what a `>= 18.0.0` range picks. That resolution throws a `TypeLoadException` against the 2.x platform `xunit.v3` 4.0.0 carries and runs zero tests. It also **still writes a well-formed Cobertura file reporting full coverage**. As a result, only the non-zero exit says the run reported nothing. 18.9.0 is the first release on Microsoft.Testing.Platform 2.3.x. On Microsoft.Testing.Platform 2.3.x, every test project writes into the one shared `--results-directory` the invocation names rather than resolving that relative path per project.

The CI invocation `WORKFLOW.md` D1.6 requires is `dotnet test --coverage --coverage-output-format cobertura --results-directory ./coverage`. The details that follow are as load-bearing as the invocation. Getting one wrong does not red the job on its own. `--coverage-output` stays unset because of what pinning one filename does. That gives every test project in the solution the same path. A solution with more than one test project then keeps only whichever ran last. Leaving it unset produces the default name `<guid>.cobertura.xml`. `codecov-cli`'s own file finder does not match that name, so the report is renamed before the upload reads the directory, per `WORKFLOW.md` D1.6.

**Diagnosing a local run.** `dotnet test` under the CI configuration reports zero tests on some machines where CI reports the full suite on the same SDK. That reads as a broken repository and is a broken driver. The target string the run prints separates the two. `net10.0` with no architecture means the driver resolved none. `net10.0|<arch>` with no tests means the tests did not register. The tests not registering is the case that points back at the requirements above.
