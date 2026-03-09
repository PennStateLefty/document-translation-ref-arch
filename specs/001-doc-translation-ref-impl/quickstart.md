# Quickstart: Document Translation Reference Implementation

**Feature Branch**: `001-doc-translation-ref-impl`

## Prerequisites

- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) v1.9+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20 LTS](https://nodejs.org/)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (for `az` commands used by azd hooks)
- An Azure subscription with permissions to create resources

## Deploy the Full Environment

```bash
# 1. Clone the repository
git clone <repo-url>
cd document-translation-ref-arch

# 2. Log in to Azure
azd auth login

# 3. Initialize a new azd environment
azd init

# 4. Provision infrastructure and deploy all services
azd up
```

`azd up` will:
1. Create all Azure resources via Bicep templates (Function App, Static Web App, Storage Account, Translator, Application Insights).
2. Build and deploy the C# Azure Functions backend.
3. Build and deploy the React frontend to Azure Static Web Apps.
4. Output the application URL.

**Expected time**: ~10–15 minutes from a clean state.

## Use the Application

1. Open the application URL printed by `azd up`.
2. Drag and drop document files (PDF, DOCX, XLSX, PPTX, HTML, TXT) onto the upload area, or use the file picker.
3. Select a target language from the dropdown.
4. Click **Translate**.
5. Watch the status update automatically (polls every 5 seconds).
6. When translation completes, click **Download** to get the translated files.

## Local Development

### Backend (Azure Functions)

```bash
cd src/api

# Restore dependencies
dotnet restore

# Run locally (requires Azurite or Azure Storage connection)
func start
```

The Functions app runs on `http://localhost:7071` by default.

### Frontend (React)

```bash
cd src/web

# Install dependencies
npm install

# Start development server
npm run dev
```

The Vite dev server runs on `http://localhost:5173` by default. Configure the API proxy in `vite.config.ts` to forward `/api/*` to the local Functions host.

### Run Tests

```bash
# Backend tests
cd src/api
dotnet test

# Frontend tests
cd src/web
npm test
```

## Tear Down

```bash
# Remove all Azure resources
azd down
```

## Project Structure

```
document-translation-ref-arch/
├── azure.yaml                 # azd manifest (service-to-infra mappings)
├── infra/                     # Bicep IaC templates
│   ├── main.bicep             # Orchestrator template
│   ├── main.parameters.json   # Parameter defaults
│   └── modules/               # Resource-specific modules
├── src/
│   ├── api/                   # C# Azure Functions backend
│   │   ├── Functions/         # HTTP triggers + orchestrator + activities
│   │   ├── Models/            # Data model classes
│   │   ├── Services/          # Translation service wrapper
│   │   └── Tests/             # Unit + integration tests
│   └── web/                   # React frontend
│       ├── src/
│       │   ├── components/    # UI components (Upload, Status, Download)
│       │   ├── hooks/         # Custom React hooks (polling, upload)
│       │   ├── services/      # API client
│       │   └── types/         # TypeScript type definitions
│       └── package.json
├── .github/
│   └── workflows/             # CI/CD pipelines
└── specs/                     # Feature specifications and plans
```

## Key Architectural Patterns Demonstrated

| Pattern | Where | Why |
|---------|-------|-----|
| **Fan-out/fan-in** | Durable Functions orchestrator | Parallel batch processing |
| **Automatic batch splitting** | Orchestrator activity | Handle service limits (1,000 files / 250 MB) |
| **Durable orchestration** | Azure Functions backend | Long-running job management with checkpoints |
| **Simple polling** | React frontend (5 s interval) | Status updates without WebSocket complexity |
| **IaC-first** | Bicep templates in `infra/` | Reproducible infrastructure |
| **azd-native** | `azure.yaml` + Bicep | Single-command deploy/teardown |
