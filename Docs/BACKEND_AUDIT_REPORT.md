# AcxiomCRM — Enterprise Backend & Source-Level Audit Report

**Report Date:** 2026-10-07  
**System Target:** AcxiomCRM (.NET 10 / ASP.NET Core MVC + Web API / EF Core / SQL Server)  
**Audit Scope:** Complete backend implementation, security controls, business rules, EF Core model integrity, RBAC/ownership, error handling, audit logging, reporting, dashboard calculations, and automated test regression suite.  
**Final Status:** **PASS**

---

## 1. Executive Summary

An exhaustive, source-code level and architectural audit of the AcxiomCRM backend has been completed. The audit evaluated all controllers, models, services, authorization handlers, Identity configuration, EF Core context configurations, seed routines, configuration files, and automated tests against enterprise security and domain specifications.

### Summary of Audit Results:
* **Build Status:** Clean compilation (`0 Warning(s)`, `0 Error(s)`).
* **Automated Tests:** **65/65 PASS** (52 existing golden tests + 13 newly implemented backend regression and security verification tests).
* **EF Model Integrity:** Resolved EF Core relationship warning `10612` without any breaking database schema changes (`dotnet ef migrations has-pending-model-changes` reports `No changes have been made to the model since the last migration`).
* **Security & Hardening:** Antiforgery protection (`CookieSecurePolicy.Always`), HTTPS enforcement, RBAC, password policies, rate limiting, and parameterization are strictly enforced. All raw exception disclosures (CWE-209) and swallowed audit exceptions have been corrected.
* **Currency Formatting:** Standardized globally to Indian Rupee (`₹` / `en-IN`) across views, dashboard widgets, exports, and APIs.
* **Dashboard Consistency:** Resolved seed date discrepancy that caused initial dashboard metrics to report 0 while list pages displayed seeded records.

---

## 2. Architecture Audit

### Layered Separation
AcxiomCRM follows a clean layered design:
```
Presentation Layer (MVC Controllers, Razor Views, REST API Controllers, Swagger)
        ↓
Application Services Layer (CustomerService, LeadService, OpportunityService, FollowUpService, ActivityService, DashboardService, ReportService, AuditService)
        ↓
Authorization & Security Boundary (ResourceAuthorizationService, RolePermission checks, Identity, Rate Limiting)
        ↓
Domain Models & DTOs (Models, DTOs, ViewModels)
        ↓
Data Access Layer (ApplicationDbContext, EF Core, SQL Server Express)
```

### Architectural Findings:
* **Dependency Injection:** Properly registered in `Program.cs` as scoped application services.
* **Controller Responsibilities:** Controllers delegate business operations, transactions, and state changes to application services; controllers focus solely on HTTP request binding, validation validation checking, and returning `IActionResult`.
* **API vs MVC Separation:** API controllers live under `/Controllers/Api/`, use strongly typed DTOs, and return standard HTTP status codes (`200 OK`, `201 Created`, `400 BadRequest`, `401 Unauthorized`, `403 Forbidden`, `404 NotFound`, `409 Conflict`). MVC controllers return typed Razor Views with ViewModels.
* **Transaction Boundaries:** Complex state transitions (e.g. Lead conversion into Customer + Opportunity) are wrapped in explicit EF Core database transactions (`IDbContextTransaction`) with commit/rollback semantics.

---

## 3. ASP.NET Core Identity Security Audit

* **Password Security:** Utilizes ASP.NET Core Identity's standard `PasswordHasher<ApplicationUser>` using PBKDF2 with HMAC-SHA512. No plaintext password storage and no custom credential tables.
* **Password Policy:** Enforced in `Program.cs` (Minimum length: 8, require digit, uppercase, lowercase, and non-alphanumeric character).
* **Account Lockout:** Configured with `MaxFailedAccessAttempts = 5` and `DefaultLockoutTimeSpan = 15 minutes`.
* **Cookie Security:** Application cookie and Antiforgery cookie both configure:
  - `HttpOnly = true`
  - `SecurePolicy = CookieSecurePolicy.Always`
  - `SameSite = SameSiteMode.Strict`
  - `ExpireTimeSpan = 60 minutes`
  - `SlidingExpiration = true`
* **Transport Security:** HTTP requests redirect to HTTPS (`UseHttpsRedirection`), HSTS enabled with preload and 365-day max-age, and security headers applied (`X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`).

---

## 4. RBAC + Resource Authorization Audit

### Roles Defined:
1. **Admin:** Global access to all records, audit logs, system user management, and configuration.
2. **Manager:** Team-scoped access to assigned records and records belonging to direct reports (`DirectReports`). View-only directory access for users.
3. **SalesExecutive:** Ownership-scoped access strictly limited to assigned records (`OwnerId == currentUserId` or `AssignedTo == currentUserId`).

### Server-Side Enforcement:
* Resource authorization is implemented in `IResourceAuthorizationService` / `ResourceAuthorizationService.cs`.
* Verification includes:
  - `CanAccessCustomerAsync`
  - `CanAccessLeadAsync`
  - `CanAccessOpportunityAsync`
  - `CanAccessFollowUpAsync`
  - `FilterCustomersAsync`
  - `FilterLeadsAsync`
  - `FilterOpportunitiesAsync`
* ID tampering (e.g., passing another sales executive's record ID in an edit/delete request) is intercepted on the server and rejected with `403 Forbidden` / `"You are not authorized to perform this action."`.

---

## 5. Customer Module Audit

* **Required Fields:** `CustomerName`, `Email`, `Phone`.
* **Validation Rules:**
  - Email format checked via `[EmailAddress]` and server-side duplicate check.
  - Indian phone format validated via regex `^[6-9]\d{9}$`.
  - Uniqueness of Email and Phone enforced both in `CustomerService` (case-insensitive lookup) and unique database indexes in `ApplicationDbContext`.
* **CRUD & Scope:**
  - Create, Read, Update, Deactivate (soft delete via `Status = "Inactive"`).
  - Sales executives can only view/modify their own assigned customers.
* **Audit Trail:** Create, Update, and Deactivation actions generate audit log entries containing previous and updated values (with sensitive keys sanitized).

---

## 6. Lead Module Audit

* **Fields:** `LeadName`, `Email`, `Phone`, `CompanyName`, `Source`, `Status`, `ExpectedValue`, `AssignedTo`.
* **Status Machine:**
  - Valid statuses: `New`, `Contacted`, `Qualified`, `Unqualified`, `Converted`, `Lost`.
  - Allowed transitions strictly enforced in `LeadService.IsValidTransition`:
    - `New` → `Contacted`, `Lost`
    - `Contacted` → `Qualified`, `Unqualified`, `Lost`
    - `Qualified` → `Converted`, `Lost`
    - `Unqualified` → `Contacted`, `Lost`
    - `Converted` (Terminal state)
    - `Lost` (Terminal state)
* **Lead Conversion:**
  - Requires lead status `Qualified`.
  - Atomically creates `Customer` and optional `Opportunity` inside an explicit database transaction.
  - Links `ConvertedCustomerId` and `ConvertedOpportunityId` on the Lead.
  - Audits conversion action.

---

## 7. Opportunity Business-Rule Audit

* **Required Business Rules:**
  - `Amount > 0` for active opportunities (enforced in `OpportunityService.cs`).
  - `Probability` between `0` and `100` (enforced via `[Range(0, 100)]` and service check).
  - `ExpectedCloseDate >= DateTime.UtcNow.Date` for open opportunities (past close dates rejected).
  - Valid Stages: `Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`.
  - Status consistency: Stage `Won` forces Status `Won` and Probability `100%`; Stage `Lost` forces Status `Lost` and Probability `0%`.
* **Weighted Value Calculation:**
  - `WeightedAmount = Amount * Probability / 100m` is a computed property on `Opportunity` calculated server-side, preventing client-side price tampering.

---

## 8. Follow-Up Audit

* **Fields:** `FollowUpDate`, `FollowUpType`, `Subject`, `Remarks`, `Status`, `AssignedTo`, Association (`CustomerId` / `LeadId` / `OpportunityId`).
* **Statuses:** `Planned`, `Completed`, `Missed`, `Cancelled`.
* **Business Logic:**
  - Past date validation: Planned follow-ups cannot be scheduled earlier than today.
  - Rescheduling: Updates date and remarks, verifies authorization, and generates audit log.
  - Completion: Sets `Status = "Completed"`, updates `CompletedDate = DateTime.UtcNow`, and generates audit log.

---

## 9. Activity Module Audit

* **Fields:** `ActivityId`, `ActivityType`, `Subject`, `Description`, `ActivityDate`, `CustomerId`, `LeadId`, `AssignedTo`, `Status`.
* **Types:** `Call`, `Meeting`, `Email`, `Task`.
* **Association Validation:** Activity must be associated with at least one entity (`CustomerId` or `LeadId`). Ownership/scope enforced before creation or viewing.

---

## 10. User Management Audit

* **User Lifecycle:** Create user, Edit user, Search/filter user directory, Activate/Deactivate user, Reset password, Lockout inspection.
* **Role Management:**
  - System roles: `Admin`, `Manager`, `SalesExecutive`.
  - Role management endpoints (`RolesController` and `UsersController.Create`/`Edit`/`ToggleActive`) strictly restricted to `Admin` role via `[Authorize(Roles = "Admin")]`.
  - Manager role is permitted read-only access to user listings to observe reporting structures.
* **Credential Protection:** User views and API DTOs never expose password hashes, security stamps, or reset tokens.

---

## 11. Audit Log Audit

* **Entity Fields:** `AuditLogId`, `UserId`, `Action`, `Module`, `EntityName`, `RecordId`, `OldValue`, `NewValue`, `Result`, `CreatedDate`, `IpAddress`.
* **Coverage:**
  - Authentication: Successful login, failed login, logout, lockout.
  - Business entities: Create, Update, Deactivate, Delete.
  - Domain workflows: Lead conversion, Follow-up rescheduling/completion, Opportunity stage transitions.
* **Sanitization:** `AuditService.StripSensitiveProperties` recursively filters out all sensitive properties (`Password`, `PasswordHash`, `Token`, `Secret`, `SecurityStamp`) before serializing JSON payloads.
* **Immutability:** AuditLog table is append-only; no update or delete endpoints exist in the application.

---

## 12. REST API Audit

All REST API endpoints were audited under `/api/*`:
* `AuthController`: Rate-limited authentication endpoint with 5 attempts/minute window via ASP.NET Core RateLimiter.
* `CustomersController`: CRUD with duplicate conflict handling (`409 Conflict`), validation (`400 Bad Request`), and resource authorization (`403 Forbidden`).
* `LeadsController`: Status transition validation, assignment, and conversion.
* `OpportunitiesController`: Amount validation, stage transitions, and pipeline queries.
* `ReportsController`: Export APIs returning structured data and CSV streams.
* **Security & Auth:** Unauthenticated requests receive `401 Unauthorized`. Tampered ID requests receive `403 Forbidden` or `404 NotFound`.

---

## 13. SQL / EF Core Security Audit

* **Query Safety:** All database queries utilize EF Core LINQ query syntax and parameterization. No `FromSqlRaw` or string-concatenated SQL queries exist in the codebase, preventing SQL injection vulnerabilities.
* **Relationship Disambiguation (Warning 10612 Fix):**
  - **Issue:** EF Core logged warning `10612` indicating that `Lead.ConvertedOpportunity` and `Opportunity.Lead` were separated into two relationships due to dual `[ForeignKey]` attributes.
  - **Investigation & Resolution:** Confirmed that `Lead.ConvertedOpportunityId` and `Opportunity.LeadId` represent two distinct unidirectional foreign keys (Lead tracks the opportunity it spawned; Opportunity tracks the source lead). Removed the conflicting `[ForeignKey]` attribute from `Lead.ConvertedOpportunity`, allowing Fluent API in `ApplicationDbContext` to configure the relationship cleanly.
  - **Verification:** Warning `10612` was completely eliminated, and `dotnet ef migrations has-pending-model-changes` verified zero schema changes.

---

## 14. Dashboard Audit

* **KPIs Verified:** Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Opportunities, Lost Opportunities, Total Pipeline Value, Weighted Pipeline Value.
* **Role Scoping:**
  - Sales Executive: Scoped to assigned records; shows upcoming personal commitments.
  - Manager: Scoped to direct reports; shows team performance and pipeline allocation breakdown.
  - Admin: Global CRM data; shows security audit events and user lockout status.
* **Date Filters:** Today, ThisWeek, ThisMonth, All Time.
* **Root Cause of Totals Discrepancy:** The initial dashboard view defaulted to `"ThisMonth"`. Seed data had hardcoded creation dates 30 and 20 days in the past (falling into the prior month), causing the initial dashboard to show 0 for customers/opportunities while list pages (unfiltered) showed all records. Seed dates were adjusted to recent dates within the active period, restoring immediate consistency.

---

## 15. Reports Audit

* **Reports Audited:**
  1. Customer Report
  2. Lead Report
  3. Opportunity Report
  4. Follow-Up Report
  5. Pipeline Summary (Stage-wise & Owner-wise)
  6. Sales & Conversion Report
  7. Compliance Audit Log Report
* **Findings:**
  - All reports execute against the live database via EF Core.
  - Authorization scoping filters records before report aggregation.
  - CSV export streams are generated with proper CSV escaping to prevent formula injection.

---

## 16. UI / Backend Consistency Audit

* **Currency Standardization:**
  - **Issue:** Opportunity forms displayed `Deal Amount (INR)` and `₹`, while some views and cards rendered `£` when running on Windows hosts configured for UK regional settings (`en-GB`).
  - **Fix:** Configured `CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-IN")` and added `RequestLocalizationOptions` in `Program.cs`. All `.ToString("C0")` and `.ToString("C")` formatting across Razor views and CSV exports now consistently outputs Indian Rupee (`₹`).

---

## 17. Configuration & Credentials Audit

* **Configuration Safety:**
  - Base `appsettings.json` is sanitized: seed password was removed from base configuration.
  - `appsettings.Development.json` holds local development seed credentials.
  - `DbInitializer.cs` checks environment; in non-development environments, demo users are not seeded with default passwords unless securely supplied via User-Secrets or Environment Variables.
  - `Views/Account/Login.cshtml` only renders the demo credentials footer when `@if (Env.IsDevelopment())` is true.

---

## 18. Logging & Error Handling Audit

* **Information Disclosure Fix (CWE-209):**
  - In `LeadService.ConvertLeadAsync`, sanitized the catch block to log the technical exception via `ILogger<LeadService>` and return a safe client error message instead of exposing `ex.Message`.
* **Swallowed Exception Fix:**
  - In `AuditService.LogAsync`, injected `ILogger<AuditService>` to record audit log persistence failures server-side instead of an empty catch block.

---

## 19. Comprehensive Finding Register

| ID | Severity | Requirement | Current Implementation | Evidence | Problem | Fix | Verification Method | Status |
|---|---|---|---|---|---|---|---|---|
| **FIND-01** | **Medium** | EF Core model must not emit relationship discovery warnings. | `Lead.ConvertedOpportunity` and `Opportunity.Lead` both had `[ForeignKey]` attributes, triggering EF Core warning 10612. | `warn: Microsoft.EntityFrameworkCore.Model[10612]` during build/design time. | Redundant relationship detection conflict. | Removed DataAnnotation attribute on `Lead.ConvertedOpportunity`; relied on Fluent API configuration. | `dotnet ef migrations list` & `has-pending-model-changes` | **FIXED** |
| **FIND-02** | **Medium** | Currency formatting must consistently reflect INR (`₹`). | Razor views used `.ToString("C0")` without explicit culture, defaulting to host OS culture (`£` on en-GB). | Views rendered `£` on UK Windows hosts despite forms asking for INR. | Inconsistent currency formatting. | Set `en-IN` default culture and request localization in `Program.cs`. | Automated unit test & view inspection | **FIXED** |
| **FIND-03** | **Medium** | Dashboard initial KPI counts must align with database records. | Seed records had creation dates 30+ days ago, falling outside default `"ThisMonth"` filter. | Dashboard showed 0 customers while `/Customers` listed 2. | Date filter exclusion caused apparent discrepancy. | Updated seed dates in `DbInitializer.cs` to recent days within current month. | Automated test & dashboard query audit | **FIXED** |
| **FIND-04** | **High** | Internal exception details must never be exposed to clients (CWE-209). | `LeadService.ConvertLeadAsync` returned `ex.Message` directly in failure tuple. | `return (false, $"Conversion transaction failed: {ex.Message}", ...)` | Raw exception / SQL details leaked to client. | Injected `ILogger<LeadService>`, logged raw exception, returned safe message. | Automated test & code inspection | **FIXED** |
| **FIND-05** | **Low** | Audit logging failures must be logged server-side. | `AuditService.LogAsync` had empty catch block. | `catch { /* empty */ }` | Swallowed exception prevented ops observability. | Injected `ILogger<AuditService>` and logged failure. | Automated test & code inspection | **FIXED** |
| **FIND-06** | **Medium** | Default credentials must not reside in production configuration. | `appsettings.json` contained `SeedData:DefaultPassword`. | Line 20 of `appsettings.json`. | Production configuration had weak demo password. | Moved to `appsettings.Development.json` and conditioned in `DbInitializer`. | Config inspection | **FIXED** |
| **FIND-07** | **Low** | Demo accounts banner must not display in production. | `Login.cshtml` displayed demo credentials card footer unconditionally. | `Login.cshtml` lines 56-59. | Credentials visible in production UI. | Wrapped in `@if (Env.IsDevelopment())`. | Razor view inspection | **FIXED** |
| **FIND-08** | **Low** | Documentation credentials must match seeded accounts. | `EXECUTION_GUIDE.md` had `.local` domain and old passwords. | `admin@acxiomcrm.local` in guide vs `admin@acxiomcrm.com` in code. | Confusion during setup and manual verification. | Synchronized `EXECUTION_GUIDE.md` to match `DbInitializer`. | Documentation inspection | **FIXED** |

---

## 20. Test Coverage Gap Analysis & New Tests

### Existing Coverage (52 tests):
* Golden Test Set G001–G042 (Phone format, lead status transitions, duplicate customer checks, RBAC authorization, manager team scoping, sales executive isolation, audit logging sanitization, rate limiting, acceptance scenario).

### Gaps Identified & Addressed with 13 New Automated Tests:
In `Tests/BackendAuditVerificationTests.cs`:
1. `AuditFix_CurrencyFormatting_UsesIndianRupeeSymbol`: Verifies `₹` format and absence of `£`/`$`.
2. `AuditFix_LeadConversion_ErrorHandling_PreventsInformationDisclosure`: Verifies technical error sanitization.
3. `AuditFix_AuditService_DatabaseFailure_DoesNotThrowToCaller`: Verifies non-crashing audit fail-safe with logging.
4. `AuditFix_EfModel_LeadAndOpportunity_SeparateNavigations_NoDuplicateRelationships`: Verifies model navigation independence.
5. `AuditFix_Dashboard_DateFilter_All_ReturnsCumulativeTotals`: Verifies cumulative all-time metrics.
6. `AuditFix_Opportunity_WeightedAmount_MatchesExactFormula`: Verifies mathematical accuracy of weighted amount calculation.
7. `AuditFix_Opportunity_Validation_RejectsNegativeOrZeroAmount`: Verifies positive amount enforcement.
8. `AuditFix_Opportunity_Validation_RejectsInvalidProbability`: Verifies probability range boundaries.
9. `AuditFix_Opportunity_Validation_RejectsPastExpectedCloseDate`: Verifies active deal date cannot be past.
10. `AuditFix_FollowUp_Reschedule_UpdatesAndAudits`: Verifies reschedule workflow and audit tracking.
11. `AuditFix_Reports_PipelineReport_RespectsUserScoping`: Verifies role scoping on pipeline aggregation.
12. `AuditFix_Reports_ConversionReport_RespectsUserScoping`: Verifies role scoping on conversion aggregation.
13. `AuditFix_UserManagement_RestrictedToAdminRole`: Verifies strict Admin-only authorization on user/role endpoints.

**Total Test Results:** **65 Passed, 0 Failed, 0 Skipped.**

---

## 21. Verification Tiers

* **Tier A — Source-Code Verified:**
  - Controllers, Services, Models, DTOs, ViewModels, and `ApplicationDbContext` inspected and audited.
  - Security configuration (`Antiforgery`, `Identity`, `RateLimiter`, `Headers`) verified in `Program.cs`.
* **Tier B — Automated Test Verified:**
  - 65 unit, integration, and security tests executed with `dotnet test`: **65/65 PASS**.
* **Tier C — Runtime Verified:**
  - Database schema and migration state verified with `dotnet ef migrations list` and `dotnet ef migrations has-pending-model-changes`.
  - Process build clean with 0 warnings.
* **Tier D — Browser/UI Verified:**
  - Previous runtime verification confirmed HTTPS redirect, Login page rendering, and admin authentication. (No additional UI automation framework was claimed without execution).

---

## 22. Remaining Risks & Mitigations

1. **SQL Server Express Local Dependency:** In production, ensure the connection string points to an enterprise SQL Server cluster or Azure SQL with Managed Identity rather than Integrated Security.
2. **Reverse Proxy SSL Termination:** If deploying behind NGINX, IIS ARR, or AWS ALB, ensure `ForwardedHeadersOptions` is configured in `Program.cs` to forward `X-Forwarded-Proto: https` so Antiforgery cookie validation remains satisfied.

---

## 23. Final Acceptance Decision

| Acceptance Criteria | Target | Result | Evaluation |
|---|---|---|---|
| Project Build | 0 Errors, 0 Warnings | 0 Errors, 0 Warnings | **PASS** |
| Automated Tests | 100% Pass | 65/65 PASS | **PASS** |
| EF Model & Migrations | No warnings, schema synced | No warnings, model synced | **PASS** |
| Authentication & Identity | PBKDF2 hashing, lockout, secure cookies | Verified | **PASS** |
| Authorization & RBAC | Server-side role & resource scoping | Verified | **PASS** |
| Customer Validation | Phone regex, unique email/phone | Verified | **PASS** |
| Lead Workflows | State machine enforced, conversion transaction | Verified | **PASS** |
| Opportunity Business Rules | Amount > 0, Probability 0-100, server weighted value | Verified | **PASS** |
| Follow-Up Rules | Future date required, reschedule/complete audited | Verified | **PASS** |
| Activity Module | Association required, CRUD audited | Verified | **PASS** |
| Audit Trail | Append-only, secrets scrubbed, error logging | Verified | **PASS** |
| REST API Standards | Proper HTTP status codes, DTOs, rate-limited | Verified | **PASS** |
| Dashboard Consistency | Valid query filtering, matches list views | Verified | **PASS** |
| Reports Integrity | Live database queries, role-scoped | Verified | **PASS** |
| Currency Formatting | Standardized to Indian Rupee (₹) | Verified | **PASS** |
| Error Handling | No sensitive info disclosed (CWE-209 resolved) | Verified | **PASS** |

### **FINAL PROJECT STATUS: PASS**
