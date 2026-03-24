# AI Agents - Polish Flow

## Overview

The user has a note with raw text (`ContentRaw`). They click **"Polish"** and two AI agents work in the background:
- **Agent 1 (Polisher)** — polishes the raw text into well-structured content
- **Agent 2 (Analyzer)** — analyzes the polished text and extracts a summary and key points

The user does not wait — they immediately receive `204 NoContent`, while the AI works in the background via the Outbox pattern.

---

## Complete Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  USER                                                               │
│                                                                     │
│  POST /api/v1/notes/{id}/polish                                     │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  WEB API (Polish.cs endpoint)                                       │
│                                                                     │
│  → invokes PolishNoteCommandHandler                                 │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  PolishNoteCommandHandler                                           │
│                                                                     │
│  1. Load Note from DB                                               │
│  2. Validate status (not already ProcessingAI)                      │
│  3. Status → ProcessingAI                                           │
│  4. Raise(NotePolishRequestedDomainEvent)                           │
│  5. SaveChangesAsync()                                              │
│     ↓                                                               │
│  InsertOutboxMessagesInterceptor automatically:                     │
│     - collects domain event from entity                             │
│     - serializes to JSON                                            │
│     - creates OutboxMessage in the same transaction                 │
│                                                                     │
│  6. Returns 204 NoContent  ← USER GETS RESPONSE IMMEDIATELY        │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          │  (in the background, async)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  WORKER SERVICE — OutboxBackgroundService                           │
│                                                                     │
│  Every N seconds:                                                   │
│  → OutboxProcessor.Execute()                                        │
│  → SELECT unprocessed FROM outbox_messages                          │
│     FOR UPDATE SKIP LOCKED  (safe for parallelism)                 │
│  → deserializes JSON → NotePolishRequestedDomainEvent               │
│  → DomainEventsDispatcher.Dispatch(event)                           │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  NotePolishRequestedDomainEventHandler                              │
│                                                                     │
│  1. Load Note from DB                                               │
│  2. Call NotePolishOrchestrator.ExecuteAsync(note)                  │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  NotePolishOrchestrator                                             │
│                                                                     │
│  await polisherAgent.RunAsync(noteId)   ← Agent 1 first            │
│  await analyzerAgent.RunAsync(noteId)   ← Agent 2 after Agent 1    │
└──────────┬──────────────────────────────────┬───────────────────────┘
           │                                  │
           ▼                                  ▼
  (Agent 1)                          (Agent 2 - only after Agent 1 completes)
```

---

## Agent 1 — Polisher Agent (Tool Calling Loop)

The agent receives `noteId` and autonomously decides which tools to call.

```
PolisherAgent.RunAsync(noteId)
│
│  System prompt:
│  "You are a professional writer. Polish the note content.
│   Use the available tools to gather information.
│   When done, save the result via the SavePolishedContent tool."
│
│  User message: "Polish the note with ID: {noteId}"
│
│  Available tools:
│  ┌─────────────────────────────────────────────────────────┐
│  │ GetNoteContent(noteId)                                  │
│  │   → returns: Title, ContentRaw, Category                │
│  │   → LLM uses this to understand context and tone        │
│  ├─────────────────────────────────────────────────────────┤
│  │ GetSimilarNotes(noteId, count)                          │
│  │   → returns: last N notes of the same category          │
│  │   → LLM uses this to match the user's writing style     │
│  ├─────────────────────────────────────────────────────────┤
│  │ SavePolishedContent(noteId, polishedContent)            │
│  │   → saves ContentPolished to DB                         │
│  │   → LLM calls this when finished                        │
│  └─────────────────────────────────────────────────────────┘
│
▼
TOOL CALLING LOOP:
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  [1] Send message + tools → Claude API                      │
│                                                             │
│  [2] Claude responds with tool_use:                         │
│      { name: "GetNoteContent", input: { noteId: "abc" } }  │
│                                                             │
│  [3] Your code executes GetNoteContent("abc")               │
│      → returns: { title: "Docker setup", contentRaw: "..." }│
│                                                             │
│  [4] Send tool_result back to Claude                        │
│      → Claude now knows the note content                    │
│                                                             │
│  [5] Claude may call GetSimilarNotes (optional)             │
│      → your code executes, returns result                   │
│                                                             │
│  [6] Claude calls SavePolishedContent(noteId, "polished..") │
│      → your code saves ContentPolished to DB                │
│                                                             │
│  [7] Claude responds with stop_reason: "end_turn"           │
│      → loop ends                                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Agent 2 — Analyzer Agent (Tool Calling Loop)

Same pattern as Agent 1, but different tools and system prompt.

```
AnalyzerAgent.RunAsync(noteId)
│
│  System prompt:
│  "You are a text analyst. Analyze the polished note.
│   Extract a concise summary and key points.
│   Save the result via the SaveAnalysis tool."
│
│  User message: "Analyze the note with ID: {noteId}"
│
│  Available tools:
│  ┌─────────────────────────────────────────────────────────┐
│  │ GetPolishedContent(noteId)                              │
│  │   → returns: ContentPolished (Agent 1 output)           │
│  ├─────────────────────────────────────────────────────────┤
│  │ GetNoteHistory(noteId)                                  │
│  │   → returns: ContentRaw + ContentPolished side by side  │
│  │   → LLM can see what changed                            │
│  ├─────────────────────────────────────────────────────────┤
│  │ SaveAnalysis(noteId, summary, keyPoints)                │
│  │   → saves ContentSummary, KeyPoints to DB               │
│  │   → sets Status → AIReady                               │
│  └─────────────────────────────────────────────────────────┘
│
▼
TOOL CALLING LOOP: (same pattern as Agent 1)
```

---

## Note Status Throughout the Flow

```
User creates note
    → Status: Draft

User clicks "Polish"
    → Status: ProcessingAI  (immediately, synchronous)

Agent 1 finishes (SavePolishedContent)
    → ContentPolished: populated

Agent 2 finishes (SaveAnalysis)
    → ContentSummary: populated
    → KeyPoints: populated
    → Status: AIReady

User reviews and approves
    → Status: Completed
```

---

## Why the Outbox Pattern is Here

```
WITHOUT Outbox:
    Handler creates Note + directly calls AI
    → If AI fails, Note is created but AI was never called
    → No retry mechanism
    → Lost event

WITH Outbox:
    Handler creates Note + saves event in same DB transaction
    → If AI fails, OutboxProcessor will retry
    → MaxRetryCount = 3 (configurable)
    → If all retries exhausted → dead letter (ProcessedOnUtc set, Error saved)
    → Event is never lost
```

---

## Tool Calling — Why This is a "Real Agent"

```
NOT an agent (regular LLM call):
    You → prepare all data → send to LLM → receive response
    LLM is passive — only processes what you gave it

IS an agent (tool calling):
    You → tell LLM "complete this task, you have these tools available"
    LLM autonomously decides:
        - which tools to call
        - in what order
        - whether it needs additional information
        - when the task is complete
    LLM is active — it orchestrates its own execution flow
```

---

## Dependency Graph

```
Polish.cs (endpoint)
    └── PolishNoteCommandHandler
            └── NotePolishRequestedDomainEvent → Outbox
                    └── NotePolishRequestedDomainEventHandler
                            └── NotePolishOrchestrator
                                    ├── PolisherAgent
                                    │       └── NotePolisherTools
                                    │               └── ApplicationDbContext
                                    └── AnalyzerAgent
                                            └── NoteAnalyzerTools
                                                    └── ApplicationDbContext
```
