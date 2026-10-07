# AcxiomCRM - Execution & Run Guide

This guide provides the exact commands and step-by-step procedures to build, configure, run, test, and interact with the AcxiomCRM application and REST APIs.

---

## 1. Prerequisites
Ensure you have the following installed on your machine:
- **.NET 10 SDK** (verify with `dotnet --version`)
- **SQL Server Express**, **Developer Edition**, or **LocalDB**
  - Check LocalDB status: `sqllocaldb info`
  - Check SQL Server Express service: `Get-Service *SQL*`

---

## 2. Configuration & Connection Strings

The default configuration in `appsettings.json` points to SQL Server Express:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AcxiomCRM;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
}
```

### If using SQL Server LocalDB instead of SQLEXPRESS:
Update `ConnectionStrings:DefaultConnection` in `appsettings.json` or use `dotnet user-secrets`:
```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=AcxiomCRM;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
```

---

## 3. Database Migration & Initialization

### Apply EF Core Migrations
To ensure the database schema and all tables are created and up to date:
```powershell
dotnet ef database update
```

*(Note: On initial startup, `DbInitializer.cs` will automatically execute, seed all roles, system permissions, default users, and sample business records across multiple sales executives).*

---

## 4. Build Commands

To build the entire solution (both web project and test project):
```powershell
dotnet build --nologo -v q
```
**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 5. Running Automated Tests (Golden Test Set G001 - G042)

To run the complete automated test suite (52 test executions covering validation, scope authorization, security hardening, and the 14-step acceptance scenario):
```powershell
dotnet test --nologo --logger "console;verbosity=minimal"
```
**Expected Output:**
```
Passed!  - Failed: 0, Passed: 52, Skipped: 0, Total: 52, Duration: 2 s - Tests.dll (net10.0)
```

To run a specific test class:
```powershell
dotnet test --filter "FullyQualifiedName~ValidationTests" --logger "console;verbosity=minimal"
dotnet test --filter "FullyQualifiedName~ScopeAndDashboardTests" --logger "console;verbosity=minimal"
dotnet test --filter "FullyQualifiedName~AcceptanceScenarioTests" --logger "console;verbosity=minimal"
```

---

## 6. Starting the Web Application

To run the application locally:
```powershell
dotnet run
```
By default, the application will launch on HTTPS:
- **Web Portal**: `https://localhost:7100` (or the port specified in console output)
- **Swagger / OpenAPI Documentation**: `https://localhost:7100/swagger`

---

## 7. Pre-Seeded User Credentials

| Role | Email / Username | Password | Access Scope |
|---|---|---|---|
| **Admin** | `admin@acxiomcrm.com` | `Password@123!` | Global access: all records, users, roles, audit logs, reports. |
| **Manager** | `manager@acxiomcrm.com` | `Password@123!` | Team access: own records + records of direct reports (sales1, sales2). |
| **Sales Executive 1** | `sales1@acxiomcrm.com` | `Password@123!` | Assigned records only (Alex Executive). Reports to Manager. |
| **Sales Executive 2** | `sales2@acxiomcrm.com` | `Password@123!` | Assigned records only (Sam Executive). Reports to Manager. |

---

## 8. Command-Line API Interaction Examples

### 1. Authenticate / Login via REST API (PowerShell)
```powershell
# Create a session web request to retain auth cookie
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$loginBody = @{
    email = "admin@acxiomcrm.com"
    password = "Password@123!"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7100/api/auth/login" `
                  -Method Post `
                  -ContentType "application/json" `
                  -Body $loginBody `
                  -WebSession $session
```

### 2. Fetch Customers via REST API
```powershell
Invoke-RestMethod -Uri "https://localhost:7100/api/customers" `
                  -Method Get `
                  -WebSession $session
```

### 3. Create a New Customer
```powershell
$customerBody = @{
    customerName = "Reliance Industries"
    email = "contact@ril.com"
    phone = "9820098200"
    companyName = "Reliance"
    city = "Mumbai"
    state = "Maharashtra"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7100/api/customers" `
                  -Method Post `
                  -ContentType "application/json" `
                  -Body $customerBody `
                  -WebSession $session
```

### 4. Fetch Pipeline Summary Report
```powershell
Invoke-RestMethod -Uri "https://localhost:7100/api/reports/pipeline" `
                  -Method Get `
                  -WebSession $session
```

### 5. Logout
```powershell
Invoke-RestMethod -Uri "https://localhost:7100/api/auth/logout" `
                  -Method Post `
                  -WebSession $session
```

---

## 9. Useful Troubleshooting Commands

### Resetting / Recreating Database
If you ever want to reset the database back to clean seeded state:
```powershell
dotnet ef database drop --force
dotnet ef database update
```

### Checking Active Background Processes
```powershell
Get-Process -Name "AcxiomCRM" -ErrorAction SilentlyContinue
```
To stop the web server running in terminal: Press `Ctrl + C`.
