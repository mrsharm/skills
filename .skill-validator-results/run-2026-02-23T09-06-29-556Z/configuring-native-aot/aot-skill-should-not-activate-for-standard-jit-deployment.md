# Judge Report: AOT skill should not activate for standard JIT deployment

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent provided a clear, well-structured, and accurate response covering the main deployment methods for ASP.NET Core to Azure App Service. It correctly used standard `dotnet publish` without unnecessary AOT flags, offered multiple deployment paths (CLI, Visual Studio, CI/CD), and included a practical quick-start section. The response was efficient (no tool calls needed for a knowledge question) and concise. It loses a point from perfect because it could have mentioned a few more practical details like configuring app settings, connection strings, or the App Service plan tier, but for the specific question asked, it's a solid answer.

- **Did NOT suggest AOT publishing for a standard App Service deployment**: 5/5 — The agent did not mention AOT (Ahead-of-Time) publishing at all. All suggested commands use the standard `dotnet publish -c Release` without any AOT-related flags like `-p:PublishAot=true` or references to native AOT. This is correct for a standard App Service deployment.
- **Suggested standard dotnet publish or deployment approach**: 5/5 — The agent clearly suggested `dotnet publish -c Release -o ./publish` as the standard publish command, and provided multiple valid deployment approaches: Azure CLI (`az webapp deploy`, `az webapp up`), Visual Studio publish wizard, and GitHub Actions CI/CD. The quick-start section also included the zip deployment workflow which is a very common and well-documented approach. All suggestions are standard and appropriate.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent provided a clear, well-structured, and accurate response covering multiple standard deployment methods for ASP.NET Core to Azure App Service. It correctly used `dotnet publish -c Release`, suggested appropriate Azure CLI commands, mentioned CI/CD via GitHub Actions, and included useful configuration tips. It did not make any incorrect suggestions like AOT publishing. The response was concise, practical, and actionable. The only reason it doesn't get a 5 is that it didn't use any tools to check the user's project for tailored advice (though it did offer to do so), and it could have mentioned Visual Studio publishing or Azure DevOps pipelines as additional options. But for the specific rubric criteria, it performs excellently.

- **Did NOT suggest AOT publishing for a standard App Service deployment**: 5/5 — The agent did not mention AOT (Ahead-of-Time) publishing at all. All suggested approaches use standard `dotnet publish -c Release`, which is the correct default for Azure App Service deployments.
- **Suggested standard dotnet publish or deployment approach**: 5/5 — The agent suggested multiple standard and well-established deployment approaches: (1) `dotnet publish` + Azure CLI `az webapp deploy`, (2) ZIP deploy with `az webapp deployment source config-zip`, and (3) GitHub Actions CI/CD with the `azure/webapps-deploy@v3` action. All are legitimate, commonly-used methods for deploying ASP.NET Core apps to Azure App Service. The agent also included helpful supplementary tips about runtime stack, environment variables, and startup command.

## Baseline Agent Output
```


Here are the main ways to publish an ASP.NET Core app to Azure App Service:

### 1. CLI (Azure CLI + dotnet)
```powershell
# Publish your app
dotnet publish -c Release -o ./publish

# Deploy with Azure CLI
az webapp deploy --resource-group <group> --name <app-name> --src-path ./publish.zip
```

### 2. One-command deploy (Azure CLI)
```powershell
# From your project directory — creates the App Service if needed
az webapp up --name <app-name> --resource-group <group> --runtime "DOTNETCORE:8.0"
```

### 3. Visual Studio
Right-click project → **Publish** → **Azure** → **Azure App Service** → follow the wizard.

### 4. GitHub Actions (CI/CD)
Add a workflow that runs `dotnet publish` and uses the `azure/webapps-deploy` action with a publish profile.

---

**Quick start (most common path):**
```powershell
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip
az login
az webapp deploy --resource-group MyResourceGroup --name my-app-name --src-path ./publish.zip --type zip
```

Would you like help with a specific deployment method or setting up CI/CD?
```

## With-Skill Agent Output
```


Here are the main ways to publish an ASP.NET Core app to Azure App Service:

### 1. CLI (`dotnet publish` + Azure CLI)
```powershell
# Build a release package
dotnet publish -c Release -o ./publish

# Deploy with Azure CLI
az webapp deploy --resource-group <rg> --name <app-name> --src-path ./publish
```

### 2. ZIP Deploy (one-liner)
```powershell
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath publish.zip
az webapp deployment source config-zip --resource-group <rg> --name <app-name> --src publish.zip
```

### 3. GitHub Actions (CI/CD)
Add a workflow at `.github/workflows/deploy.yml` that runs `dotnet publish` and uses the `azure/webapps-deploy@v3` action with a publish profile.

### Key things to set:
- **Runtime stack** — match your `.csproj` `TargetFramework` (e.g., `net8.0` → .NET 8 on App Service).
- **`ASPNETCORE_ENVIRONMENT`** — set to `Production` in App Service Configuration.
- **Startup command** — usually auto-detected, but you can set `dotnet YourApp.dll` explicitly.

Would you like me to look at your project and generate a specific publish command or deployment workflow?
```