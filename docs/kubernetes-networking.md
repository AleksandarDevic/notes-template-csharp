# Kubernetes Networking — From Browser to Pod

## 1. What is a Network?

Every device on a network has an **IP address** — think of it as a home address. Your Windows PC has `127.0.0.1` (localhost) — meaning "this computer, right here".

When you type `http://127.0.0.1:5000` in a browser, you're saying: "connect to **this machine**, on **port 5000**".

**Port** is like an apartment number in a building. IP is the building address, port is the apartment. A single IP address can have multiple services listening, each on its own port.

## 2. What is Docker?

Docker creates **isolated containers** — small "virtual machines" inside your computer. Each container has its own network, filesystem, and processes.

Docker creates an **internal virtual network** that only containers can see:

```
Your Windows (physical machine)
├── IP: 127.0.0.1 (localhost)
├── Network: 192.168.1.x (your home WiFi/LAN)
│
└── Docker engine
    └── Internal Docker network: 172.17.0.x
        ├── Container A: 172.17.0.2
        └── Container B: 172.17.0.3
```

`172.17.0.2` exists **only inside Docker**. Your browser on Windows **cannot** reach `172.17.0.2` — it's like a building with no doors to the outside.

## 3. What is Minikube?

Minikube is an **entire Kubernetes cluster** packed into **a single Docker container**:

```
Windows
└── Docker
    └── Minikube container (192.168.49.2)
        └── Kubernetes
            ├── API Server
            ├── etcd
            ├── Pod: postgres (10.244.0.5)
            └── Pod: webapp (10.244.0.6)
```

Three levels of networks, each invisible to the previous one:

```
Windows sees:        127.0.0.1
Docker network:      192.168.49.2     ← Windows CANNOT see this
Kubernetes network:  10.244.0.x       ← even Docker CANNOT see this
```

## 4. How Does Kubernetes Route Traffic Internally?

Inside the cluster there's **yet another** virtual network. Every Pod gets its own IP address (`10.244.0.x`), but these addresses are ephemeral — when a Pod restarts, it gets a new one.

**Service** solves this problem. A Service has a **stable IP address** and a name:

```
webapp-service (ClusterIP: 10.96.45.123)
  → forwards to Pod webapp (10.244.0.6:8080)
```

Other pods use DNS: `postgres-service:5432` — Kubernetes has an internal DNS server that resolves `postgres-service` to `10.96.x.x`.

But all of this works **only inside the cluster**. Your browser on Windows has no idea what `10.96.45.123` is.

## 5. How Does NodePort Expose a Service?

NodePort opens a port on **the minikube node itself** (192.168.49.2):

```
Kubernetes internally:
  Service webapp-service
    ClusterIP: 10.96.45.123:5000     ← only inside the cluster
    NodePort:  192.168.49.2:32133    ← on the node itself
      → Pod webapp: 10.244.0.6:8080
```

Now anyone who can reach `192.168.49.2` can access the service. But...

## 6. The Problem — Windows Cannot See 192.168.49.2

```
Browser (Windows)
  → http://192.168.49.2:32133
  → ❌ "Connection refused" / timeout
```

Why? Because `192.168.49.2` lives on Docker's internal network. Windows has no route to that network. It's like trying to send a letter to an address that doesn't exist in your postal system.

## 7. Solution A — `minikube service` (tunnel)

`minikube service webapp-service` does the following:

1. Opens a port on `127.0.0.1` (your Windows) — e.g. `50780`
2. Starts a process that **listens** on that port
3. Every packet that arrives gets **forwarded** into the Docker network to `192.168.49.2:32133`

```
Browser → 127.0.0.1:50780 → [minikube process] → 192.168.49.2:32133 → Pod:8080
           ↑ Windows           ↑ tunnel              ↑ Docker network     ↑ K8s network
```

Think of it as a **telephone switchboard** — you call a local number (50780), the switchboard connects you to an internal extension (32133) in another building.

That's why:
- The port is **random** — minikube picks a free port
- The terminal must stay **open** — close it and the tunnel disappears
- It changes every time you run the command

## 8. Solution B — `kubectl port-forward` (direct tunnel)

```bash
kubectl port-forward service/webapp-service 5001:5001
```

This works differently:

1. `kubectl` connects to the **Kubernetes API Server** (which already has a tunnel to the cluster)
2. Asks the API Server: "forward traffic from my port 5001 to Service port 5001"
3. The API Server routes it to the Pod

```
Browser → 127.0.0.1:5001 → [kubectl] → [K8s API Server] → Service:5001 → Pod:8081
                              ↑ bypasses NodePort entirely
```

Advantages:
- **You choose the port** (5001, not random)
- Bypasses NodePort — goes directly to the Service/Pod
- Works the same on all OS's

## 9. The Full Picture — All Layers

```
┌─────────────────────────────────────────────────────┐
│  Windows (your PC)                                  │
│  127.0.0.1                                          │
│                                                     │
│  ┌────────────────────────────────────────────────┐ │
│  │  Docker engine                                 │ │
│  │                                                │ │
│  │  ┌──────────────────────────────────────────┐  │ │
│  │  │  Minikube container (192.168.49.2)       │  │ │
│  │  │                                          │  │ │
│  │  │  ┌──────────────────────────────────┐    │  │ │
│  │  │  │  Kubernetes network (10.244.0.x) │    │  │ │
│  │  │  │                                  │    │  │ │
│  │  │  │  Pod webapp    10.244.0.6:8080   │    │  │ │
│  │  │  │  Pod postgres  10.244.0.5:5432   │    │  │ │
│  │  │  └──────────────────────────────────┘    │  │ │
│  │  │                                          │  │ │
│  │  │  NodePort: 192.168.49.2:32133            │  │ │
│  │  └──────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────┘ │
│                                                     │
│  Tunnel: 127.0.0.1:50780 ──→ 192.168.49.2:32133    │
└─────────────────────────────────────────────────────┘
```

Each layer is isolated. Tunnels are the only way to "punch through" these layers from the outside.

## 10. What About Production?

No tunnels. Instead:
- **LoadBalancer** Service — the cloud provider (AWS, GCP) assigns a public IP address
- **Ingress** — a single entry point with a domain name and TLS

```
User → https://api.myapp.com → Ingress → Service → Pod
```

No manual tunnels, no random ports. But for local dev, `port-forward` is your best friend.
