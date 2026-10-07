# AcxiomCRM - Golden Test Set (G001 - G042)

This document records the complete Golden Test Set specified in `docs/ACXIOMCRM_PROMPT.md.md` Section 12, including the test ID, target scenario, implementation test method, and execution verification.

| Test ID | Scenario Description | Implementation Test Method | Test Class | Verification Result |
|---|---|---|---|---|
| **G001** | Customer name blank -> reject | `G001_CustomerNameBlank_RejectsValidation` | `ValidationTests` | **PASS** |
| **G002** | Invalid customer email -> reject | `G002_InvalidCustomerEmail_RejectsValidation` | `ValidationTests` | **PASS** |
| **G003** | Invalid customer phone -> reject | `G003_InvalidCustomerPhone_RejectsValidation` | `ValidationTests` | **PASS** |
| **G004** | Duplicate customer email -> reject | `G004_DuplicateCustomerEmail_RejectsCreation` | `CustomerAndApiTests` | **PASS** |
| **G005** | Duplicate customer phone -> reject | `G005_DuplicateCustomerPhone_RejectsCreation` | `CustomerAndApiTests` | **PASS** |
| **G006** | Opportunity Amount = 0 -> reject | `G006_OpportunityAmountZero_RejectsValidation` | `ValidationTests` | **PASS** |
| **G007** | Opportunity Amount < 0 -> reject | `G007_OpportunityAmountNegative_RejectsValidation` | `ValidationTests` | **PASS** |
| **G008** | Opportunity Probability = 101 -> reject | `G008_OpportunityProbabilityAbove100_RejectsValidation` | `ValidationTests` | **PASS** |
| **G009** | Opportunity Probability = -1 -> reject | `G009_OpportunityProbabilityNegative_RejectsValidation` | `ValidationTests` | **PASS** |
| **G010** | Active Opportunity Close Date yesterday -> reject | `G010_ActiveOpportunityCloseDateYesterday_RejectsValidation` | `ValidationTests` | **PASS** |
| **G011** | Follow-up date yesterday -> reject | `G011_FollowUpYesterday_RejectsValidation` | `ValidationTests` | **PASS** |
| **G012** | Invalid lead status -> reject | `G012_InvalidLeadStatus_RejectsValidation` | `ValidationTests` | **PASS** |
| **G013** | Invalid lead status transition -> reject | `G013_InvalidLeadTransition_RejectsValidation` | `ValidationTests` | **PASS** |
| **G014** | SalesExecutive reads another SE's customer -> deny | `G014_SalesExecutiveReadsAnotherSalesExecutivesCustomer_DeniesAccess` | `ScopeAndDashboardTests` | **PASS** |
| **G015** | SalesExecutive edits another SE's opportunity -> deny | `G015_SalesExecutiveEditsAnotherSalesExecutivesOpportunity_DeniesAccess` | `WorkflowAndAuditingTests` | **PASS** |
| **G016** | Unauthenticated CRM page access -> block/redirect | `G016_CrmControllersDecoratedWithAuthorizeAttribute` | `SecurityIdentityAuditTests` | **PASS** |
| **G017** | Tampered API request -> server denies | `G017_G029_ApiControllersDecoratedWithAuthorizeAttribute` | `ScopeAndDashboardTests` | **PASS** |
| **G018** | Repeated failed logins -> lockout | `G018_RepeatedFailedLogins_EnforcesLockout` | `SecurityIdentityAuditTests` | **PASS** |
| **G019** | Password in any API response -> FAIL (verified absent) | `G019_NoPasswordPropertiesInApiDtos` | `SecurityIdentityAuditTests` | **PASS** |
| **G020** | Password in logs -> FAIL (verified sanitized) | `G020_AuditServiceStripsPasswordsAndSecretsFromLogs` | `SecurityIdentityAuditTests` | **PASS** |
| **G021** | Create customer -> audit event generated | `G021_CreateCustomer_GeneratesAuditEntry` | `CustomerAndApiTests` | **PASS** |
| **G022** | Update customer -> audit event generated | `G022_UpdateCustomer_GeneratesAuditEntry` | `CustomerAndApiTests` | **PASS** |
| **G023** | Deactivate customer -> audit event generated | `G023_DeactivateCustomer_GeneratesAuditEntry` | `CustomerAndApiTests` | **PASS** |
| **G024** | Role change -> audit event generated | `G024_RoleChange_AuditsEvent` | `SecurityIdentityAuditTests` | **PASS** |
| **G025** | Lead conversion -> audit event generated | `G025_LeadConversion_CreatesCustomerOpportunityAndAuditLog` | `WorkflowAndAuditingTests` | **PASS** |
| **G026** | Follow-up completion -> audit event generated | `G026_FollowUpComplete_GeneratesAuditLog` | `WorkflowAndAuditingTests` | **PASS** |
| **G027** | Follow-up reschedule -> audit event generated | `G027_FollowUpReschedule_GeneratesAuditLog` | `WorkflowAndAuditingTests` | **PASS** |
| **G028** | Authorized GET /api/customers -> DTO JSON | `G028_AuthorizedGetApiCustomers_ReturnsDtoJson` | `CustomerAndApiTests` | **PASS** |
| **G029** | Unauthorized API call -> 401/403 | `G017_G029_ApiControllersDecoratedWithAuthorizeAttribute` | `ScopeAndDashboardTests` | **PASS** |
| **G030** | Dashboard as SalesExecutive -> own data only | `G030_DashboardAsSalesExecutive_ShowsOwnDataOnly` | `ScopeAndDashboardTests` | **PASS** |
| **G031** | Dashboard as Manager -> team data | `G031_DashboardAsManager_ShowsTeamData` | `ScopeAndDashboardTests` | **PASS** |
| **G032** | Dashboard as Admin -> all data | `G032_DashboardAsAdmin_ShowsAllData` | `ScopeAndDashboardTests` | **PASS** |
| **G033** | Password below policy -> reject | `G033_PasswordBelowPolicy_RejectsPassword` | `SecurityIdentityAuditTests` | **PASS** |
| **G034** | POST without anti-forgery token -> reject | `G034_AllMvcStateChangingPostActions_HaveValidateAntiForgeryToken` | `SecurityIdentityAuditTests` | **PASS** |
| **G035** | SQL-injection string in search -> no error, no extra rows | `G035_SqlInjectionStringInSearch_NoExtraRowsOrCrash` | `CustomerAndApiTests` | **PASS** |
| **G036** | Admin unlock of locked user -> works + audit | `G036_AdminUnlock_UnlocksUserAndGeneratesAudit` | `SecurityIdentityAuditTests` | **PASS** |
| **G037** | Manager reads out-of-team record -> deny | `G037_ManagerReadsOutOfTeamRecord_DeniesAccess` | `ScopeAndDashboardTests` | **PASS** |
| **G038** | API GET unknown ID -> 404 NotFound | `G038_ApiGetUnknownId_Returns404NotFound` | `CustomerAndApiTests` | **PASS** |
| **G039** | API duplicate customer -> 409 Conflict | `G039_ApiDuplicateCustomer_Returns409Conflict` | `CustomerAndApiTests` | **PASS** |
| **G040** | Lead email/phone/ExpectedValue invalid -> reject | `G040_LeadInvalidFields_RejectsValidation` | `ValidationTests` | **PASS** |
| **G041** | Register cannot create Admin | `G041_RegisterCannotCreateAdmin` | `SecurityIdentityAuditTests` | **PASS** |
| **G042** | Audit records cannot be edited or deleted | `G042_AuditLogServiceAndRepository_AreAppendOnly` | `SecurityIdentityAuditTests` | **PASS** |

### Execution Summary
- **Total Tests**: 51 test executions across all 42 Golden Test criteria.
- **Passed**: 51
- **Failed**: 0
- **Duration**: ~1 second
- **Test Command**: `dotnet test --nologo --logger "console;verbosity=minimal"`
