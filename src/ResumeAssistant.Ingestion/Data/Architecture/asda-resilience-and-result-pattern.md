---
title: "Tier-1 UK Retail Architecture: Railway-Oriented Programming & Result Pattern Resiliency"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Result Pattern & Resiliency Engineering"
sourceLink: "#architecture-resilience-result-pattern"
technologies:
  - "C#"
  - ".NET 6"
  - "Railway-Oriented Programming"
  - "ErrorOr Library"
  - "Polly Resilience"
  - "Dead Letter Queue (DLQ) Architecture"
  - "Clean Architecture"
---

# Resiliency Engineering: Railway-Oriented Programming with ErrorOr

In high-throughput enterprise messaging architectures, traditional exception-based flow control (`try/catch`) introduces severe performance overhead, pollutes logs, and causes Service Bus triggers to re-deliver unrecoverable messages up to `maxDeliveryCount`. To achieve deterministic, high-performance error handling, I established a platform-wide standard leveraging **Railway-Oriented Programming (ROP)** via the `ErrorOr` library.

## 1. ADR-005: Railway-Oriented Result Pattern & Single Dispatch Architecture
The platform wraps all domain operations in `ErrorOr<TSuccess>` results, dispatching message settlement actions deterministically at the Azure Service Bus function boundary:

```
Message Ingestion
       │
       ▼
[MediatR Pipeline / Handler] ─── (Returns ErrorOr<Success>)
       │
       ▼
[BaseFunction: SwitchFirstAsync Dispatcher]
       ├──► Success ────────────────────────────────────────► CompleteMessageAsync()
       │
       ├──► Code 1003 (BusinessValidation: Order Cancelled) ─► CompleteMessageAsync() [Don't Retry]
       │
       ├──► Code 1001 (MoveToDeadLetter: Malformed Data) ────► DeadLetterMessageAsync("UNPROCESSABLE_ENTITY")
       │
       └──► Transient HTTP / Network Exception ─────────────► Polly Retry -> Max Delivery Count (3)
```

## 2. Custom Error Code Taxonomy (ErrorExtension.cs)
```csharp
public static class Errors
{
    public const int MoveToDeadLetterCode = 1001;    // Unrecoverable schema / data inconsistency
    public const int ShutDownGracefullyCode = 1002;   // Host shutdown / scaling drain
    public const int BusinessValidationCode = 1003;   // Expected domain states (e.g. duplicate drop)
    public const int ReturnErrorResponseCode = 1004;  // API boundary error response
}
```

## 3. Composable Functional Pipelines & Single-Point Dispatch (BaseFunction.cs)
Handlers compose multiple enrichment and external adapter steps cleanly without nested `if/else` or `try/catch` pyramids:
```csharp
return await orderDetails.MatchAsync(
    value => this.ProcessPickCompleteAcknowledgmentRequest(
        this.mapper.Map<PickCompletedResponseDto>((request, value)),
        cancellationToken),
    errors => Task.FromResult(ErrorOr<Success>.From(errors))
);
```

At the Service Bus boundary, `BaseFunction.cs` translates error numeric types into deterministic message completion or dead-lettering:
```csharp
return await result.SwitchFirstAsync(
    _ => messageActions.CompleteMessageAsync(message, cancellationToken),
    async error =>
    {
        switch (error.NumericType)
        {
            case Errors.BusinessValidationCode:
                // Expected domain outcome: complete message without retrying
                await messageActions.CompleteMessageAsync(message, cancellationToken);
                break;
                
            case Errors.MoveToDeadLetterCode:
                // Poison message or schema violation: immediately dead-letter with forensics
                await messageActions.DeadLetterMessageAsync(message, 
                    deadLetterReason: "UNPROCESSABLE_ENTITY", 
                    deadLetterErrorDescription: error.Description);
                break;
                
            default:
                await messageActions.DeadLetterMessageAsync(message, 
                    deadLetterReason: error.Code, 
                    deadLetterErrorDescription: error.Description);
                break;
        }
    });
```

## 4. Outbound HTTP Resilience with Polly & Automated 401 Token Refresh
- Outbound adapters for In-Store Picking Services, SAP, Legacy Order Services, and SharePoint configure Polly policies with `WaitAndRetryAsync` using **Decorrelated Jitter Backoff** (`DecorrelatedJitterBackoffV2`).
- **401 Token Refresh Policy**: A specialized Polly handler catches HTTP 401 Unauthorized responses, proactively refreshes the OAuth2 access token via `IStorePickingTokenService` / `ISharepointTokenService`, and seamlessly retries the outbound call without surfacing authentication failures to the caller.
