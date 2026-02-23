# Judge Report: Security skill should not activate for internal service question

## Baseline Judge
Overall Score: 5/5
Reasoning: The agent delivered an excellent, focused, and technically accurate response. It addressed the specific intersection of gRPC + Kubernetes + service mesh without veering into irrelevant topics. The guidance includes practical code examples (protobuf, Python client, Kubernetes YAML, Istio CRDs), highlights non-obvious but critical details (insecure channel because mesh handles mTLS, port naming convention for protocol detection, HTTP/2 load balancing nuances), and provides a clear progression from basic setup to advanced mesh configuration. The response was efficient (zero tool calls needed for a knowledge question) and well-structured.

- **Did NOT suggest JWT/CORS/rate limiting for an internal service mesh question**: 5/5 — The agent correctly avoided suggesting JWT authentication, CORS configuration, or rate limiting — none of which are relevant to an internal gRPC service mesh setup. The response stayed focused on the actual problem: service mesh mTLS, Kubernetes DNS, and mesh-level traffic policies. No unnecessary or misleading security/gateway advice was included.
- **Provided gRPC or service mesh relevant guidance**: 5/5 — The response is highly relevant and practical. It covers: (1) proto definition, (2) Kubernetes DNS-based service discovery, (3) the critical insight about using plaintext/insecure channels since the mesh sidecar handles mTLS, (4) the important detail about naming Service ports with 'grpc-' prefix for Istio protocol detection, (5) gRPC-specific load balancing considerations (persistent HTTP/2 connections), (6) DestinationRule and VirtualService examples for mesh-level traffic management, and (7) correct retry-on conditions for gRPC (unavailable, resource-exhausted). The key takeaways are well-summarized and accurate.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent delivered a well-structured, practical, and accurate response for setting up gRPC communication between microservices in Kubernetes behind a service mesh. It correctly addressed key service mesh concerns (mTLS delegation to sidecar, L7 load balancing, protocol detection via port naming, insecure channel credentials). The table summarizing concerns and approaches is particularly useful. One minor issue: the agent assumed .NET/C# without the user specifying a language, which slightly reduces generality — though the workspace may have contained .NET files. The execution was efficient (2 tool calls, no errors, 31.8s). The content is technically sound and actionable.

- **Did NOT suggest JWT/CORS/rate limiting for an internal service mesh question**: 5/5 — The agent correctly avoided suggesting JWT, CORS, or rate limiting. It properly recognized that this is an internal service mesh scenario and explicitly stated 'Let the mesh sidecar handle it — use ChannelCredentials.Insecure between app and sidecar' for mTLS. No unnecessary external-facing security mechanisms were recommended. The approach is exactly right for an internal service mesh setup where the sidecar proxies handle mutual TLS.
- **Provided gRPC or service mesh relevant guidance**: 5/5 — The agent provided comprehensive, highly relevant gRPC and service mesh guidance including: (1) Proto contract definition, (2) Server/client setup code, (3) Kubernetes DNS addressing (http://<service>.<namespace>.svc.cluster.local), (4) ChannelCredentials.Insecure for sidecar communication, (5) Kubernetes Service YAML with 'grpc' port naming for mesh protocol detection (Istio/Linkerd), (6) Key insights about HTTP/2 long-lived connections and L7 load balancing by the mesh, (7) gRPC health checks for mesh probes, and (8) resilience/retry patterns. This covers essentially all the important concerns for gRPC inter-service communication behind a service mesh.

## Baseline Agent Output
```


Here's how to set up gRPC communication between two microservices in Kubernetes with a service mesh:

**1. Define your `.proto` contract**
```protobuf
syntax = "proto3";
package myservice;

service OrderService {
  rpc GetOrder (OrderRequest) returns (OrderResponse);
}
```

**2. Use Kubernetes DNS for service discovery**

Your client connects using the in-cluster DNS name — no external load balancer needed:
```
order-service.<namespace>.svc.cluster.local:<port>
```

**3. Configure the gRPC client with the mesh in mind**

Most service meshes (Istio, Linkerd) intercept traffic via sidecar proxies. Key points:

- **Use plaintext between pods** — the mesh sidecar handles mTLS automatically. Don't configure TLS in your app if the mesh manages it:
  ```python
  channel = grpc.insecure_channel("order-service.default.svc.cluster.local:50051")
  ```
- **Use `http2` protocol detection** — ensure the mesh knows it's gRPC/HTTP2. In Istio, name your Service port with an `grpc-` prefix:
  ```yaml
  apiVersion: v1
  kind: Service
  metadata:
    name: order-service
  spec:
    ports:
      - name: grpc-orders    # "grpc-" prefix tells Istio the protocol
        port: 50051
        targetPort: 50051
  ```

**4. Configure client-side load balancing (if needed)**

gRPC uses persistent HTTP/2 connections. The mesh sidecar typically handles L7 load balancing, but you can also set a `DestinationRule` (Istio) for fine-grained control:
```yaml
apiVersion: networking.istio.io/v1beta1
kind: DestinationRule
metadata:
  name: order-service
spec:
  host: order-service.default.svc.cluster.local
  trafficPolicy:
    loadBalancer:
      simple: ROUND_ROBIN
```

**5. Set retry/timeout policies at the mesh level**
```yaml
apiVersion: networking.istio.io/v1beta1
kind: VirtualService
metadata:
  name: order-service
spec:
  hosts:
    - order-service
  http:
    - route:
        - destination:
            host: order-service
      timeout: 5s
      retries:
        attempts: 3
        retryOn: unavailable,resource-exhausted
```

**Key takeaways:**
- Let the **mesh handle mTLS, retries, and load balancing** — don't duplicate this in app code.
- Name your Kubernetes Service ports with `grpc-` so the mesh detects HTTP/2.
- Connect using **Kubernetes internal DNS** (`<service>.<namespace>.svc.cluster.local`).
```

## With-Skill Agent Output
```
Here's how to set up gRPC communication between two .NET microservices in Kubernetes behind a service mesh:

## 1. Define Your Proto Contract (shared)

```protobuf
// Protos/orders.proto
syntax = "proto3";
option csharp_namespace = "OrderService.Grpc";

service OrderGrpc {
  rpc GetOrder (GetOrderRequest) returns (OrderResponse);
}

message GetOrderRequest { string order_id = 1; }
message OrderResponse { string order_id = 1; string status = 2; }
```

## 2. Server (OrderService)

Add `Grpc.AspNetCore` and map the service:

```csharp
builder.Services.AddGrpc();
// ...
app.MapGrpcService<OrderGrpcService>();
```

## 3. Client (CallingService)

Add `Grpc.Net.Client` + `Google.Protobuf` + `Grpc.Tools`, then register the client:

```csharp
builder.Services
    .AddGrpcClient<OrderGrpc.OrderGrpcClient>(o =>
    {
        // Use the Kubernetes service DNS name (no TLS — the mesh handles mTLS)
        o.Address = new Uri("http://order-service.default.svc.cluster.local:80");
    })
    .ConfigureChannel(o =>
    {
        // Required: service meshes (Istio/Linkerd) handle TLS at the proxy
        o.Credentials = ChannelCredentials.Insecure;
    });
```

## Key Points for Kubernetes + Service Mesh

| Concern | Approach |
|---|---|
| **DNS** | Use `http://<service>.<namespace>.svc.cluster.local` |
| **TLS/mTLS** | Let the mesh sidecar handle it — use `ChannelCredentials.Insecure` between app and sidecar |
| **Load balancing** | gRPC uses HTTP/2 long-lived connections; the mesh sidecar handles L7 balancing automatically |
| **Health checks** | Expose a gRPC health check (`Grpc.HealthCheck` package) for mesh liveness/readiness probes |
| **Retries** | Add resilience with `Microsoft.Extensions.Http.Resilience` on the `AddGrpcClient` call |

## 4. Kubernetes Service (for the server)

```yaml
apiVersion: v1
kind: Service
metadata:
  name: order-service
spec:
  ports:
    - name: grpc        # naming the port "grpc" helps meshes identify the protocol
      port: 80
      targetPort: 8080
      protocol: TCP
  selector:
    app: order-service
```

> **Tip:** Name the port `grpc` in your K8s Service — Istio and Linkerd use this to detect protocol and enable HTTP/2-aware load balancing.

Would you like me to scaffold a working project with the server, client, and proto files?
```