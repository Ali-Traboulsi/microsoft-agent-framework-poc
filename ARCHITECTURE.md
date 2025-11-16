# 🏗️ Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                          │
│                           (Program.cs)                              │
│                                                                     │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────────┐   │
│  │  Demo         │  │  Interactive  │  │  Workflow          │   │
│  │  Scenarios    │  │  Mode         │  │  Execution         │   │
│  └───────────────┘  └───────────────┘  └────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         AGENT LAYER                                 │
│                                                                     │
│  ┌────────────────┐  ┌────────────────┐  ┌───────────────────┐  │
│  │   Portfolio    │  │   Investment   │  │   Account         │  │
│  │   Manager      │  │   Advisor      │  │   Services        │  │
│  │   Agent        │  │   Agent        │  │   Agent           │  │
│  └────────────────┘  └────────────────┘  └───────────────────┘  │
│           ↓                  ↓                     ↓              │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │              Compliance Officer Agent                        │ │
│  └──────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         WORKFLOW LAYER                              │
│                                                                     │
│  ┌─────────────────────────────┐  ┌──────────────────────────┐   │
│  │   Sequential Workflow       │  │  Concurrent Workflow     │   │
│  │                             │  │                          │   │
│  │   Advisor                   │  │      ┌─ Advisor         │   │
│  │      ↓                      │  │      ├─ Portfolio Mgr   │   │
│  │   Compliance                │  │      └─ Compliance      │   │
│  │      ↓                      │  │                          │   │
│  │   Portfolio Manager         │  │  (Parallel Execution)    │   │
│  └─────────────────────────────┘  └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         TOOL LAYER                                  │
│                                                                     │
│  ┌───────────────────┐  ┌──────────────────┐  ┌────────────────┐ │
│  │ PortfolioTools    │  │ MutualFundTools  │  │ AccountTools   │ │
│  │                   │  │                  │  │                │ │
│  │ • CreatePortfolio │  │ • SearchFunds    │  │ • GetBalance   │ │
│  │ • GetDetails      │  │ • GetFundDetails │  │ • FundPortfolio│ │
│  │ • ListPortfolios  │  │ • ListAllFunds   │  │ • GetHistory   │ │
│  │ • GetAllocation   │  │ • CompareFunds   │  │ • DepositFunds │ │
│  └───────────────────┘  └──────────────────┘  └────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         SERVICE LAYER                               │
│                                                                     │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │              InvestmentDataStore                           │   │
│  │                                                            │   │
│  │  • Account Operations                                     │   │
│  │  • Portfolio Operations                                   │   │
│  │  • Mutual Fund Operations                                 │   │
│  │  • Transaction Operations                                 │   │
│  │  • In-Memory Storage                                      │   │
│  └────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         DATA MODEL LAYER                            │
│                                                                     │
│  ┌──────────┐  ┌──────────┐  ┌────────────┐  ┌───────────────┐  │
│  │ Account  │  │Portfolio │  │ MutualFund │  │ PortfolioHold │  │
│  └──────────┘  └──────────┘  └────────────┘  └───────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │                    Transaction                               │ │
│  └──────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## Data Flow

### Scenario 1: Simple Agent Query

```
User Query
    ↓
[Portfolio Manager Agent]
    ↓
[Function Tool: GetPortfolioDetails]
    ↓
[InvestmentDataStore: GetPortfolio()]
    ↓
[Portfolio Model]
    ↓
Response String
    ↓
Agent (formats response)
    ↓
User sees result
```

### Scenario 2: Investment Workflow

```
User: "Invest $5000 in bonds"
    ↓
┌─────────────────────────────┐
│   Investment Advisor        │
│   - Analyzes request        │
│   - Searches for bond funds │
│   - Recommends USBIF        │
└─────────────────────────────┘
    ↓
┌─────────────────────────────┐
│   Compliance Officer        │
│   - Checks account balance  │
│   - Verifies risk level     │
│   - Approves transaction    │
└─────────────────────────────┘
    ↓
┌─────────────────────────────┐
│   Portfolio Manager         │
│   - Executes purchase       │
│   - Updates portfolio       │
│   - Records transaction     │
└─────────────────────────────┘
    ↓
User receives confirmation
```

### Scenario 3: Concurrent Analysis

```
User: "Review my portfolio"
    ↓
    ├─→ [Investment Advisor] → Fund quality analysis
    │
    ├─→ [Portfolio Manager] → Allocation analysis
    │
    └─→ [Compliance Officer] → Compliance check
         ↓
    All results combined
         ↓
    User sees comprehensive report
```

## Agent Capabilities Matrix

| Agent | Portfolio Tools | Fund Tools | Account Tools | Compliance Tools |
|-------|----------------|------------|---------------|------------------|
| **Portfolio Manager** | ✅ Full Access | ❌ | ✅ FundPortfolio, GetBalance | ❌ |
| **Investment Advisor** | ❌ | ✅ Full Access | ❌ | ❌ |
| **Account Services** | ❌ | ❌ | ✅ Full Access | ❌ |
| **Compliance Officer** | ✅ GetDetails | ✅ GetFundDetails | ✅ GetBalance | ✅ All validation |

## Component Responsibilities

### 🤖 Agents
- Understand natural language queries
- Use appropriate tools to fulfill requests
- Format responses in user-friendly way
- Follow role-specific instructions

### 🔧 Tools
- Provide business logic functions
- Validate inputs
- Call data store methods
- Return formatted strings

### 💾 Data Store
- Manage in-memory data
- Provide CRUD operations
- Maintain data consistency
- Seed initial data

### 📦 Models
- Define data structures
- Enforce type safety
- Include validation logic
- Support enumerations

## Technology Stack

```
┌─────────────────────────────────┐
│    .NET 9.0 Runtime            │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│  Microsoft Agent Framework      │
│  v1.0.0-preview.251114.1       │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│       OpenAI Client             │
│  (gpt-4o-mini model)           │
└─────────────────────────────────┘
```

### NuGet Packages

```xml
<PackageReference Include="Microsoft.Agents.AI.OpenAI" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" />
<PackageReference Include="Azure.AI.OpenAI" />
<PackageReference Include="Azure.Identity" />
```

## Workflow Patterns

### Sequential Pattern

```
START → Agent 1 → Agent 2 → Agent 3 → END

Use case: Investment process with compliance
- Step 1: Advisor recommends fund
- Step 2: Compliance verifies
- Step 3: Portfolio Manager executes
```

### Concurrent Pattern

```
        ┌─→ Agent 1 ─┐
START ──┼─→ Agent 2 ─┼→ AGGREGATE → END
        └─→ Agent 3 ─┘

Use case: Portfolio analysis
- All agents analyze simultaneously
- Results combined at end
- Faster than sequential
```

### Conditional Pattern (Future)

```
START → Agent 1 → [Decision] → Agent 2A
                             └→ Agent 2B → END

Use case: Risk-based routing
- High risk → Extra compliance checks
- Low risk → Fast-track approval
```

## Scalability Considerations

### Current Design (Demo)
- ✅ In-memory data store
- ✅ Synchronous processing
- ✅ Single-instance deployment

### Production Enhancements
- 🔄 Database persistence (SQL Server, PostgreSQL)
- 🔄 Message queue (RabbitMQ, Azure Service Bus)
- 🔄 Caching layer (Redis)
- 🔄 Load balancing
- 🔄 Microservices architecture
- 🔄 Event sourcing
- 🔄 CQRS pattern

## Security Architecture (Production)

```
┌─────────────────────────────────┐
│   Authentication Layer          │
│   (Azure AD, OAuth 2.0)        │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│   Authorization Layer           │
│   (Role-based access control)   │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│   API Gateway                   │
│   (Rate limiting, throttling)   │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│   Agent Framework Application   │
└─────────────────────────────────┘
                ↓
┌─────────────────────────────────┐
│   Secure Data Store             │
│   (Encrypted at rest/transit)   │
└─────────────────────────────────┘
```

## Monitoring & Observability

```
┌──────────────────────────────────┐
│   Application Insights          │
│   - Request telemetry            │
│   - Agent performance            │
│   - Tool execution metrics       │
└──────────────────────────────────┘
                ↓
┌──────────────────────────────────┐
│   Logging (Serilog)              │
│   - Structured logs              │
│   - Error tracking               │
│   - Audit trail                  │
└──────────────────────────────────┘
                ↓
┌──────────────────────────────────┐
│   Metrics Dashboard              │
│   - Agent success rates          │
│   - Response times               │
│   - Tool usage patterns          │
└──────────────────────────────────┘
```

---

**This architecture demonstrates a clean, maintainable, and extensible design for multi-agent systems.**
