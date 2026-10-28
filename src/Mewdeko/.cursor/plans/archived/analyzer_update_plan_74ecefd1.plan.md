---
name: Analyzer Update Plan
overview: Investigate and safely trial CodeAnalysis/analyzer dependency updates without requiring a newer installed .NET SDK by keeping compiler-hosted source generator packages compatible with the SDK compiler.
todos:
  - id: selective-bump
    content: Trial the Dependabot analyzer bump with only main bot CodeAnalysis packages updated and sourcegen Roslyn packages pinned.
    status: completed
  - id: validate-build
    content: Run isolated temp-output builds for the selective bump and capture exact failure modes if any.
    status: completed
  - id: dependabot-config
    content: Adjust Dependabot grouping/ignore strategy so sourcegen Roslyn updates are not bundled with safe app package updates.
    status: completed
  - id: fallback-decision
    content: Only if selective pinning fails, evaluate committing generated localization output as a static fallback.
    status: completed
isProject: false
---

# CodeAnalysis Analyzer Update Plan

## Findings

- `src/Mewdeko/Mewdeko.csproj` consumes `src/MewdekoSourceGen/MewdekoSourceGen.csproj` as an analyzer via:
  - `[src/Mewdeko/Mewdeko.csproj](src/Mewdeko/Mewdeko.csproj)`
  - `ProjectReference Include="..\MewdekoSourceGen\MewdekoSourceGen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false"`
- The source generator is compiler-hosted, so its Roslyn package references must not be newer than the compiler bundled with the active SDK:
  - `[src/MewdekoSourceGen/MewdekoSourceGen.csproj](src/MewdekoSourceGen/MewdekoSourceGen.csproj)`
  - current comment already records this invariant.
- The earlier workaround was not a checked-in static generated source file. It was:
  - emit generated files to `obj/Generated` with `EmitCompilerGeneratedFiles` in `[src/Mewdeko/Mewdeko.csproj](src/Mewdeko/Mewdeko.csproj)`
  - include those generated files only for `DesignTimeBuild` so DotRush/OmniSharp can see `GeneratedBotStrings`
  - shadow-copy analyzers during design-time builds in `[src/Mewdeko/Directory.Build.targets](src/Mewdeko/Directory.Build.targets)` to avoid IDE locks
  - exclude Sentry generated files from design-time compile to avoid duplicate definitions
- The actual Roslyn band-aid was `756b7e885`, which pinned `MewdekoSourceGen` back to compiler-compatible Roslyn versions after Dependabot tried to bump it. Later `3e0f68bac` reverted a Dependabot update from Roslyn `5.0.0` to `5.3.0` in the sourcegen project.
- Current Dependabot analyzer branches update both app Roslyn packages and sourcegen Roslyn packages to `5.9.0`, which is likely the unsafe coupling.

## Recommended Approach

Keep `MewdekoSourceGen` on SDK-compatible Roslyn references, but allow the main bot project’s runtime/editor Roslyn packages to update. This preserves source-generator load compatibility while still moving the app-facing CodeAnalysis packages used by scripting/workspaces.

## Implementation Outline

1. Create an isolated trial branch/worktree and apply the current Dependabot analyzer bump selectively.
2. Update only `[src/Mewdeko/Mewdeko.csproj](src/Mewdeko/Mewdeko.csproj)` CodeAnalysis package references first.
3. Leave `[src/MewdekoSourceGen/MewdekoSourceGen.csproj](src/MewdekoSourceGen/MewdekoSourceGen.csproj)` pinned initially.
4. Build with the existing temp output pattern:
   - `dotnet build src/Mewdeko/Mewdeko.csproj -p:OutDir=bin/tmp-agent-build/ -p:UseAppHost=false`
5. If the selective app-only bump succeeds, update `.github/dependabot.yml` so Dependabot stops bundling sourcegen Roslyn updates with app Roslyn updates.
6. If the app-only bump fails, inspect whether the failure comes from runtime scripting APIs, workspace APIs, transitive assembly loading, or source generator load context.
7. Only consider a static generated fallback if selective pinning cannot work. That fallback would be higher maintenance because every localization key change would require regenerating and committing `GeneratedBotStrings.g.cs`.

## Validation

- Run the temp-dir build after each dependency combination.
- Run a quick owner eval/script path smoke check if the app Roslyn package bump compiles, because `OwnerOnly` uses `Microsoft.CodeAnalysis.CSharp.Scripting`.
- Confirm design-time generated output still avoids duplicate Sentry generator definitions.
- Confirm Dependabot config no longer proposes unsafe sourcegen Roslyn bumps, or at least isolates them into a PR that can be closed intentionally.