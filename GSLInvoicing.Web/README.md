# GSL Invoicing Web App

ASP.NET Core MVC web app connected to SQL Server tables:
- `dbo.Client`
- `dbo.Invoice`
- `dbo.InvoiceItem`

## Features
- Client list + CRUD page (`/Clients`)
- Invoice list page (`/Invoices`)
- Invoice edit page with add-invoice-item flow (`/Invoices/Edit/{id}`)
- GST rule for new invoice items based on client `GSTCode`:
  - `S` => `15%`
  - anything else => `0%`

## Local Run
1. Update connection string in `appsettings.json` or user secrets.
2. Run:

```powershell
dotnet restore
dotnet run
```

## Azure Hosting (App Service)

### 1) Create Azure resources

```powershell
az group create --name rg-gslinvoicing --location australiaeast
az appservice plan create --name asp-gslinvoicing --resource-group rg-gslinvoicing --sku B1 --is-linux
az webapp create --resource-group rg-gslinvoicing --plan asp-gslinvoicing --name gslinvoicing-webapp --runtime "DOTNETCORE:9.0"
```

### 2) Configure SQL connection string in App Service

```powershell
az webapp config connection-string set --resource-group rg-gslinvoicing --name gslinvoicing-webapp --connection-string-type SQLAzure --settings DefaultConnection="Server=VivoTouchMar23\\MSSQLSERVER01;Database=GSLInvoicing;User Id=gslinvoicing;Password=lesley01*;TrustServerCertificate=True;"
```

### 3) Deploy app

```powershell
dotnet publish -c Release -o .\publish
az webapp deploy --resource-group rg-gslinvoicing --name gslinvoicing-webapp --src-path .\publish
```

## Notes
- For production, store credentials in Azure Key Vault and App Service settings.
- Ensure SQL Server firewall/network allows traffic from Azure App Service outbound IPs.
