---
title: "Tier-1 UK Retail Architecture: Azure Service Bus Session Sharding Pattern"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Service Bus Sharded Sessions"
sourceLink: "#architecture-servicebus-sharding"
technologies:
  - "Azure Service Bus Premium"
  - "Session-Enabled Queues"
  - "Distributed Systems"
  - "High-Concurrency Concurrency Control"
  - "Azure API Management (APIM)"
  - "FIFO Ordering"
  - "Performance Engineering"
---

# High-Concurrency Messaging: The Sharded Session Pattern

In enterprise grocery eCommerce fulfillment, customer orders and in-store pick lifecycle events for a given physical store must maintain strict chronological ordering. Azure Service Bus **Sessions** provide guaranteed FIFO ordered processing per `SessionId`. However, Service Bus enforces a strict constraint: **a single session can only be processed by one consumer instance at a time**.

If `SessionId` was set naively to `StoreId`, all traffic for a massive supermarket store serialized through a single consumer. During flash demand surges (such as **90,000+ orders within 30 minutes** or **150,000+ Christmas peak orders**), high-volume stores formed severe hot-partition bottlenecks.

```
Naive Session Model (Hot-Partition Bottleneck):
Store #1042 Events ──────► [Session: Store_1042] ──────► (Single Function Receiver) ──► Queue Backlog!

Sharded Session Pattern (4x Parallelism with Shard-Level FIFO):
Store #1042 Events ──┬──► [Session: Store_1042_Dispatch2_1] ──► (Receiver 1)
                     ├──► [Session: Store_1042_Dispatch2_2] ──► (Receiver 2) ──► 4x Concurrent
                     ├──► [Session: Store_1042_Dispatch2_3] ──► (Receiver 3)      Throughput!
                     └──► [Session: Store_1042_Dispatch2_4] ──► (Receiver 4)
```

## 1. ADR-002: Dynamic Sharded Session IDs via APIM Ingress Gateway Policies
To resolve this architectural bottleneck without writing complex custom partition orchestrators, I engineered the **Sharded Session Pattern** directly inside the Azure API Management ingress policy layer:

```xml
<!-- APIM Inbound Policy Expression -->
<choose>
    <when condition="@(context.Variables.GetValueOrDefault<string>("dispatchCode") == "2")">
        <set-variable name="randomNumber" value="@(new Random().Next(1, 5).ToString())" />
        <set-variable name="sessionId" value="@(string.Format("{0}_{1}_{2}", 
            context.Variables["storeId"], 
            context.Variables["dispatchCode"], 
            context.Variables["randomNumber"]))" />
    </when>
    <otherwise>
        <set-variable name="sessionId" value="@(string.Format("{0}_{1}", 
            context.Variables["storeId"], 
            context.Variables["dispatchCode"]))" />
    </otherwise>
</choose>
```

## 2. Architectural Mechanics, Ordering Guarantees & Concurrency Tuning
1. **Dynamic Load Distribution**:
   - For high-volume dispatch streams (`dispatchCode == "2"`), APIM generates a pseudo-random shard number between 1 and 4, appending it to the session key.
   - Traffic for a single physical store is automatically distributed across up to 4 concurrent Service Bus session receivers.
2. **Preserving Ordering Guarantees**:
   - Within each individual shard session, strict FIFO delivery is fully preserved by Azure Service Bus.
   - The business domain easily tolerates partial interleaving between separate orders for the same store, eliminating the hot-partition bottleneck while maintaining order integrity.
3. **Zero Code Changes to Compute Layer**:
   - The optimization was achieved entirely within the APIM gateway policy without requiring architectural rewrites in the downstream .NET Azure Function applications.
4. **Parallelism & Concurrency Tuning**:
   - Downstream Function App consumers are configured with `maxConcurrentSessions = 50` in production and staging (and `16` in non-prod environments), allowing the infrastructure to scale horizontally on App Service Plan autoscale metrics.
