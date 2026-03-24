# Note Flows

## Status State Machine

```
                    ┌─────────────────────────────────────┐
                    │                                     │
                    ▼                                     │
              ┌──────────┐                               │
   Create ──► │  Draft   │ ◄── Reject / Update           │
              └──────────┘                               │
                    │                                     │
                 Polish                                   │
                    │                                     │
                    ▼                                     │
          ┌───────────────────┐                          │
          │   ProcessingAI    │  (Update blocked)         │
          └───────────────────┘                          │
                    │                                     │
              AI agents finish                           │
                    │                                     │
                    ▼                                     │
            ┌─────────────┐                              │
            │   AIReady   │ ──── Update ────────────────►│
            └─────────────┘                              │
                    │                                     │
                 Approve                                  │
                    │                                     │
                    ▼                                     │
            ┌─────────────┐                              │
            │  Completed  │ ──── Update ────────────────►│
            └─────────────┘
```

---

## Endpoints Overview

| Method | Route | Status required | Status after |
|--------|-------|-----------------|--------------|
| `POST` | `/notes` | — | `Draft` |
| `GET` | `/notes` | any | — |
| `GET` | `/notes/{id}` | any | — |
| `PUT` | `/notes/{id}` | not `ProcessingAI` | `Draft` (if was AIReady/Completed) |
| `POST` | `/notes/{id}/polish` | `Draft` or `AIReady` | `ProcessingAI` |
| `POST` | `/notes/{id}/approve` | `AIReady` | `Completed` |
| `POST` | `/notes/{id}/reject` | `AIReady` | `Draft` |

---

## 1. Create Note

```
POST /notes
     │
     ▼
CreateNoteCommandHandler
  → new Note { Status: Draft }
  → Raise(NoteCreatedDomainEvent)
  → SaveChanges
     │
     │  InsertOutboxMessagesInterceptor (same transaction)
     │  → OutboxMessage { NoteCreatedDomainEvent }
     │
     ▼
Worker picks up NoteCreatedDomainEvent
  → NoteCreatedDomainEventHandler
  → OllamaService.GetCategoryAsync(ContentRaw)
  → note.Category = result
  → SaveChanges

Returns: 200 OK { id }
```

---

## 2. Polish Note

```
POST /notes/{id}/polish
     │
     ▼
PolishNoteCommandHandler
  Validates: Status == Draft || AIReady
  → Status = ProcessingAI
  → Raise(NotePolishRequestedDomainEvent)
  → SaveChanges
     │
     │  InsertOutboxMessagesInterceptor (same transaction)
     │  → OutboxMessage { NotePolishRequestedDomainEvent }
     │
Returns: 204 NoContent  ◄── user gets response immediately

     │  (background, via Worker)
     ▼
NotePolishRequestedDomainEventHandler
  → NotePolishOrchestrator.ExecuteAsync(noteId)
       │
       ├── ContentPolished == null?
       │     YES → PolisherAgent.RunAsync(noteId)
       │               → Tool calling loop (Claude API)
       │               → SavePolishedContent → note.ContentPolished
       │
       └── ContentSummary == null?
             YES → AnalyzerAgent.RunAsync(noteId)
                       → Tool calling loop (Claude API)
                       → SaveAnalysis → note.ContentSummary
                                      → note.KeyPoints
                                      → note.Status = AIReady
```

---

## 3. Update Note

```
PUT /notes/{id}
     │
     ▼
UpdateNoteCommandHandler
  Validates: Status != ProcessingAI

  Status == Draft
    → Title, ContentRaw updated
    → Status stays Draft

  Status == AIReady || Completed
    → Title, ContentRaw updated
    → ContentPolished = null   ← AI results no longer valid
    → ContentSummary  = null
    → KeyPoints       = null
    → Status = Draft           ← must re-polish

Returns: 204 NoContent
```

---

## 4. Approve Note

```
POST /notes/{id}/approve
     │
     ▼
ApproveNoteCommandHandler
  Validates: Status == AIReady
  → Status = Completed

Returns: 204 NoContent
```

---

## 5. Reject Note

```
POST /notes/{id}/reject
     │
     ▼
RejectNoteCommandHandler
  Validates: Status == AIReady
  → Status = Draft
  → ContentPolished = null
  → ContentSummary  = null
  → KeyPoints       = null

Returns: 204 NoContent
```

---

## Note Fields Throughout the Flow

```
Field            Draft   ProcessingAI   AIReady     Completed
─────────────────────────────────────────────────────────────
Title            ✓       ✓              ✓           ✓
ContentRaw       ✓       ✓              ✓           ✓
ContentPolished  null    null*          ✓           ✓
ContentSummary   null    null*          ✓           ✓
KeyPoints        null    null*          ✓           ✓
Category         ✓**     ✓              ✓           ✓

* set by AI agents during processing
** set by Ollama after Create via Outbox
```

---

## Outbox Retry Behavior

```
On failure during AI processing:

  RetryCount < MaxRetryCount
    → ProcessedOnUtc stays null
    → RetryCount++
    → Worker retries on next cycle

  RetryCount >= MaxRetryCount (dead letter)
    → ProcessedOnUtc = now
    → Error message saved
    → Note stays in ProcessingAI ← manual intervention needed

Retry safety (idempotency):
  Orchestrator checks before each agent:
  → ContentPolished != null → skip PolisherAgent
  → ContentSummary  != null → skip AnalyzerAgent
```

---

## Architecture Flow (Request → Response)

```
HTTP Request
     │
     ▼
Web.Api (Minimal API Endpoint)
     │
     ▼
Application (CommandHandler / QueryHandler)
     │
     ├── Domain (Note, NoteStatus, DomainEvents)
     │
     └── Infrastructure
           ├── Database (EF Core + PostgreSQL)
           ├── Outbox (InsertOutboxMessagesInterceptor)
           └── AI (OllamaService, PolisherAgent, AnalyzerAgent)

Worker.Service (background)
     │
     ▼
OutboxProcessor (every N seconds)
     │
     ▼
DomainEventsDispatcher
     │
     ├── NoteCreatedDomainEventHandler → OllamaService
     └── NotePolishRequestedDomainEventHandler → NotePolishOrchestrator
```
