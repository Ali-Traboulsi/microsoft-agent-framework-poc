# Observability & Monitoring Guide

## Overview

This application implements **comprehensive observability** using **OpenTelemetry** to provide full transparency into the Master Agent's decision-making process and sub-agent coordination.

## Architecture

### Hybrid Observability Approach

We combine two complementary observability strategies:

1. **Built-in Microsoft Agent Framework Instrumentation**
   - Automatic tracing of AI agent interactions
   - Token usage metrics
   - Model latency tracking
   - Tool call instrumentation

2. **Custom Business Metrics**
   - Master Agent delegation patterns
   - Sub-agent utilization
   - Workflow coordination timing
   - Success/failure rates

## OpenTelemetry Stack

### Installed Packages

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.14.0" />
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.4.0" />
```

### Configured Components

#### Tracing (Distributed Traces)

**ActivitySources:**
- `Microsoft.Agents.AI` - Built-in agent framework tracing
- `Microsoft.Extensions.AI` - AI extensions tracing
- `InvestmentBanking.MasterOrchestrator` - Master agent coordination
- `InvestmentBanking.SubAgents` - Sub-agent operations
- `InvestmentBanking.Workflows` - Workflow execution

**Instrumentation:**
- ASP.NET Core HTTP requests/responses
- HTTP client outbound calls
- Custom delegation activities

#### Metrics

**Meters:**
- `Microsoft.Agents.AI` - Agent framework metrics
- `Microsoft.Extensions.AI` - AI extensions metrics
- `InvestmentBanking.MasterOrchestrator` - Custom orchestration metrics
- `InvestmentBanking.SubAgents` - Sub-agent metrics
- `InvestmentBanking.Workflows` - Workflow metrics
- ASP.NET Core runtime metrics
- .NET runtime metrics

**Custom Metrics:**
- `orchestrator.delegations` (Counter) - Number of delegations by sub-agent and success status
- `orchestrator.delegation.duration` (Histogram) - Duration of delegations in milliseconds
- `orchestrator.delegation.errors` (Counter) - Number of delegation failures by error type

## Custom Instrumentation

### MasterOrchestrator Observability

The `MasterOrchestrator` class implements comprehensive observability:

```csharp
// OpenTelemetry components
private static readonly ActivitySource ActivitySource = 
    new("InvestmentBanking.MasterOrchestrator", "2.0.0");
private static readonly Meter Meter = 
    new("InvestmentBanking.MasterOrchestrator", "2.0.0");

// Metrics
private static readonly Counter<long> DelegationCounter = 
    Meter.CreateCounter<long>("orchestrator.delegations");
private static readonly Histogram<double> DelegationDuration = 
    Meter.CreateHistogram<double>("orchestrator.delegation.duration", unit: "ms");
private static readonly Counter<long> DelegationErrorCounter = 
    Meter.CreateCounter<long>("orchestrator.delegation.errors");
```

### Traced Operations

All key orchestration operations are traced:

1. **ProcessRequest** (`MasterOrchestrator.ProcessRequest`)
   - Tags: `conversation.id`, `message.length`, `response.length`, `duration_ms`
   - Records request/response sizes and timing

2. **ProcessRequestStreaming** (`MasterOrchestrator.ProcessRequestStreaming`)
   - Tags: `conversation.id`, `message.length`, `response.total_length`, `response.chunk_count`
   - Tracks streaming chunk count and total response size

3. **DelegateToSubAgent** (`MasterOrchestrator.DelegateToSubAgent`)
   - Tags: `subagent.name`, `request.length`, `response.tools_used`, `response.duration_ms`
   - Records delegation success/failure and tool usage

4. **DelegateToMultipleSubAgents** (`MasterOrchestrator.DelegateToMultipleSubAgents`)
   - Tags: `delegations.count`, `delegations.subagents`, `total.duration_ms`
   - Tracks multi-agent coordination

## Development Monitoring

### Console Exporter

In development, OpenTelemetry data is exported to the console for immediate visibility:

```bash
dotnet run
```

**Console Output Includes:**
- Trace spans with timing and tags
- Metric values with dimensions
- Activity hierarchy showing delegation flow

**Example Console Output:**
```
Activity.TraceId:          a1b2c3d4e5f6g7h8i9j0k1l2
Activity.SpanId:           m3n4o5p6q7r8
Activity.ParentSpanId:     s9t0u1v2w3x4
Activity.ActivityName:     MasterOrchestrator.DelegateToSubAgent
Activity.Kind:             Internal
Activity.StartTime:        2024-01-15T10:30:00.0000000Z
Activity.Duration:         00:00:01.2345678
Activity.Tags:
    subagent.name: PortfolioManager
    request.length: 150
    response.tools_used: 3
    response.duration_ms: 1234
Activity.StatusCode:       Ok
```

## Production Monitoring (Azure Monitor)

### Configuration

To enable Azure Monitor (Application Insights), set the connection string:

**appsettings.json:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=xxx;IngestionEndpoint=https://region.in.applicationinsights.azure.com/"
  }
}
```

**Or via environment variable:**
```bash
export ApplicationInsights__ConnectionString="InstrumentationKey=xxx;..."
```

### Azure Monitor Features

When configured, the application automatically exports to Application Insights:

- **Distributed Traces** → Application Insights Transactions
- **Custom Metrics** → Application Insights Metrics
- **Logs** → Application Insights Logs
- **Dependencies** → HTTP calls to OpenAI API
- **Exceptions** → Error tracking and alerting

### Application Insights Queries

**View Master Agent Delegations:**
```kusto
customMetrics
| where name == "orchestrator.delegations"
| extend subagent = tostring(customDimensions.subagent)
| extend success = tostring(customDimensions.success)
| summarize Count = sum(value) by subagent, success
| order by Count desc
```

**Delegation Duration Percentiles:**
```kusto
customMetrics
| where name == "orchestrator.delegation.duration"
| extend subagent = tostring(customDimensions.subagent)
| summarize 
    p50 = percentile(value, 50),
    p95 = percentile(value, 95),
    p99 = percentile(value, 99)
  by subagent
```

**Delegation Error Analysis:**
```kusto
customMetrics
| where name == "orchestrator.delegation.errors"
| extend subagent = tostring(customDimensions.subagent)
| extend error = tostring(customDimensions.error)
| summarize Count = sum(value) by subagent, error
| order by Count desc
```

**End-to-End Request Trace:**
```kusto
dependencies
| where operation_Name contains "ProcessRequest"
| extend conversationId = tostring(customDimensions["conversation.id"])
| project timestamp, operation_Name, duration, resultCode, conversationId
| order by timestamp desc
```

## Observability Best Practices

### 1. Activity Lifecycle

Always use `using` statements for activities:

```csharp
using var activity = ActivitySource.StartActivity("OperationName", ActivityKind.Internal);
activity?.SetTag("key", "value");

try 
{
    // Do work
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddTag("exception.type", ex.GetType().FullName);
    throw;
}
```

### 2. Metric Dimensions

Use consistent dimension keys for filtering and grouping:

```csharp
DelegationCounter.Add(1, 
    new KeyValuePair<string, object?>("subagent", subAgentName),
    new KeyValuePair<string, object?>("success", true)
);
```

### 3. Correlation IDs

Always propagate conversation IDs through the call chain for request correlation:

```csharp
activity?.SetTag("conversation.id", conversationId);
_logger.LogInformation("Processing request {ConversationId}", conversationId);
```

## Viewing Observability Data

### Option 1: Console (Development)

```bash
dotnet run
```

Look for OpenTelemetry output in the console showing spans and metrics.

### Option 2: Azure Monitor (Production)

1. Configure Application Insights connection string
2. Run the application
3. View in Azure Portal → Application Insights → Investigate → Application Map / Transaction Search

### Option 3: Jaeger (Local Development)

Run Jaeger locally with Docker:

```bash
docker run -d --name jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  jaegertracing/all-in-one:latest
```

Update Program.cs exporter:
```csharp
.AddJaegerExporter(options =>
{
    options.AgentHost = "localhost";
    options.AgentPort = 6831;
})
```

View traces at: http://localhost:16686

### Option 4: Prometheus + Grafana (Local Development)

**docker-compose.yml:**
```yaml
version: '3'
services:
  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
```

Update Program.cs exporter:
```csharp
.AddPrometheusExporter()
```

## Troubleshooting

### No Telemetry Data

**Check:**
1. OpenTelemetry packages installed
2. `AddOpenTelemetry()` called in `Program.cs`
3. ActivitySource/Meter names match configuration
4. Console exporter configured for dev

**Verify Configuration:**
```csharp
builder.Logging.AddFilter("OpenTelemetry", LogLevel.Debug);
```

### Azure Monitor Connection Issues

**Check:**
1. Connection string format correct
2. Network connectivity to Azure
3. Application Insights resource exists
4. Instrumentation key valid

**Test Connection:**
```bash
curl "https://<region>.in.applicationinsights.azure.com/v2/track"
```

### Missing Custom Metrics

**Verify:**
1. Meter created with correct name
2. Meter added to `WithMetrics()` configuration
3. Metrics actually recorded (check counter/histogram calls)

**Debug Metrics:**
```csharp
builder.Logging.AddFilter("OpenTelemetry.Metrics", LogLevel.Debug);
```

## Performance Impact

OpenTelemetry instrumentation has minimal performance overhead:

- **Tracing:** ~1-2ms per span
- **Metrics:** ~0.1ms per measurement
- **Memory:** ~10-50KB per active span

For high-throughput scenarios, consider:
- Sampling traces (e.g., 10% sampling rate)
- Aggregating metrics before export
- Batching telemetry exports

## Future Enhancements

Potential observability improvements:

1. **Custom Metrics**
   - Sub-agent response quality scores
   - Token cost tracking per delegation
   - Workflow completion rates

2. **Advanced Tracing**
   - Trace context propagation to external services
   - Custom baggage for workflow metadata
   - Span events for significant milestones

3. **Alerting**
   - High delegation error rates
   - Slow sub-agent responses
   - Quota exhaustion warnings

4. **Dashboards**
   - Grafana dashboards for real-time monitoring
   - Power BI reports for business metrics
   - Custom Azure Monitor workbooks

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [Azure Monitor OpenTelemetry](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-overview)
- [Microsoft Agent Framework Observability](https://github.com/microsoft/agents)
- [ASP.NET Core Logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
