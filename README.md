# .NET Aspire 13 — Batteries Included 🔋

![Banner](banner/Aspire13.png)

> Materiale della sessione presentata a **.NET Conf Rome 2026**.
> Demo progressiva in 7 step — da un progetto vuoto a un'applicazione cloud-native completa con AI, deploy su Azure e caching distribuito.

## Stack tecnologico

| | Versione |
|---|---|
| .NET | 10.0 |
| .NET Aspire | 13.1.1 |
| PostgreSQL | Container gestito da Aspire |
| Redis | Container gestito da Aspire |
| Azure AI Foundry | GPT-4o Mini |
| Azure Container Apps | Deploy target |

## Prerequisiti

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/aspire-cli) — installabile con `dotnet tool install -g aspire`
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (o un runtime OCI compatibile)
- Una sottoscrizione Azure con accesso ad Azure AI Foundry — solo per gli step 04-07

## Quick Start

```bash
git clone https://github.com/narow84/dot-net-conf-rome-2026.git
cd dot-net-conf-rome-2026

# Parti dallo step che preferisci
git checkout feature/01-apphost-dashboard

# Avvia la soluzione con Aspire CLI
cd Aspire13BatteriesIncludedDemo
aspire run
```

La dashboard Aspire si aprirà automaticamente nel browser.

> **Nota:** `aspire run` individua automaticamente l'AppHost nella soluzione. Se necessario puoi specificarlo esplicitamente con `aspire run --project Aspire13BatteriesIncludedDemo.AppHost`.

## Aspire CLI

Questa demo utilizza l'**Aspire CLI** (`aspire`) — lo strumento da riga di comando dedicato per creare, eseguire, pubblicare e deployare applicazioni Aspire.

### Installazione

```bash
dotnet tool install -g aspire

# Verifica
aspire --version   # → 13.1.1
```

### Comandi principali usati nella demo

| Comando | Descrizione | Step |
|---------|-------------|------|
| `aspire run` | Avvia l'AppHost in modalità sviluppo con dashboard | Tutti |
| `aspire add <integration>` | Aggiunge un'hosting integration al progetto | 02, 03, 04, 06, 07 |
| `aspire publish` | Genera gli artefatti di deploy (Bicep, manifest) | 06 |
| `aspire deploy` | Pubblica direttamente sulle destinazioni configurate | 06 |

### Esempi

```bash
# Aggiungere PostgreSQL all'AppHost
aspire add postgres

# Aggiungere Redis
aspire add redis

# Generare i template Bicep per Azure Container Apps
aspire publish --output-path ./deploy-output

# Deploy diretto su Azure (preview)
aspire deploy
```

## Struttura della demo

La demo è organizzata in **7 feature branch** da seguire in sequenza. Ogni branch aggiunge nuove funzionalità incrementando la complessità della configurazione.

### Step 01 — AppHost & Dashboard

```
git checkout feature/01-apphost-dashboard
```

Il punto di partenza: soluzione .NET Aspire con 4 progetti (AppHost, ServiceDefaults, ApiService, Web).
Mostra la **dashboard**, il **service discovery**, i **health check** e l'**avvio ordinato** con `WaitFor`.

---

### Step 02 — PostgreSQL Hosting

```
git checkout feature/02-add-postgres-hosting
```

Aggiunge **PostgreSQL** come risorsa container con volume persistente, **pgAdmin** per l'amministrazione e **comandi custom nella dashboard** (Create App Role, Reset Database). Introduce i parametri segreti.

---

### Step 03 — Client Integration, Product API e Telemetria

```
git checkout feature/03-postgres-client-telemetry
```

Introduce le **client integration** (`Aspire.Npgsql`): connection string automatiche, health check, retry e **tracing OpenTelemetry su ogni query SQL**. Aggiunge un Migration Service, 4 endpoint CRUD per i prodotti e la pagina Blazor Products.

---

### Step 04 — Risorse Esterne: Azure AI Foundry

```
git checkout feature/04-external-resource
```

Integra **Azure AI Foundry** con il modello GPT-4o Mini. Crea un agente "Product Assistant" con il **Microsoft Agent Framework** che usa **tool calling** per interrogare il database Postgres in linguaggio naturale. Telemetria AI end-to-end.

---

### Step 05 — DNS-style Endpoints e Scalar

```
git checkout feature/05-dns-example
```

Migliora la developer experience con **URL leggibili** nella dashboard (`apiservice.dev.localhost`) e **Scalar** come documentazione API interattiva alternativa a Swagger UI.

---

### Step 06 — Deploy su Azure

```
git checkout feature/06-deploy
```

Una singola riga (`AddAzureContainerAppEnvironment`) genera automaticamente tutti i **template Bicep** per il deploy su **Azure Container Apps**: Container Registry, Managed Identity, Role Assignment e risorse AI.

```bash
# Genera i template Bicep
aspire publish --output-path ./deploy-output

# Oppure deploy diretto (preview)
aspire deploy
```

---

### Step 07 — Connection Shaping

```
git checkout feature/07-client-integration
```

Il concetto chiave: **la stessa risorsa** (Postgres, Redis) può essere consumata con **tipi .NET diversi** a seconda della client integration scelta. Dimostra 4 "shape" differenti: `NpgsqlDataSource`, `DbContext` EF Core, `IOutputCacheStore` e `IDistributedCache`.

---

## Struttura della soluzione

```
Aspire13BatteriesIncludedDemo/
├── Aspire13BatteriesIncludedDemo.AppHost/        # Orchestratore Aspire
│   ├── AppHost.cs                                # Definizione risorse e relazioni
│   ├── PostgresExtensions.cs                     # Comandi custom per la dashboard
│   ├── EndpointExtensions.cs                     # DNS-style endpoints
│   └── postgres-init/                            # Script SQL di inizializzazione
├── Aspire13BatteriesIncludedDemo.ServiceDefaults/ # Telemetria, health check, resilienza
├── Aspire13BatteriesIncludedDemo.ApiService/     # Web API (.NET Minimal APIs)
│   └── Data/CatalogDbContext.cs                  # EF Core DbContext (step 07)
├── Aspire13BatteriesIncludedDemo.MigrationService/ # Migrazione DB e seeding
├── Aspire13BatteriesIncludedDemo.Web/            # Blazor Server frontend
└── deploy-output/                                # Template Bicep generati (step 06)
```

## Pacchetti NuGet utilizzati

### Hosting Integrations (AppHost)

| Pacchetto | Scopo |
|-----------|-------|
| `Aspire.Hosting.PostgreSQL` | PostgreSQL + pgAdmin |
| `Aspire.Hosting.Azure.AIFoundry` | Azure AI Foundry |
| `Aspire.Hosting.Azure.CognitiveServices` | Azure Cognitive Services |
| `Aspire.Hosting.Azure.AppContainers` | Azure Container Apps |
| `Aspire.Hosting.Redis` | Redis + RedisInsight |

### Client Integrations (Progetti consumer)

| Pacchetto | Tipo registrato nel DI |
|-----------|------------------------|
| `Aspire.Npgsql` | `NpgsqlDataSource` |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | `DbContext` |
| `Aspire.Azure.AI.Inference` | `IChatClient` |
| `Aspire.StackExchange.Redis.OutputCaching` | `IOutputCacheStore` |
| `Aspire.StackExchange.Redis.DistributedCaching` | `IDistributedCache` |

## Risorse utili

- [Documentazione .NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/aspire-cli)
- [What's new in .NET Aspire 13](https://learn.microsoft.com/dotnet/aspire/whats-new/)
- [Microsoft Agent Framework](https://github.com/microsoft/agents)
- [Scalar — OpenAPI Reference](https://github.com/scalar/scalar)

## Licenza

Questo progetto è rilasciato sotto licenza [MIT](LICENSE).

---

**Presentato a .NET Conf Rome 2026** · [narow84](https://github.com/narow84)
