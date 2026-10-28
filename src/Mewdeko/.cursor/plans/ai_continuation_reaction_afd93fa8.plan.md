---
name: AI Continuation Reaction
overview: Add an opt-in continuation path when an AI provider stops because it reached its output token cap. The user will be prompted to react with a checkmark, and only the original requester can trigger a follow-up continuation response.
todos:
  - id: detect-truncated-stop
    content: Add a helper in AiService to identify output-limit stop reasons such as Claude max_tokens.
    status: pending
  - id: track-continuations
    content: Add continuation reaction constants, tracking record, tracking dictionary, and pruning logic alongside cleanup tracking.
    status: pending
  - id: prompt-for-continuation
    content: After final response update, send a continuation prompt when stopReason indicates truncation and attach the checkmark reaction.
    status: pending
  - id: handle-continuation-reaction
    content: Extend the ReactionAdded handler to authorize the requester and launch a continuation request.
    status: pending
  - id: validate-build
    content: Run a targeted dotnet build with isolated output and check lints for edited files.
    status: pending
isProject: false
---

# AI Continuation Reaction

Implement a scoped continuation flow in [`E:/Dev/Mewdeko/src/Mewdeko/Modules/Utility/Services/AiService.cs`](E:/Dev/Mewdeko/src/Mewdeko/Modules/Utility/Services/AiService.cs), reusing the existing reaction authorization pattern.

Key existing anchors:

```csharp
private readonly ConcurrentDictionary<ulong, CleanupTrackedResponse> trackedResponseCleanupRequests = new();
private static readonly Emoji CleanupReaction = new("🗑️");
handler.Subscribe("ReactionAdded", "AiService", HandleAiCleanupReactionAdded);
```

```csharp
if (streamFinishInfo.IsFinished)
{
    stopReason = streamFinishInfo.StopReason;
    logger.LogInformation($"AI stream finished with stop reason: {stopReason}");
    break;
}
```

```csharp
await UpdateMessageEmbed(true);
await AttachCleanupReactionForRequester(userMsg.Author.Id, regularMessage, webhookMessageId, userMsg.Channel);
```

Plan:

- Detect truncation after streaming using provider stop reasons. Start with Claude's `max_tokens`; include OpenAI/Groq/Grok compatible reasons like `length` if their parsers return them.
- Add a `ContinueReaction` checkmark and a second tracked dictionary, similar to cleanup tracking, keyed by the prompt/response message ID and scoped to the requester.
- When a response ends due to the token cap, send a small follow-up prompt such as: `Response hit the model output limit. React ✅ to continue.` Attach the checkmark to that prompt and track it for the same 12-hour lifetime as cleanup.
- Update the existing reaction handler to route cleanup reactions and continuation reactions separately. Only the original requester can trigger either action.
- On checkmark, remove the tracked continuation request, optionally delete/mark the prompt message, then call the AI flow again with a synthetic continuation prompt like: `Continue the previous answer exactly where it stopped. Do not repeat prior content.` The existing DB conversation already contains the partial assistant message, so the continuation request can use normal context.
- Keep the continuation response as a normal new AI response, including current embed splitting, provider branding, token footer, and cleanup reaction behavior. If the continuation also hits `max_tokens`, show another continuation prompt.
- Keep this independent from the hardcoded Claude request cap in [`E:/Dev/Mewdeko/src/Mewdeko/Modules/Utility/Services/Impl/ClaudeClient.cs`](E:/Dev/Mewdeko/src/Mewdeko/Modules/Utility/Services/Impl/ClaudeClient.cs). We can raise/configure that separately later, but the reaction continuation fixes the immediate cut-off UX without changing provider cost behavior.