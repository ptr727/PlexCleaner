# .NET Project Configuration

1. **Target framework**: .NET 10.0 (`<TargetFramework>net10.0</TargetFramework>`).
2. **AOT compatibility**: `<IsAotCompatible>true</IsAotCompatible>` unconditionally, and
   `<VerifyReferenceAotCompatibility>true</VerifyReferenceAotCompatibility>` only in a
   `<PropertyGroup Condition="'$(PublishAot)' == 'true'">`. That group is placed in the `.csproj` after
   the `.csproj` sets `PublishAot` or in `Directory.Build.targets`, never in `Directory.Build.props`.
   `Directory.Build.props` is imported before the project body and so never sees a `PublishAot` the
   `.csproj` sets. Reference
   verification reports `IL3058` for every referenced assembly that lacks `IsAotCompatible` metadata
   set to `true`. `TreatWarningsAsErrors` turns that into a failed build on any such
   dependency, so reference verification runs only where an AOT publish needs it.
3. **Assembly information**: use semantic versioning, include SourceLink
   (`<PublishRepositoryUrl>true</PublishRepositoryUrl>`), embed untracked sources
   (`<EmbedUntrackedSources>true</EmbedUntrackedSources>`).
4. **Internal visibility**: use `InternalsVisibleTo` for test and benchmark access (adapt the
   project names to your repo's test/benchmark projects):

   ```xml
   <ItemGroup>
     <InternalsVisibleTo Include="YourBenchmarkProject" />
     <InternalsVisibleTo Include="YourTestProject" />
   </ItemGroup>
   ```

5. **Nullable and XML documentation**: `<Nullable>enable</Nullable>`,
   `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (see `references/conventions.md`
   for the XML documentation format every public surface needs).
