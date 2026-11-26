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
3. **Modern C# features**: prefer modern language constructs. Each construct below is required
   wherever its IDE rule matches, since the gate reports each rule at warning:
   - a primary constructor (IDE0290),
   - top-level statements in place of a `Program.Main` (IDE0210),
   - pattern matching over traditional checks (IDE0019, IDE0020, IDE0038, IDE0078, IDE0083,
     IDE0260),
   - a collection expression when types loosely match (IDE0028, IDE0300 to IDE0306),
   - implicit object creation when the type is apparent (IDE0090),
   - range and index operators (IDE0056, IDE0057).

   Prefer extension methods (the classic `this`-parameter form or an `extension(<receiver>) { ... }`
   block on C# 14+).
4. **Expression-bodied members**: each is required wherever its IDE rule matches (IDE0021 to
   IDE0027, IDE0053, IDE0061). That covers constructors, methods, operators, properties,
   indexers, accessors, lambdas, and local functions.
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
7. **Member ordering (StyleCop SA1201, SA1202)**: order by kind first, across every access level
   (SA1201). The kinds run fields, constructors, finalizers, delegates, events, enums, interfaces,
   extension blocks, properties, indexers, conversion operators, operators, methods, nested
   structs, and nested classes. Within one kind, order by access: public -> internal -> protected
   internal -> protected -> private protected -> private (SA1202). Within one kind and access,
   const precedes static, which precedes instance, and readonly precedes mutable (SA1203, SA1204,
   SA1214).

## Comments and documentation

XML documentation is on: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. Every
public surface must be documented: a single-line summary, additional details in remarks, documented input
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
