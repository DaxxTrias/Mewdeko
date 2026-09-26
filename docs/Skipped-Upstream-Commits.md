# Skipped Upstream Commits

Last reviewed: 2026-09-26.

This note tracks upstream commits that were considered during the drive-by cherry-pick pass but were not pulled into this branch. The branch did take the small suggestion modal, wizard, and XP default-setting fixes separately; the commits below were skipped because they are large, mixed-purpose, dependent on skipped work, or likely to need a focused port.

## General Revisit Checklist

- Re-run a clean dry-run before touching the branch:
  - `git diff <commit>^ <commit> --binary | git apply --check --whitespace=nowarn`
  - `git diff <commit>^ <commit> --binary | git apply --check --3way --whitespace=nowarn`
- Prefer splitting mixed commits into smaller local commits by concern. Several upstream commits combine migrations, services, API behavior, and UI/dashboard behavior.
- Re-check `Database/Scripts` numbering before bringing in any migration. Follow the repository migration-lane convention and do not reuse existing prefixes.
- Treat database maintenance scripts separately from normal application migrations. Confirm whether they should be embedded migration resources, operator-run scripts, or documentation-only tools.
- For dashboard/API changes, verify the current branch's `DashboardAccessEnforcementFilter`, `SkipDashboardAccessAttribute`, and JWT auth flow before porting.
- For service registrations, check `Program.cs` and lifetime expectations. Background services should not be added without confirming config toggles, cancellation behavior, and database load.
- Build to a temporary output directory after each focused port:
  - `dotnet build src/Mewdeko/Mewdeko.csproj -p:OutDir=bin/tmp-agent-build/ -p:UseAppHost=false`

## 2026-09-26 Drive-By Upstream Pass

This session reviewed the following upstream commits:

- `0c41a867465c84d092d0febd267b4cf6aceec0ef`
- `5927669729dbca05990047b890c075f6cfa31b9b`
- `8541311b0b6c99d60eab609df4ba863f951ff254`
- `9aba5d73773c9d256140e530a948bdb0771de278`
- `fbed8b5bf8dcc52baa9c7cfefc490e1107182775`
- `608ad480e070546ee23fb952bd7d59a92fea432e`
- `6815ec13de37ae92fd57fc887b28b159c2f1af60`
- `b9f330cd5bef82efaa525a4b08437cfed531eefb`
- `16cbd4f97ceaf080ce5482bb5f2ed88c8cf0df7f`
- `b03cab09442ce8e30e9b74a7082409d3b753b9f3`
- `e47094e368877f6bdf7c0e1f2ef70fc5e741a195`
- `e4cd71a2554ff3ff58db3442131bb73f4cfeda90`
- `42a59f214d2397f0ae5c9a26098e6be4ee2e26c0`
- `3387d37dbcdef0e90e0be92dfb6b8ba9ce68ceb3`

Already taken from this session:

- `0c41a8674`: chat trigger null/candidate handling and stream-notification index ordering.
- `9aba5d737`: forms question-option update/delete API routes, with the local service update narrowed to editable option fields.
- `608ad480e`: workflow release permission and date-stamped nightly tag intent, adapted to the local workflow.
- `3387d37db`: message count inserts now use `InsertWithInt64IdentityAsync`.
- Partial `6815ec13d`: `PlayRequest` accepts either `Url` or `Query`, `MusicController.Play` uses the fallback term with validation, and TTS join/leave defaults are exposed and reused.

Deferred commits from this session are documented below. Treat them as candidates for focused ports, not clean cherry-picks.

## 592766972

Upstream summary: broad currency economy and help-system refactor. Adds currency economy configuration, cooldowns, shops, inventory, analytics, leaderboard helpers, dashboard currency API models/controllers, a help search modal, and large localization updates.

Why it was skipped:

- Large mixed commit spanning currency schema, services, controllers, commands, help UI, strings, aliases, and leaderboard rendering.
- Direct patch check failed in high-traffic files including `Currency.cs`, currency service implementations, `HelpService`, and many localization paths.
- Several string files referenced by upstream do not exist on this branch, so a literal patch would create or modify the wrong localization shape.
- It includes migration `185-AddCurrencyEconomy.sql`; migration numbering must be reconciled before porting.

Things to account for before revisiting:

- Decide whether the local currency implementation should be replaced, extended, or left as-is. Do not layer the upstream economy system on top of a different local model without reconciling balance, transaction, cooldown, and scope semantics.
- Split the port into independent slices:
  - database models and migration
  - core currency service abstractions
  - shop/economy/betting commands
  - dashboard API contracts
  - leaderboard renderer and interactions
  - help-service categorization/localization
- Re-check all new tables and POCO mappings against the migration-lane convention.
- Keep the help-system refactor separate if the goal is only to import currency behavior.
- Verify that aliases and generated/localized strings match this branch's current strings layout.

## 8541311b0

Upstream summary: adds Channel Access with Discord commands, slash commands, dashboard endpoints, channel gate applications, blacklists, approvals/denials, embed webhook personas, CDN storage, and related schema.

Why it was skipped:

- Full feature addition with multiple new tables, controllers, command modules, services, credentials, and strings.
- Direct patch check failed in `EmbedsController`, `HelpService`, `Music.cs`, `BotCredentials`, `credentials_example.json`, common strings, and dashboard model files.
- Some hunks assume files introduced by other skipped commits, such as currency request models.
- Adds migrations `186`, `187`, and `188`; these need migration-lane review.

Things to account for before revisiting:

- Split Channel Access from the embed persona/CDN work unless both are intentionally being adopted together.
- Review Discord permission behavior carefully. Channel gates modify role/channel access and need clear failure handling for missing permissions or deleted roles/channels.
- Confirm whether the dashboard should be allowed to post panels and manage applications, and how audit logging should record those changes.
- Reconcile credential changes before adding CDN storage settings. Do not introduce new credential fields without updating example config, environment loading, and deployment docs together.
- If `HelpService` category changes are wanted, apply them after the module exists locally.

## fbed8b5bf

Upstream summary: adds Ban Prune settings with SQL schema, commands, API endpoints, service-layer resolution, and integration into ban/softban/tempban/role-ban flows.

Why it was skipped:

- Cross-cutting moderation feature touching administration, punishment services, permissions filters, role monitoring, channel commands, aliases, strings, API controllers, and workflow config.
- Direct patch check failed in `.github/workflows/main.yml`, `AdministrationController`, `MewdekoDb`, `Administration.cs`, `FilterService`, and other integration points.
- Some context assumes previously skipped channel-access/schema changes exist.
- Adds migration `189-AddBanPruneSettings.sql`; numbering and dependencies need review.

Things to account for before revisiting:

- Port the schema and `BanPruneAction` definitions first, then wire command/API/service behavior in a second pass.
- Verify Discord's current ban-delete-message limits and map every upstream action key to the local moderation command set.
- Review all punishment paths that can ban users: text commands, slash commands, auto-ban-role, filters, role monitor, and any service-only entry points.
- Ensure category/channel override resolution is deterministic and cached invalidation is correct after dashboard or command updates.
- Keep workflow changes out of this feature port unless they are still independently needed.

## 6815ec13d

Upstream summary: music module and API improvements including `Url`/`Query` request fallback, guild-wide player settings API request model, player auto-creation/join support, volume normalization, queue deletion behavior, bass boost aliasing, and TTS format cleanup.

What was partially taken:

- `PlayRequest` now supports nullable `Url`, nullable `Query`, and a `Term` fallback.
- `MusicController.Play` validates `Term`, keeps the local active-player requirement, and uses the fallback when loading tracks.
- `TtsService.DefaultJoinFormat` and `DefaultLeaveFormat` are public constants, and `MusicTts` uses them for display.

Why the rest was skipped:

- Direct patch check failed in `MusicController`, which has diverged heavily.
- The upstream controller changes are broader than a compatibility fix and alter behavior by creating/joining players dynamically from dashboard requests.
- `MusicPlayerSettingsRequest` is unused without the larger `UpdateSettings` endpoint changes.

Things to account for before revisiting:

- Decide whether dashboard play should auto-create a player and join the requester's voice channel. That changes current API behavior from "requires active player" to "may create player".
- If adopting `GetOrCreatePlayerAsync`, verify user identity resolution, voice-channel membership, bot permissions, and failure messages for API-key callers.
- Revisit volume semantics end to end. Upstream normalizes percentage values across clients; confirm the current UI and Lavalink expectations first.
- Apply queue deletion and filter alias changes separately so regressions are easy to isolate.
- Only add `MusicPlayerSettingsRequest` when the controller endpoint actually consumes it.

## b9f330cd5

Upstream summary: major stat-channel enhancements with metadata/preview APIs, guild-wide defaults, new stat types, formatting/style options, stat-channel settings schema, Twitch snapshot caching, and deletion of `Lavalink.jar`.

Why it was skipped:

- Large feature commit across database schema, controllers, stat-channel service/commands, Twitch services, strings, and repository assets.
- Direct patch check failed in `.gitignore`, `StatChannelController`, `MewdekoDb`, `Lavalink.jar`, Twitch service paths, and response string paths.
- Several upstream Twitch files and response JSON files do not exist in this branch.
- Adds migration `190-AddStatChannelEnhancements.sql`; migration numbering needs review.

Things to account for before revisiting:

- Separate stat-channel schema/API work from Twitch snapshot caching.
- Confirm whether `Lavalink.jar` should be deleted in this repository. Treat binary asset removal independently from stat-channel behavior.
- Reconcile current Twitch module layout before porting snapshot cache code. The upstream paths do not match this branch.
- Review preview/metadata endpoints for dashboard access control and audit logging.
- Test existing stat-channel update behavior before and after adding display styles, intervals, categories, and permission overwrites.

## 16cbd4f97

Upstream summary: large chat-trigger expansion with trigger placeholder providers, trigger events, counters, economy/XP/reputation/activity placeholders, response modes, conditions, chaining/bot behavior, import/export expansion, and permission matching improvements.

Why it was skipped:

- Very large schema and behavior expansion touching chat triggers, permissions, giveaway/ticket/sticky services, currency, XP, reputation, and aliases.
- Direct patch check failed in `CurrencyCategory`, chat-trigger extension/service files, permissions module, and localization paths.
- Depends on currency types from skipped currency work.
- Adds migrations `191` through `197`; these need careful ordering and compatibility review.

Things to account for before revisiting:

- Preserve the already-taken `0c41a8674` chat-trigger matching fixes when porting any upstream `ChatTriggersService` changes.
- Split into layers:
  - schema/model expansion
  - placeholder infrastructure
  - counter service and counter placeholders
  - event publisher/listeners
  - command/slash-command surface
  - permission matching changes
  - import/export compatibility
- Treat provider payloads and regex placeholders as untrusted input. Keep parse failures cheap and non-noisy.
- Verify migration compatibility with existing persisted triggers before enabling response modes, conditions, chaining, or bot-authored trigger behavior.
- Do not port economy-gating pieces until the currency economy decision from `592766972` is settled.

## b03cab094

Upstream summary: XML documentation updates for the expanded chat-trigger and ticket-event constructor/method parameters.

Why it was skipped:

- Mostly documentation, but it documents parameters and behavior introduced by the skipped chat-trigger event/placeholder work.
- Direct patch check failed in `ChatTriggersService`; `TicketService` had partial context, but importing only that doc would be misleading without the related constructor change.

Things to account for before revisiting:

- Revisit after `16cbd4f97` or any equivalent local chat-trigger event system is present.
- If only doc cleanup is desired, regenerate the XML comments from the local method signatures rather than applying upstream text literally.

## e47094e36

Upstream summary: adds request models and dashboard endpoints to `ChatTriggersController` for category toggles, counters, placeholders, dry-run testing, and usage stats.

Why it was skipped:

- Depends on `TriggerCounterService`, placeholder infrastructure, and expanded chat-trigger statistics that are not present unless `16cbd4f97` is ported.
- Direct patch check failed in `ChatTriggersController`; new request DTOs alone are not useful without service/controller support.

Things to account for before revisiting:

- Port after the counter and placeholder services exist locally.
- Review API authorization and dashboard audit behavior for category-wide enable/disable and counter mutation endpoints.
- Define what "test trigger" should execute safely. It should not mutate counters, grant roles, chain triggers, or send messages unless explicitly designed to do so.
- Confirm whether placeholder discovery should be static metadata or derived from registered providers.

## e4cd71a25

Upstream summary: refactors `WizardDecisionService` completed-guild deserialization and adds reset-wizard support in `MeController` and `WizardController`.

Why it was skipped:

- Medium-sized and potentially useful, but not a clean apply. Conflicts hit `MeController` and `WizardController`.
- Reset behavior touches persisted user preferences, guild config flags, cache invalidation, and API surface.

Things to account for before revisiting:

- Inspect the current wizard state model first. Confirm where completion, skip flags, and per-user completed guilds are persisted today.
- Port the `DeserializeCompletedGuilds` helper separately from the reset API route.
- Ensure reset behavior clears the same cache keys that setup/wizard completion writes.
- Decide whether reset should be per-user, per-guild, or both, and whether it requires audit logging.
- Add dashboard/API checks for resetting a guild the user no longer has access to.

## 42a59f214

Upstream summary: broad localization, messaging, stability, Twitch, music, stat-channel, channel-access, audit-log, and test refactor.

Why it was skipped:

- Very broad mixed-purpose refactor.
- Direct patch check failed in `Mewdeko.cs`, `LogCommandService`, `ChatTriggers`, `HelpService`, `StatChannelService`, aliases, and multiple missing Twitch/channel-access/string paths.
- Assumes other skipped features exist, including channel access and stat-channel/Twitch reshaping.

Things to account for before revisiting:

- Split by concrete outcome. Good smaller candidates may include:
  - permission-safe audit-log helper in `LogCommandService`
  - safer music message sending in `MewdekoPlayer`
  - localization string-provider adjustments
  - targeted test updates
- Do not import channel-access or stat-channel hunks from this commit before those underlying features are present.
- Check generated string access patterns against the current strings source generator and runtime provider.
- Audit every "safe send" helper for Discord permission checks, exception swallowing, and logging level.

## 7bf1cf93d

Upstream summary: adds deduplication scripts, a guild leave-feedback system, message timestamp retention, database maintenance scripts, and related XP/message-count service changes.

Why it was skipped:

- Large mixed commit: about 30 files and 2,500+ lines.
- Combines unrelated concerns: data cleanup, DbUp behavior, maintenance SQL, leave feedback, retention jobs, XP cache/service changes, and message count changes.
- Direct patch check failed in conflict-prone areas including `AuditLogFilter`, `MewdekoDb`, and `Program.cs`.
- Some later commits depend on parts of this one, especially the leave-feedback service.

Things to account for before revisiting:

- Split into separate ports:
  - dedupe migrations for `GuildXpSettings`, `MessageCounts`, and `CustomVoiceConfig`
  - hot-path/drop-index migrations
  - `UserXpStats` table removal
  - leave-feedback model, controller, service, interactions, strings, config
  - message timestamp retention background service
  - database migrator/connection factory behavior
  - XP/message-count service changes
- Confirm whether the migration prefixes `198` through `204` fit this branch's migration lane. Rename if needed.
- Review `DbMigrator` and `PostgreSqlConnectionFactory` changes carefully. They affect how scripts run, not just the feature being added.
- Decide whether maintenance scripts such as `numeric-to-bigint.sql` and `collation-reindex.sql` belong in source as operator tools or embedded application migrations.
- If porting leave feedback, also inspect `73488691c`; it changes `GuildLeaveFeedbackService` again.
- If porting timestamp retention, verify the retention interval, batch size, cancellation token handling, and expected impact on large `MessageTimestamps` tables.

## 73488691c

Upstream summary: adds dashboard user ID resolution from JWT, tightens dashboard access behavior, updates giveaway/review user identity handling, adds several `[SkipDashboardAccess]` annotations, updates welcome messaging, and adjusts leave-feedback join timestamps.

Why it was skipped:

- Mixed commit with security, dashboard access, welcome-message behavior, and leave-feedback follow-up.
- Whole-commit apply fails because `GuildLeaveFeedbackService` is not present unless `7bf1cf93d` is ported first.
- `HelpService` changes conflicted and alter join/welcome message delivery semantics, including audit-log lookup and DM behavior.

Things to account for before revisiting:

- Consider splitting the dashboard security pieces from the welcome-message changes.
- The security subset may be worthwhile on its own:
  - add `DashboardUserExtensions`
  - use the authenticated dashboard user ID for giveaway entry and review submission
  - add `[SkipDashboardAccess]` to read-only or public-ish endpoints that should not require guild dashboard access
  - update `DashboardAccessEnforcementFilter` so guild-scoped routes are resolved before bypassing non-Bearer requests
- Verify current frontend/API callers. Some endpoints may currently depend on body-provided user IDs or API-key-only access.
- Re-check which endpoints are safe to mark `[SkipDashboardAccess]`; public leaderboard/stat endpoints differ from manage/update endpoints.
- Treat `HelpService` invite/welcome changes as separate product behavior. They require audit-log permission, DM fallback behavior, and dashboard URL correctness.
- Only port the `GuildLeaveFeedbackService` join timestamp change after the leave-feedback service exists locally.

## db93320de

Upstream summary: adds form lifecycle support, drafts, response revisions, review workflow, reviewer roles, validators, launch/review components, new models, migrations, and substantial `FormsService` refactoring.

Why it was skipped:

- Very large feature commit: about 34 files and 4,800+ lines.
- Touches database schema, controllers, module services, validation, components, and dependency registration.
- Direct patch check failed in core integration points such as `MewdekoDb`, `FormsService`, and `Program.cs`.
- Likely needs product/API review rather than a drive-by cherry-pick.

Things to account for before revisiting:

- Start by comparing the current forms feature with upstream's expected model. This commit appears to change the lifecycle contract, not just add optional fields.
- Review migrations `205-AddFormsLifecycle.sql` and `206-AddFormReviewerRole.sql` against the current migration lane and existing forms schema.
- Port models and service slices in dependency order:
  - schema and L2DB models
  - validator/snapshot primitives
  - draft/version/review services
  - controller endpoints and request DTOs
  - Discord component handlers
  - DI registration in `Program.cs`
- Check compatibility with existing persisted form data before applying schema changes.
- Add focused API tests or manual dashboard checks for draft save/resume, submit, review accept/reject, role gating, and versioning.

## 5011fbaa0

Upstream summary: updates memory stats reporting, stats display text, status roles service behavior, and project dependencies.

Why it was skipped:

- Direct patch failed in `Mewdeko.csproj`, `SlashUtility`, and `StatsService`.
- `Mewdeko.csproj` churn is large relative to the visible stats behavior.
- Later cache statistics commit `e3298cf15` assumes the new `IStatsService` memory properties introduced here.

Things to account for before revisiting:

- Split dependency updates from stats behavior. Dependency bumps should be reviewed and tested independently.
- For the stats behavior, port these pieces together:
  - `IStatsService.ManagedHeap`
  - `IStatsService.CommittedHeap`
  - `IStatsService.GcMode`
  - `StatsService` implementations using `GC.GetTotalMemory`, `GC.GetGCMemoryInfo`, and `GCSettings`
  - `.stats` output formatting in both text and slash commands
- Re-check the current `StatsService` file shape before applying upstream code; it conflicted in the dry-run.
- Verify whether the current target framework/runtime exposes all GC APIs used by the upstream implementation.
- Keep package updates out of a drive-by unless there is a specific bug or security reason to take them.

## e3298cf15

Upstream summary: adds cache statistics to stats reporting.

Why it was skipped:

- Depends on the stats service shape from `5011fbaa0`.
- Direct patch failed in text stats, slash stats, `IStatsService`, and `StatsService`.
- Small by line count, but not independent on this branch.

Things to account for before revisiting:

- Port after, or together with, the stats service portion of `5011fbaa0`.
- Confirm the desired cost of walking all guild caches when `.stats` is called. The upstream implementation sums downloaded members, channels, and roles across every guild.
- Verify `DownloadedMemberCount` is meaningful for this bot's gateway/member cache settings.
- Ensure the added stats field does not make embed output too tall for large or verbose deployments.
