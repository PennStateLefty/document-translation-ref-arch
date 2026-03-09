# Architecture Diagram

```mermaid
graph TB
    subgraph Client
        SWA["React SPA<br/>(Azure Static Web App)"]
    end

    subgraph Compute
        FA["Azure Functions<br/>(Flex Consumption / Linux)<br/>.NET 10 Isolated"]
        ORCH["Durable Functions<br/>Orchestrator"]
        FA --- ORCH
    end

    subgraph Storage
        BLOB["Azure Blob Storage<br/>(allowSharedKeyAccess: false)"]
        SRC["source-documents<br/>container"]
        TGT["translated-documents<br/>container"]
        DEP["deployments<br/>container"]
        BLOB --- SRC
        BLOB --- TGT
        BLOB --- DEP
    end

    subgraph AI
        TRANSLATOR["Azure Document Translator<br/>(Cognitive Services)<br/>(disableLocalAuth: true)"]
    end

    subgraph Observability
        APPINSIGHTS["Application Insights<br/>(disableLocalAuth: true)"]
        LOG["Log Analytics Workspace<br/>(disableLocalAuth: true)"]
        APPINSIGHTS --> LOG
    end

    subgraph "Identity & CI/CD"
        FAMI["Function App<br/>System MI"]
        TMI["Translator<br/>System MI"]
        UMI["User-Assigned MI<br/>(GitHub OIDC Federation)"]
        GHA["GitHub Actions<br/>(OIDC)"]
        GHA -. "federated credentials" .-> UMI
    end

    %% Client to Compute
    SWA -->|"Linked Backend"| FA

    %% Compute to Storage (MI)
    FAMI -->|"Blob Data Owner<br/>Queue Data Contributor<br/>Table Data Contributor<br/>Storage Account Contributor"| BLOB

    %% Compute to Translator (MI)
    FAMI -->|"Cognitive Services User"| TRANSLATOR

    %% Compute to Monitoring (MI)
    FAMI -->|"Monitoring Metrics Publisher"| APPINSIGHTS

    %% Translator to Storage (MI)
    TMI -->|"Blob Data Contributor<br/>(reads source / writes translated)"| BLOB

    %% Deployment
    UMI -->|"Contributor + User Access Admin"| FA
    DEP -. "blob-based deploy<br/>(SystemAssignedIdentity)" .-> FA

    %% FA uses its MI
    FA -. "DefaultAzureCredential" .-> FAMI
    TRANSLATOR -. "system MI" .-> TMI

    %% Styling
    classDef disabled fill:#e8f5e9,stroke:#2e7d32,color:#1b5e20
    classDef identity fill:#e3f2fd,stroke:#1565c0,color:#0d47a1
    classDef client fill:#fff3e0,stroke:#e65100,color:#bf360c

    class BLOB,TRANSLATOR,APPINSIGHTS,LOG disabled
    class FAMI,TMI,UMI identity
    class SWA,GHA client
