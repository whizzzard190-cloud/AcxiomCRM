# AcxiomCRM

A secure, role-aware, auditable, and workflow-driven enterprise Customer Relationship Management (CRM) system built with ASP.NET Core 10, Entity Framework Core, SQL Server, and ASP.NET Core Identity.

---

## 1. Stack & Architecture
- **Framework**: C# / .NET 10.0 (ASP.NET Core MVC + Web API)
- **Data Access**: Entity Framework Core 9.0 (SQL Server Express / LocalDB)
- **Security & Identity**: ASP.NET Core Identity (PBKDF2 hashing, cookie auth, lockout, password policy)
- **Frontend**: Razor Views, Bootstrap 5 CDN, Chart.js CDN (no Tailwind, no npm build step)
- **API Documentation**: Swagger / OpenAPI 3.0
- **Testing**: xUnit with InMemory database support

---

## 2. Setup & Configuration

### Prerequisites
- .NET 10 SDK
- SQL Server Express / Developer / LocalDB

### Configuration & Connection String
The default connection string is configured in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AcxiomCRM;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

### User Secrets (Optional for Production / Custom Passwords)
To manage sensitive passwords or connection strings without committing to source control:
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=AcxiomCRM;..."
dotnet user-secrets set "SeedUsers:AdminPassword" "Admin@Acxiom2026!"
dotnet user-secrets set "SeedUsers:ManagerPassword" "Manager@Acxiom2026!"
dotnet user-secrets set "SeedUsers:SalesExecutivePassword" "Sales@Acxiom2026!"
```

---

## 3. Database Migrations & Seeding
To apply migrations and seed the initial data:
```bash
dotnet ef database update
```
When the application starts, `DbInitializer.InitializeAsync` automatically seeds:
- **Roles**: `Admin`, `Manager`, `SalesExecutive`
- **Default Permissions**: 23 system permissions mapped to roles
- **Seeded Accounts**:
  - **Admin**: `admin@acxiomcrm.local` / `Admin@Acxiom2026!`
  - **Manager**: `manager@acxiomcrm.local` / `Manager@Acxiom2026!`
  - **Sales Executive 1**: `se1@acxiomcrm.local` / `Sales@Acxiom2026!` (reports to Manager)
  - **Sales Executive 2**: `se2@acxiomcrm.local` / `Sales@Acxiom2026!` (reports to Manager)
- **Sample Data**: Seeded customers, leads, opportunities, follow-ups, and activities spread across both executives to demonstrate scope-based visibility.

---

## 4. Running the Application
```bash
dotnet run
```
Navigate to:
- **Web Portal**: `https://localhost:7198` (or configured HTTPS port)
- **Swagger API Documentation**: `https://localhost:7198/swagger`

---

## 5. Running Tests
To run the automated test suite, including the Golden Test Set (G001 - G042):
```bash
dotnet test --nologo --logger "console;verbosity=minimal"
```

---

## 6. Security Features
- **Centralized Scope Authorization**: `IResourceAuthorizationService` dynamically enforces record ownership and managerial hierarchy across all queries and commands.
- **CSRF Protection**: All state-changing MVC POST actions are enforced with `[ValidateAntiForgeryToken]`.
- **Injection Safety**: 100% EF Core parameterized LINQ queries; zero dynamic SQL string concatenation.
- **Strict Cookie Security**: `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `SecurePolicy = CookieSecurePolicy.Always`.
- **HTTP Security Headers**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`, and `HSTS`.
- **Rate Limiting**: Built-in ASP.NET Core FixedWindow rate limiting (5 requests/minute) applied to authentication endpoints.
- **Immutable Auditing**: Append-only audit trail stripping passwords, tokens, and sensitive properties.

---

## 7. Known Limitations & Design Notes
- **LocalDB / SQL Server Express**: Default connection targets `.\SQLEXPRESS`. Update `DefaultConnection` if using SQL Server LocalDB (`(localdb)\mssqllocaldb`) or standard SQL Server instance.
- **AI Layer**: As specified in the project requirements, the optional AI layer is omitted.
