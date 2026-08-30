# .NET Coding Standards and Conventions

Code snippets below are illustrative examples only, replace namespaces and types to match your
project.

## C# language features

1. **File-scoped namespaces**:

   ```csharp
   namespace Example.Project.Library;
   ```

2. **Nullable reference types**: enabled (`<Nullable>enable</Nullable>`), use nullable annotations
   appropriately, use `required` for mandatory properties.
3. **Modern C# features**: prefer modern language constructs, primary constructors when
   appropriate, top-level statements for console apps, pattern matching over traditional checks,
   collection expressions when types loosely match, extension methods (the classic
   `this`-parameter form or an `extension(<receiver>) { ... }` block on C# 14+), implicit object
   creation when the type is apparent, range and index operators.
4. **Expression-bodied members**: use for applicable methods, properties, accessors, operators,
   lambdas, local functions.
5. **`var` keyword**: do NOT use `var`, always use explicit types:

   ```csharp
   // Correct
   int count = 42;
   string name = "test";

   // Incorrect
   var count = 42;
   var name = "test";
   ```

## Naming conventions

1. **Private fields**: underscore prefix with camelCase:

   ```csharp
   private readonly HttpClient _httpClient;
   private int _counter;
   ```

2. **Static fields**: `s_` prefix with camelCase:

   ```csharp
   private static int s_instanceCount;
   ```

3. **Constants**: PascalCase:

   ```csharp
   private const int MaxRetries = 3;
   ```

## Code structure

1. **Global usings**: use `GlobalUsings.cs` for common namespaces:

   ```csharp
   global using System;
   global using System.Net.Http;
   global using System.Threading.Tasks;
   global using Microsoft.Extensions.Logging;
   ```

2. **Usings placement**: outside the namespace, sorted with `System` directives first:

   ```csharp
   using System.CommandLine;
   using System.Runtime.CompilerServices;
   using Example.Project.Library;

   namespace Example.Project.Console;
   ```

3. **Braces**: Allman style:

   ```csharp
   public void Method()
   {
       if (condition)
       {
           // code
       }
   }
   ```

4. **Indentation**: C# files 4 spaces, XML/csproj files 2 spaces, YAML files 2 spaces, JSON files
   4 spaces.
5. **Line endings**: not specified here, governed per repo by `.editorconfig` / `.gitattributes`
   per GOVERNANCE.md's "Line Endings" section.
6. **`#region`**: do not use regions, prefer logical file/folder/namespace organization.
7. **Member ordering (StyleCop SA1201)**: const -> static readonly -> static fields -> instance
   readonly fields -> instance fields -> constructors -> public (events -> properties -> indexers
   -> methods -> operators) -> non-public in same order -> nested types.

## Comments and documentation

XML documentation is on: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, and
missing XML comments for public APIs are suppressed in `.editorconfig`. Every public surface must
still be documented: a single-line summary, additional details in remarks, documented input
parameters, return values, exceptions, and crefs.

```csharp
/// <summary>
/// Example of a single line summary.
/// </summary>
/// <remarks>
/// Additional important details about usage.
/// Multiple lines if needed.
/// </remarks>
/// <param name="category">
/// The quote category to request
/// </param>
/// <param name="cancellationToken">
/// A <see cref="System.Threading.CancellationToken"/> that can be used to cancel the request.
/// </param>
/// <returns>
/// A <see cref="string"/> containing the quote text.
/// </returns>
/// <exception cref="System.ArgumentException">
/// Thrown when <paramref name="category"/> is not a supported value.
/// </exception>
public async Task<string> GetQuoteOfTheDayAsync(string category, CancellationToken cancellationToken)
{
    if (category is not ("motivational" or "humor"))
    {
        throw new ArgumentException($"Unsupported category: {category}", nameof(category));
    }

    cancellationToken.ThrowIfCancellationRequested();
    await Task.Delay(1, cancellationToken);
    return $"Quote for {category}";
}
```
