# E-Commerce Customer Support Bot

AI-powered customer support API built with **Microsoft Agent Framework 1.0.0-rc2** and **.NET 10**.  
Clean Architecture — five projects, zero compiler warnings.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                          HTTP Requests                               │
│                       EcommerceSupport.Api                           │
│          (ASP.NET Core controllers, DI wiring, OpenTelemetry)        │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ ISupportWorkflow
┌──────────────────────────────▼──────────────────────────────────────┐
│                    EcommerceSupport.Workflows                         │
│                                                                      │
│  SupportWorkflow (orchestrator)                                      │
│       │                                                              │
│       ▼  TriageExecutor ──── AIAgent (triage)                        │
│       │  classifies intent  → WorkflowContext                        │
│       │                                                              │
│       ▼  routes on intent                                            │
│       ├── BillingExecutor  ──── AIAgent (billing)                    │
│       ├── ShippingExecutor ──── AIAgent (shipping)                   │
│       ├── TechnicalExecutor──── AIAgent (technical)                  │
│       └── GeneralExecutor  ──── AIAgent (general)                    │
│                                                                      │
│  All executors extend Executor<TIn,TOut>  (own lightweight base)     │
│  Per-run IWorkflowContext holds cached AgentSessions                 │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ AgentFactory
┌──────────────────────────────▼──────────────────────────────────────┐
│                     EcommerceSupport.Agents                           │
│                                                                      │
│  AgentFactory — builds 5 ChatClientAgents via AsAIAgent()            │
│  Middleware pipeline (AIAgentBuilder):                               │
│    RateLimitingMiddleware → AuditLoggingMiddleware → ChatClientAgent  │
│                                                                      │
│  CustomerContextProvider (IAIContextProvider)                        │
│    reads customerId from AgentSession.StateBag, loads profile        │
└──────────────────────────────┬──────────────────────────────────────┘
               ┌───────────────┼────────────────┐
               │               │                │
┌──────────────▼───┐   ┌───────▼──────┐  ┌─────▼──────────────────────┐
│ EcommerceSupport │   │ EcommerceSupport│  │   EcommerceSupport.Core    │
│ .Infrastructure  │   │ .Infrastructure│  │                            │
│                  │   │ (Tools)        │  │  Domain models             │
│ In-memory repos: │   │                │  │  Interfaces                │
│  Orders          │   │  OrderTools    │  │  Exceptions                │
│  Customers       │   │  BillingTools  │  │                            │
│  Tickets         │   │  ShippingTools │  │  SupportRequest/Response   │
│                  │   │  TechnicalTools│  │  SupportTicket/TriageResult │
└──────────────────┘   └────────────────┘  └────────────────────────────┘
```

---

## Projects

| Project | Role |
|---------|------|
| `EcommerceSupport.Core` | Domain models, interfaces, exceptions — no dependencies |
| `EcommerceSupport.Infrastructure` | In-memory repositories + AI tool implementations |
| `EcommerceSupport.Agents` | Agent factory, middleware, context providers |
| `EcommerceSupport.Workflows` | Workflow orchestration and executor graph |
| `EcommerceSupport.Api` | ASP.NET Core Web API, DI composition root |

---

## Request Flow

```
POST /api/support/sessions        → create session, get sessionId
POST /api/support/sessions/{id}/messages   → send message

1. Controller creates SupportRequest(customerId, sessionId, message)
2. SupportWorkflow
   a. TriageExecutor  → triage agent classifies intent (billing/shipping/technical/general)
                        returns WorkflowContext
   b. Routes to matching DepartmentExecutor
   c. DepartmentExecutor
      · Creates/re-uses AgentSession (stored in InMemoryWorkflowContext)
      · Stores customerId in session.StateBag so CustomerContextProvider loads profile
      · Calls agent.RunAsync([ChatMessage(User, prompt)], session, null, ct)
      · Persists transcript to ITicketRepository
      · Returns SupportResponse
3. Controller returns 200 with response text and ticket ID
```

---

## Key Design Decisions

### Workflow Abstractions Are Ours
`Microsoft.Agents.AI` provides agents and middleware — not workflow orchestration.  
The `Executor<TIn,TOut>` base class and `IWorkflowContext` / `InMemoryWorkflowContext` are our own thin abstractions in `EcommerceSupport.Workflows/Abstractions/`.  This keeps the business logic decoupled from any SDK workflow API that might change.

### Middleware via `AIAgentBuilder`
```csharp
private AIAgent WireMiddleware(AIAgent agent) =>
    new AIAgentBuilder(agent)
        .Use(_rateLimit.RunAsync, _rateLimit.RunStreamingAsync)
        .Use(_audit.RunAsync,     _audit.RunStreamingAsync)
        .Build();
```
Middleware functions have the signature:
```csharp
Task<AgentResponse> RunAsync(
    IEnumerable<ChatMessage> messages,
    AgentSession?            session,
    AgentRunOptions?         options,
    AIAgent                  next,
    CancellationToken        ct)
```

### Session State via StateBag
`AgentSession` in rc2 has no `SessionId` or `Metadata` properties.  
Context is passed through `session.StateBag`:
```csharp
session.StateBag.SetValue("customerId", request.CustomerId);
session.StateBag.SetValue("sessionId",  request.SessionId);

// reading
var id = session?.StateBag.GetValue<string>("customerId");
```

### Tools Go Inside `ChatOptions`
```csharp
new ChatClientAgentOptions
{
    ChatOptions = new()
    {
        Instructions = "...",
        MaxOutputTokens = 1024,
        Tools = [ AIFunctionFactory.Create(tool.MethodAsync), ... ]
    }
}
```

---

## Prerequisites

- **.NET SDK 10.0.100+** — `dotnet --version`
- An **Azure OpenAI** or **OpenAI** endpoint
- Optionally: Azure Application Insights / OTLP collector (OpenTelemetry)

---

## Configuration

Copy `appsettings.json` and add a `appsettings.Development.json` (or use user secrets):

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "...",
    "Deployment": "gpt-4o",
    "MaxTokens": 1024
  },
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317"
  }
}
```

Or use **User Secrets** (recommended for local dev):
```bash
cd src/EcommerceSupport.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-key>"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..."
dotnet user-secrets set "AzureOpenAI:Deployment" "gpt-4o"
```

---

## Running

```bash
cd D:\dotnet\EcommerceSupport
dotnet build
dotnet run --project src/EcommerceSupport.Api
```

The API listens on `http://localhost:5000`.  
OpenAPI document: `http://localhost:5000/openapi/v1.json`

### Example Requests

```bash
# 1. Create a session
curl -X POST http://localhost:5000/api/support/sessions \
  -H "Content-Type: application/json" \
  -d '{"customerId": "cust-001"}'
# → { "sessionId": "abc123", ... }

# 2. Send a message
curl -X POST http://localhost:5000/api/support/sessions/abc123/messages \
  -H "Content-Type: application/json" \
  -d '{"message": "I was charged twice for my last order, order #ORD-9988"}'
# → { "text": "I see your concern...", "ticketId": "TICKET-XYZ", "department": "billing" }
```

---

## Package Management

This solution uses **Central Package Management** (`Directory.Packages.props`).  
All versions are pinned in one file at the solution root — no version attributes in individual `.csproj` files.

| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.Agents.AI` | 1.0.0-rc2 | Agent Framework core |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc2 | Azure OpenAI / OpenAI integration |
| `Microsoft.Extensions.AI` | 10.3.0 | `ChatMessage`, `ChatRole`, `AIFunction` |
| `Azure.AI.OpenAI` | 2.1.0 | Azure OpenAI SDK |
| `Microsoft.AspNetCore.OpenApi` | 10.0.3 | Built-in .NET 10 OpenAPI |
| `Serilog.AspNetCore` | 9.0.0 | Structured logging |
| `OpenTelemetry.*` | 1.11.x | Distributed tracing and metrics |

---

## Project Structure

```
EcommerceSupport/
├── Directory.Build.props          ← net10.0, nullable, implicit usings
├── Directory.Packages.props       ← CPM — all NuGet versions here
├── EcommerceSupport.sln
└── src/
    ├── EcommerceSupport.Core/
    │   ├── Models/                ← Order, Customer, SupportTicket, TriageResult …
    │   ├── Interfaces/            ← IOrderRepository, ITicketRepository, ISupportWorkflow
    │   └── Exceptions/            ← SupportException, OrderNotFoundException …
    ├── EcommerceSupport.Infrastructure/
    │   ├── Repositories/          ← In-memory implementations (swap for Cosmos DB / SQL)
    │   └── Tools/                 ← OrderTools, BillingTools, ShippingTools, TechnicalTools
    ├── EcommerceSupport.Agents/
    │   ├── Factory/               ← AgentFactory (creates & wires all 5 agents)
    │   ├── Memory/                ← CustomerContextProvider (IAIContextProvider)
    │   └── Middleware/            ← AuditLoggingMiddleware, RateLimitingMiddleware
    ├── EcommerceSupport.Workflows/
    │   ├── Abstractions/          ← Executor<TIn,TOut>, IWorkflowContext, InMemoryWorkflowContext
    │   ├── Executors/             ← TriageExecutor, DepartmentExecutor + 4 concrete subclasses
    │   ├── Models/                ← WorkflowContext
    │   └── SupportWorkflow.cs     ← Main orchestrator (ISupportWorkflow impl)
    └── EcommerceSupport.Api/
        ├── Controllers/           ← SupportController
        ├── Extensions/            ← AddAgents, AddRepositories, AddObservability … 
        └── Program.cs
```
