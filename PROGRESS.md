# AcxiomCRM Implementation Progress

## Checklist of Phases
- [x] **Phase 0: Workspace Inspection & Discovery** (Completed)
- [x] **Phase 1: Solution Architecture, Data Layer, Identity, Roles, Seed Data & Audit Service** (Completed - EF Core migration created & applied to Local SQL Server Express, DbInitializer and IAuditService implemented)
- [x] **Phase 2: Authorization Framework, Global Layout, Navigation & Shared Components** (Completed - ResourceAuthorizationService, AccountController, Auth views, Layout with Bootstrap 5 CDN & Chart.js, shared partials _Alerts, _ValidationSummary, _Pager, _SearchBar, _DeleteConfirmModal)
- [x] **Phase 3: Customers Module (CRUD, Duplicate Check, Scope, Auditing)** (Completed - ICustomerService, CustomerService, CustomersController, Views, search, sort, pagination, duplicate phone/email detection, scope security, deactivation)
- [x] **Phase 4: Leads Module & Conversion Workflow (CRUD, Transitions, Qualification & Conversion, Auditing)** (Completed - ILeadService, LeadService, LeadsController, Views, state-machine transitions, single-transaction Convert to Customer & optional Opportunity, audit logging)
- [x] **Phase 5: Opportunities Module & Pipeline View (Stages, Validation, Weighted Value, Auditing)** (Completed - IOpportunityService, OpportunityService, OpportunitiesController, List & Kanban Pipeline Views, validation: Amount > 0, Probability 0-100, ExpectedCloseDate, Weighted Amount calculation, auditing)
- [x] **Phase 6: Follow-ups Module (Scheduling, Completing, Rescheduling, Overdue tracking, Auditing)** (Completed - IFollowUpService, FollowUpService, FollowUpsController, Views, polymorphic links, date validation: no past dates for planned/rescheduled, overdue computation, auditing)
- [x] **Phase 7: Activities Module (Calls, Meetings, Emails, Tasks, Linkages, Auditing)** (Completed - IActivityService, ActivityService, ActivitiesController, Views, call/meeting/email/task logging, entity association, filtering and audit logging)
- [x] **Phase 8: User, Role & Permission Management (Admin features, Unlock, Password Reset, Auditing)** (Completed - UsersController, RolesController, ViewModels, Views, Manager view-only check, Admin full management: role assignment, lockout removal/unlock, password reset, and auditing)
- [x] **Phase 9: Audit Log Screen (Read-Only, Filtering, Scope Awareness)** (Completed - AuditLogsController, Views, filter by user/module/action/date, read-only immutable access, team scope for managers, full scope for admins, payload inspection)
- [x] **Phase 10: Executive Dashboard & Dynamic Charts (KPI Cards, Date Filter, 3 Chart.js Visualizations)** (Completed - IDashboardService, DashboardService, DashboardController, Views, 8 KPI cards, date range filtering, 3 Chart.js graphs: Lead Status, Opportunity Pipeline, Monthly Sales, and role-specific widgets for Admin, Manager, and SE)
- [x] **Phase 11: Reporting Engine & CSV Export (8 Comprehensive Reports, Filtering, Scope Enforcement)** (Completed - IReportService, ReportService, ReportsController, Views, 8 reports: Customer, Lead, FollowUp, Opportunity, Pipeline, Conversion, UserActivity, Audit, role scoping, and CSV export functionality)
- [x] **Phase 12: REST API & Swagger / OpenAPI Documentation** (Completed - DTO-only API controllers for Auth, Customers, Leads, Opportunities, FollowUps, and Reports; Swagger/OpenAPI v1 configured at /swagger; built-in FixedWindow AuthRateLimiter; strict status codes 200, 201, 400, 401, 403, 404, 409)
- [x] **Phase 13: Security Hardening** (Completed - [ValidateAntiForgeryToken] on all state-changing MVC POST actions, strict secure cookies HttpOnly/SameSite=Strict/Secure=Always, OWASP security headers X-Content-Type-Options/X-Frame-Options/Referrer-Policy, HSTS, 100% EF Core parameterized queries, rate limiting, sensitive data redaction in AuditService)
- [x] **Phase 14: Comprehensive Test Suite & Golden Tests (G001 - G042)** (Completed - xUnit test suite with 52 automated test executions covering all Golden Tests G001 through G042; 52 passed, 0 failed)
- [x] **Phase 15: Documentation Suite** (Completed - Produced docs/REQUIREMENT_TRACEABILITY.md, docs/SECURITY_CHECKLIST.md, docs/GOLDEN_TEST_SET.md, docs/API_DOCUMENTATION.md, docs/ARCHITECTURE.md, and root README.md)
- [x] **Phase 16: Final 14-Step Acceptance Scenario Verification** (Completed - Verified end-to-end 14-step acceptance scenario in Tests/AcceptanceScenarioTests.cs: anonymous blocked -> register -> dashboard -> invalid customer client-side -> crafted request rejected server-side -> opp Amount<=0 / Probability>100 / past close date rejected -> past follow-up rejected -> SE scope -> Manager team scope -> Admin admin pages -> create/update/delete audited -> /api/customers returns authorized JSON -> dashboard KPIs + charts)

---

## Inferences & Design Decisions ([INFERENCE])
1. **[INFERENCE] ApplicationUser Manager Relationship**: `ManagerId` configured as nullable self-referencing foreign key on `ApplicationUser` with `DeleteBehavior.Restrict`.
2. **[INFERENCE] Phone Validation**: 10-digit Indian mobile numbers starting with digits 6-9 via regex `^[6-9]\d{9}$`.
3. **[INFERENCE] Role on Registration**: All self-service user registrations automatically assign the `SalesExecutive` role; only Admins can assign or alter roles.
4. **[INFERENCE] API Authentication**: Uses ASP.NET Core Identity authentication cookies with anti-forgery and rate limiting, providing seamless single sign-on across MVC and REST API layers.
5. **[INFERENCE] Append-Only Auditing**: `IAuditService` implements only `LogAsync` and `LogSecurityEventAsync` methods; no update or deletion logic is exposed, ensuring audit trail immutability.
6. **[INFERENCE] In-Memory Test Warning Configuration**: Test environment DbContext configures `InMemoryEventId.TransactionIgnoredWarning` suppression to enable testing atomic multi-entity conversion workflows without requiring external SQL transaction coordinators.

