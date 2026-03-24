# AI Agent Tool Calling — Cheatsheet

## Koncept u jednoj rečenici

> Ti definišeš alate. Claude odlučuje kada i kako da ih pozove. Ti ih izvršiš i vratiš rezultat. Claude nastavlja dok ne završi.

---

## Stop Reason — zašto Claude staje

| StopReason   | Značenje                                              | Šta radiš          |
|--------------|-------------------------------------------------------|--------------------|
| `EndTurn`    | Claude smatra zadatak završenim                       | Izađi iz loop-a    |
| `ToolUse`    | Claude čeka rezultat alata da bi nastavio             | Izvrši alat, vrati |
| `MaxTokens`  | Potrošeni tokeni, odgovor prekinut                    | Error handling     |
| `StopSequence` | Naišao na definisani stop string                    | Retko se koristi   |

---

## Anatomija jednog Tool poziva

```
Claude vraća:
┌─────────────────────────────────────┐
│ ToolUseBlock                        │
│   Name  = "GetNoteContent"          │  ← koji alat želi
│   ID    = "toolu_abc123"            │  ← jedinstveni ID
│   Input = { "noteId": "guid..." }   │  ← parametri koje je sam izabrao
└─────────────────────────────────────┘

Ti vraćaš:
┌─────────────────────────────────────┐
│ ToolResultBlockParam                │
│   ToolUseID = "toolu_abc123"        │  ← mora biti ISTI ID
│   Content   = "{ result json }"     │  ← rezultat izvršavanja
└─────────────────────────────────────┘
```

---

## Ceo Loop — korak po korak

```
INIT:
messages = [ { Role: User, Content: "uradi X" } ]

LOOP:
  1. Pošalji messages → Claude API
  2. Dobij response (Message)
  3. Konvertuj response.Content → List<ContentBlockParam>   (JSON round-trip)
  4. Dodaj u messages kao Role.Assistant
  5. Ako StopReason == EndTurn  → IZAĐI
  6. Ako StopReason == ToolUse:
       - Prođi kroz response.Content
       - Za svaki ToolUseBlock:
           a. Pročitaj Name, ID, Input
           b. Izvrši alat (tvoj kod)
           c. Upackuj u ToolResultBlockParam sa istim ID-om
       - Dodaj sve rezultate u messages kao Role.User
  7. Ponovi loop (Claude sada ima rezultate, nastavlja)
```

---

## Primer konverzacije u messages[]

```
[0] User:      "Polish the note with ID: abc"
[1] Assistant: tool_use → GetNoteContent { noteId: "abc" }
[2] User:      tool_result → { title: "Docker", contentRaw: "..." }
[3] Assistant: tool_use → SavePolishedContent { noteId: "abc", polishedContent: "..." }
[4] User:      tool_result → { success: true }
[5] Assistant: "Done."   ← EndTurn, izlaz
```

> Claude nema memoriju između API poziva.
> Svaki poziv šalje CELU istoriju od početka.

---

## Definisanje Alata

```csharp
new Tool
{
    Name        = "GetNoteContent",            // kako Claude zove alat
    Description = "Retrieve note by ID.",      // što detaljnije = bolje odluke
    InputSchema = new InputSchema              // default konstruktor već setuje Type="object"
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["noteId"] = JsonSerializer.SerializeToElement(new
            {
                type        = "string",
                description = "The note ID (GUID)."
            })
        },
        Required = ["noteId"]                  // Claude MORA da pošalje ova polja
    }
}
```

---

## Skeleton koda

```csharp
public async Task RunAsync(Guid noteId, CancellationToken ct = default)
{
    var client   = new AnthropicClient { ApiKey = _apiKey };
    var messages = new List<MessageParam>
    {
        new() { Role = Role.User, Content = $"Do X for note {noteId}" }
    };

    while (true)
    {
        // 1. Pozovi API
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model    = "claude-sonnet-4-6",
            MaxTokens = 4096,
            System   = "System prompt...",
            Tools    = _tools,
            Messages = messages
        }, cancellationToken: ct);

        // 2. Dodaj assistant odgovor u istoriju (JSON round-trip konverzija)
        var assistantContent = JsonSerializer.Deserialize<List<ContentBlockParam>>(
            JsonSerializer.Serialize(response.Content))!;
        messages.Add(new() { Role = Role.Assistant, Content = assistantContent });

        // 3. Provjeri razlog zaustavljanja
        if (response.StopReason == StopReason.EndTurn) break;
        if (response.StopReason != StopReason.ToolUse) break;

        // 4. Izvrši alate i prikupi rezultate
        var toolResults = new List<ContentBlockParam>();
        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse)) continue;

            var result = await ExecuteTool(toolUse.Name, toolUse.Input);

            toolResults.Add(new ToolResultBlockParam
            {
                ToolUseID = toolUse.ID,                        // ISTI ID koji je Claude poslao
                Content   = JsonSerializer.Serialize(result)
            });
        }

        // 5. Vrati rezultate Claudeu
        messages.Add(new() { Role = Role.User, Content = toolResults });
    }
}
```

---

## Kako agenti dele podatke

```
Pristup u ovom projektu — indirektno kroz DB:

PolisherAgent                     AnalyzerAgent
     │                                 │
     │  SavePolishedContent()          │  GetPolishedContent()
     ↓                                 ↓
  ┌──────────────────────────────────────┐
  │           Database (Note)            │
  │  ContentPolished = "polished text"   │
  └──────────────────────────────────────┘

Zašto ovako:
  ✓ Ako Analyzer padne → Outbox retry, Polished je već u DB
  ✓ Nije potrebno držati podatke u memoriji između agenata
  ✓ Svaki agent je nezavisan i može se retry-ovati posebno
```

---

## Česte greške

| Greška | Problem | Fix |
|--------|---------|-----|
| `ToolUseID = toolUse.Id` | Pogrešno — capital ID | `ToolUseID = toolUse.ID` |
| `Type = InputSchema.TypeEnum.Object` | Ne postoji | Izbaci — default konstruktor to već setuje |
| `Content = response.Content` | Tip nije kompatibilan | JSON round-trip konverzija |
| Zaboraviš da dodaš tool_result u messages | Claude ostaje blokiran | Uvek dodaj rezultate pre sledećeg loop-a |
| Ne proveriš StopReason | Beskonačan loop | Uvek provjeri EndTurn i neočekivane razloge |
