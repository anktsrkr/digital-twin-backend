---
title: "Tier-1 UK Retail Architecture: Hardware Edge Printing, Self-Healing DNS & FusionCache"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: In-Store Edge Printing & DNS Optimization"
sourceLink: "#architecture-edge-printing"
technologies:
  - "IoT & Hardware Integration"
  - "FusionCache"
  - "TCP/IP Sockets"
  - "Zebra Programming Language (ZPL)"
  - "DNS Caching & Self-Healing"
  - "SharePoint API"
  - "mTLS Security"
---

# In-Store Edge Printing: Self-Healing DNS & Multi-Channel Printing Architecture

In nationwide grocery eCommerce fulfillment, store colleagues picking items across 600+ physical stores depend on instant label printing (tote routing labels, item barcode tags, temperature classification stickers). The platform bridges cloud-native event-driven Azure Functions to physical store hardware under challenging edge constraints including frequent hardware swaps, store WAN latency, and zero-downtime reliability requirements.

## 1. Physical Edge Constraints & Multi-Channel Strategy Routing (ADR-006)
The printing subsystem (`eCommerce Printing Service`) dynamically selects one of three printing strategies based on the request characteristics:

```
Inbound Print Request
         │
         ▼
[Strategy Discriminator]
   ├── Type == "LASER" ──────────► LaserPrint (Enterprise Print Server via mTLS)
   │
   ├── Address starts with "00" ─► VirtualPrint (Render ZPL to PNG -> SharePoint)
   │
   └── Default (MAC Address) ────► DirectLabelPrint (Zebra TCP Socket + FusionCache DNS)
```

## 2. Direct Zebra Network Printing: Self-Healing DNS Prediction & FusionCache
To support multiple hardware generations across 600+ retail stores without maintaining manual configuration databases, the system implements a self-healing DNS resolution heuristic with **FusionCache**:

```csharp
private string PredictZebraPrinterName(string mac, string storeId, bool plusModel = false)
{
    var printerFqdn = plusModel
        ? string.Format(this.settings.PrinterName, "Plus", mac, storeId)
        : string.Format(this.settings.PrinterName, string.Empty, mac, storeId);
        
    try
    {
        Dns.GetHostAddresses(printerFqdn);
        return printerFqdn;
    }
    catch
    {
        return plusModel ? string.Empty : this.PredictZebraPrinterName(mac, storeId, true);
    }
}
```

- **FusionCache Optimization**: The resolved FQDN is cached via `_cache.GetOrSet($"{mac}_{storeId}", ...)` with a configurable multi-hour TTL.
- **Latency & Scale Impact**: Converts an $O(\text{prints per day})$ network DNS lookup cost into $O(\text{printers})$, completely eliminating DNS latency from the critical picking path.
- **Socket Resilience**: Physical socket writes wrap `TcpClient.ConnectAsync` in a dedicated Polly policy handling `SocketException` with bounded retries.

## 3. Virtual Printing: In-Memory ZPL Rendering to SharePoint
For fulfillment hubs requiring digital label generation:
- Parses raw Zebra Programming Language (ZPL) strings in-process using custom `ZplAnalyzer` and `ZplElementDrawer` components to render an exact PNG barcode label.
- Uploads the generated PNG to Microsoft SharePoint for human inspection and dispatch archiving.
- **Two-Tier Caching**: Caches the static SharePoint Drive ID in `FusionCache` for days, while caching the OAuth access token in `IMemoryCache` with a 5-minute sliding TTL.

## 4. Enterprise Laser Printing via mTLS & Certificate Pinning
- Submits bulk manifest and summary reports to an on-premise enterprise print server.
- Configures a custom certificate validation callback (`HttpClientHandler`) enforcing certificate pinning for secure communication across corporate networks.
