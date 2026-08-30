# .NET Project Configuration

1. **Target framework**: .NET 10.0 (`<TargetFramework>net10.0</TargetFramework>`).
2. **AOT compatibility**: `<IsAotCompatible>true</IsAotCompatible>`,
   `<VerifyReferenceAotCompatibility>true</VerifyReferenceAotCompatibility>`.
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
