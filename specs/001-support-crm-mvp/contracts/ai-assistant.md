# AI Assistant Port Contract

Application-layer port implemented in Infrastructure by `ClaudeAiAssistant` (Anthropic C# SDK,
NuGet `Anthropic`). The REST surface is in [openapi.yaml](openapi.yaml) under the `Ai` tag; this
file defines the internal contract and the behavior every implementation must satisfy.

```csharp
public interface IAiAssistant
{
    Task<AiResult<TicketSummaryDto>>      SummarizeAsync(AiTicketContext ticket, CancellationToken ct);
    Task<AiResult<SuggestedReplyDto>>     SuggestReplyAsync(AiTicketContext ticket, IReadOnlyList<KbSnippet> articles,
                                                            string? agentInstructions, CancellationToken ct);
    Task<AiResult<ClassificationDto>>     ClassifyAsync(AiTicketContext ticket, IReadOnlyList<CategoryOption> categories,
                                                        CancellationToken ct);
    Task<AiResult<ChatbotReplyDto>>       AnswerChatAsync(IReadOnlyList<ChatTurn> conversation, IReadOnlyList<KbSnippet> articles,
                                                          Language language, CancellationToken ct);
}

// AiResult<T>: Success(T) | Disabled | Unavailable(reason) | Refused(category)
```

## Behavior requirements

| Operation | Model call | Output | Rules |
|---|---|---|---|
| Summarize | single request, effort `medium` | `{ issue, actionsTaken, currentState }` via structured output | Summary in agent's UI language |
| SuggestReply | single request, effort `medium` | `{ body, sourceArticleIds[] }` via structured output | Customer's language; never sent automatically (FR-029) |
| Classify | single request, effort `low` | `{ categoryId, priority, confidence, rationale }` via JSON schema with `categoryId` enum = active category ids | Result stored as suggestion; agent can override (FR-030) |
| AnswerChat | single request, effort `low` | `{ answer, citedArticleIds[], handOff: bool }` | Answer **only** from supplied public, published KB snippets; `handOff = true` when no snippet answers or user asks for a human (FR-031) |

- Model id from configuration `Ai:Model` (default `claude-opus-5-5`).
- System prompt + category list/KB instructions are stable and placed first with a prompt-cache
  breakpoint; per-ticket content follows.
- Server-side refusal fallbacks enabled; `stop_reason` is checked before reading content;
  refusals map to `AiResult.Refused` → UI shows "AI could not help with this request".
- Timeout 20 s; failures/timeouts → `Unavailable` (HTTP 503 `ai.unavailable` at the API).
- Before any call: global `ai.enabled` and per-feature toggle checked → `Disabled` (HTTP 403 `ai.disabled`).
- Data minimization: only subject, public + (for agent features) internal message text of the
  ticket, customer first name and language are sent; no attachments, emails, phones or IDs
  other than category ids. Log token usage, never prompt content.
