# AcxiomCRM - REST API & OpenAPI Documentation

AcxiomCRM provides a secured, role-aware REST API compliant with OpenAPI 3.0 / Swagger standards. All endpoints strictly utilize Data Transfer Objects (DTOs), preventing entity leakage and mass assignment vulnerabilities.

## Base URL & Interactive Documentation
- **API Base Route**: `/api`
- **Swagger UI**: `/swagger` (or `/swagger/index.html`)
- **OpenAPI JSON**: `/swagger/v1/swagger.json`

## Authentication & Rate Limiting
- **Authentication**: ASP.NET Core Identity authentication cookies.
- **Login Rate Limiter**: Configured with a fixed-window rate limiter (`AuthRateLimit`), permitting up to **5 requests per minute** per client.

---

## API Endpoints Summary

### 1. Authentication (`/api/auth`)
| Method | Endpoint | Description | Request Body | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | Authenticates user credentials & issues session cookie. | `LoginRequestDto` | `200 OK`, `400 Bad Request`, `429 Too Many Requests` |
| `POST` | `/api/auth/logout` | Terminates session cookie & logs audit event. | None | `200 OK` |

### 2. Customers (`/api/customers`)
| Method | Endpoint | Description | Request Body / Query | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/customers` | Retrieves scoped list of customers with pagination & search. | Query: `search`, `page`, `pageSize` | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/customers/{id}` | Retrieves a customer by primary key (scope checked). | None | `200 OK`, `401 Unauthorized`, `404 Not Found` |
| `POST` | `/api/customers` | Creates a new customer with duplicate email/phone check. | `CreateCustomerDto` | `201 Created`, `400 Bad Request`, `409 Conflict` |
| `PUT` | `/api/customers/{id}` | Updates existing customer (scope checked). | `UpdateCustomerDto` | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `DELETE` | `/api/customers/{id}` | Soft-deactivates customer record & logs audit trail. | None | `200 OK`, `400 Bad Request`, `404 Not Found` |

### 3. Leads (`/api/leads`)
| Method | Endpoint | Description | Request Body / Query | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/leads` | Retrieves scoped list of leads. | Query: `search`, `page`, `pageSize` | `200 OK`, `401 Unauthorized` |
| `POST` | `/api/leads` | Creates a new lead with status & validation checks. | `CreateLeadDto` | `201 Created`, `400 Bad Request` |

### 4. Opportunities (`/api/opportunities`)
| Method | Endpoint | Description | Request Body / Query | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/opportunities` | Retrieves scoped list of opportunities. | Query: `search`, `page`, `pageSize` | `200 OK`, `401 Unauthorized` |
| `POST` | `/api/opportunities` | Creates an opportunity (Amount > 0, Probability 0-100, close date). | `CreateOpportunityDto` | `201 Created`, `400 Bad Request` |

### 5. Follow-ups (`/api/followups`)
| Method | Endpoint | Description | Request Body / Query | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/followups` | Retrieves scoped list of scheduled/pending follow-ups. | Query: `status`, `page`, `pageSize` | `200 OK`, `401 Unauthorized` |
| `POST` | `/api/followups` | Schedules a follow-up (date cannot be in the past). | `CreateFollowUpDto` | `201 Created`, `400 Bad Request` |

### 6. Reports (`/api/reports`)
| Method | Endpoint | Description | Request Body / Query | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/reports/pipeline` | Retrieves stage-wise and owner-wise pipeline summary. | None | `200 OK`, `401 Unauthorized`, `403 Forbidden` |

---

## Data Transfer Objects (DTOs) Schema

### CustomerDto
```json
{
  "customerId": 1,
  "customerCode": "CUST-00001",
  "customerName": "Tata Consultancy Services",
  "email": "contact@tcs.com",
  "phone": "9876543210",
  "companyName": "Tata Group",
  "address": "Bandra Kurla Complex",
  "city": "Mumbai",
  "state": "Maharashtra",
  "status": "Active",
  "notes": "Key enterprise account",
  "ownerId": "user-guid",
  "ownerName": "Sales Executive 1",
  "createdDate": "2026-10-07T12:00:00Z"
}
```

### CreateCustomerDto
```json
{
  "customerName": "Infosys Technologies",
  "email": "partners@infosys.com",
  "phone": "9812345678",
  "companyName": "Infosys",
  "address": "Electronics City",
  "city": "Bengaluru",
  "state": "Karnataka",
  "notes": "Prospective cloud client",
  "ownerId": "optional-user-id"
}
```

### Standard Error Response (RFC 7807 ProblemDetails)
```json
{
  "type": "about:blank",
  "title": "A customer with this email address already exists.",
  "status": 409,
  "detail": null,
  "instance": null
}
```
