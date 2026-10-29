# Job Board System (.NET 10 + SQL Server)

An internship-level backend RESTful API built with **ASP.NET Core Web API**, **C#**, **Entity Framework Core**, and **SQL Server (LocalDB)**. The system enables employers to manage and publish job listings, while allowing candidates to browse active positions and submit applications with built-in validation, status tracking, and duplicate prevention.

---

## 1. Project Overview & Architectural Highlights

- **Clean Layered Architecture**: Pragmatic separation of `Controllers`, `Services`, `Data`, `Models`, `DTOs`, and `Middleware` without unnecessary over-engineering or boilerplate repository abstractions.
- **Strongly Typed Domain**: Enums for `JobStatus`, `ApplicationStatus`, `EmploymentType`, and `WorkArrangement` are serialized as strings across JSON APIs and validated strictly against arbitrary numbers or invalid values.
- **Relational Integrity**: Configured foreign key constraints (`Restrict` delete behavior) and a composite unique index on `(JobId, NormalizedEmail)` to prevent duplicate applications at both the application and database levels.
- **Application History Preservation**: Jobs with active applications are soft-deleted by transitioning their status to `Archived`, ensuring no applicant data or historical records are orphaned.
- **Centralized Exception Handling**: Custom domain exceptions (`NotFoundException`, `ConflictException`, `ValidationException`) are captured by `ExceptionHandlingMiddleware` and mapped to RFC 7807 **ProblemDetails** and **ValidationProblemDetails** responses.

---

## 2. Technologies & Prerequisites

- **SDK**: .NET SDK 10.0 (Target Framework: `net10.0`)
- **Database**: Microsoft SQL Server / SQL Server LocalDB (`(localdb)\mssqllocaldb`)
- **ORM**: Entity Framework Core 10.0 (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Sqlite`)
- **Documentation**: Swagger / OpenAPI (`Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.OpenApi`)
- **Testing**: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Data.Sqlite`

---

## 3. Project Structure

```text
Job Board System/
├── JobBoardSystem.sln
├── README.md
├── .gitignore
├── dotnet-tools.json
├── src/
│   └── JobBoard.Api/
│       ├── Controllers/
│       │   ├── JobsController.cs
│       │   └── ApplicationsController.cs
│       ├── Data/
│       │   ├── JobBoardDbContext.cs
│       │   └── Migrations/
│       │       └── 20261028131841_InitialCreate.cs
│       ├── DTOs/
│       │   ├── Common/
│       │   │   ├── PagedResultDto.cs
│       │   │   └── PaginationQueryDto.cs
│       │   ├── Jobs/
│       │   │   ├── JobCreateDto.cs
│       │   │   ├── JobUpdateDto.cs
│       │   │   ├── JobResponseDto.cs
│       │   │   └── JobFilterDto.cs
│       │   └── Applications/
│       │       ├── ApplicationCreateDto.cs
│       │       ├── ApplicationResponseDto.cs
│       │       └── ApplicationStatusUpdateDto.cs
│       ├── Exceptions/
│       │   ├── NotFoundException.cs
│       │   ├── ConflictException.cs
│       │   └── ValidationException.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Models/
│       │   ├── Job.cs
│       │   ├── Application.cs
│       │   └── Enums/
│       │       ├── JobStatus.cs
│       │       ├── ApplicationStatus.cs
│       │       ├── EmploymentType.cs
│       │       └── WorkArrangement.cs
│       ├── Services/
│       │   ├── IJobService.cs
│       │   ├── JobService.cs
│       │   ├── IApplicationService.cs
│       │   └── ApplicationService.cs
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Program.cs
│       ├── appsettings.json
│       └── appsettings.Development.json
└── tests/
    └── JobBoard.Tests/
        ├── UnitTests/
        │   ├── ValidationTests.cs
        │   ├── JobServiceTests.cs
        │   └── ApplicationServiceTests.cs
        └── IntegrationTests/
            ├── CustomWebApplicationFactory.cs
            ├── JobEndpointsTests.cs
            ├── ApplicationEndpointsTests.cs
            └── SqlServerDatabaseTests.cs
```

---

## 4. Database Setup & Migrations

### Connection String Configuration
- In development (`appsettings.Development.json`), the application uses LocalDB:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=JobBoardDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
  ```
- In production (`appsettings.json`), use environment variables or secret placeholders:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=JobBoardDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
  ```

### Managing Migrations with dotnet-ef
```powershell
# Restore local tools (includes dotnet-ef)
dotnet tool restore

# Add a new migration (if domain models change)
dotnet ef migrations add <MigrationName> --project src/JobBoard.Api/JobBoard.Api.csproj --output-dir Data/Migrations

# Apply migrations to the database
dotnet ef database update --project src/JobBoard.Api/JobBoard.Api.csproj
```

---

## 5. Running the API & Swagger Documentation

### Start the API Server
```powershell
dotnet run --project src/JobBoard.Api/JobBoard.Api.csproj --launch-profile http
```
The API will listen at:
- Base HTTP URL: `http://localhost:5054`
- Swagger UI: `http://localhost:5054/swagger`
- OpenAPI JSON Spec: `http://localhost:5054/swagger/v1/swagger.json`

---

## 6. Endpoints Reference

| Method | Endpoint | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/jobs` | Paginated search of published, unexpired jobs with filters (search, location, type, arrangement, salary, sorting). | 200, 400 |
| `GET` | `/api/jobs/{id}` | Retrieve details of a single published, unexpired job. | 200, 404 |
| `POST` | `/api/jobs` | Create a new job listing with initial `Draft` status. | 201, 400 |
| `PUT` | `/api/jobs/{id}` | Update an existing job. Rejects updates if job is `Closed` or `Archived`. | 200, 400, 404, 409 |
| `DELETE` | `/api/jobs/{id}` | Delete a job. If applications exist, soft-deletes to `Archived` to preserve history. | 204, 404 |
| `PATCH` | `/api/jobs/{id}/publish` | Transition a draft job to `Published`. Validates completeness and future deadline. | 200, 404, 409 |
| `PATCH` | `/api/jobs/{id}/close` | Close an active published job, preventing further applications. | 200, 404, 409 |
| `POST` | `/api/jobs/{jobId}/applications` | Submit an application for an active published job. Validates duplicate email. | 201, 400, 404, 409 |
| `GET` | `/api/jobs/{jobId}/applications` | Paginated list of applications submitted for a job. | 200, 404 |
| `GET` | `/api/applications/{id}` | Retrieve details of a specific application. | 200, 404 |
| `PATCH` | `/api/applications/{id}/status` | Update candidate status (`UnderReview`, `Shortlisted`, `Rejected`, `Hired`). | 200, 400, 404, 409 |
| `DELETE` | `/api/applications/{id}` | Soft-withdraw an application (transitions status to `Withdrawn`). | 200, 404, 409 |

---

## 7. Business Rules & Workflow State Machines

### A. Job Status Transition Rules
```text
[ Draft ] ──── (publish) ───► [ Published ] ──── (close) ───► [ Closed ]
    │                              │
    └──────── (delete) ────────────┴───── (delete with apps) ─► [ Archived ]
```
- **Creation**: All jobs start in `Draft` status.
- **Publishing (`PATCH /api/jobs/{id}/publish`)**:
  - Allowed only when status is `Draft`.
  - Rejects if `ApplicationDeadlineUtc` has passed (must be in the future).
  - Rejects if required fields are incomplete.
- **Closing (`PATCH /api/jobs/{id}/close`)**:
  - Allowed only when status is `Published`.
  - Prevents any subsequent candidate applications.
- **Updating (`PUT /api/jobs/{id}`)**:
  - Allowed on `Draft` and `Published` jobs.
  - Prohibited on `Closed` or `Archived` jobs (`409 Conflict`).
- **Deletion (`DELETE /api/jobs/{id}`)**:
  - If 0 applications exist: The job record is cleanly removed.
  - If 1+ applications exist: The job is soft-deleted to `Archived` to protect relational integrity and candidate application records.

### B. Application Status Transition Rules
```text
[ Submitted ] ────► [ UnderReview ] ────► [ Shortlisted ] ────► [ Hired ] (Terminal)
      │                   │                     │
      ▼                   ▼                     ▼
 [ Rejected ] (Terminal) <──────────────────────┘

 [ Withdrawn ] (Terminal - triggered via DELETE /api/applications/{id})
```
- **Allowed Transitions**:
  - `Submitted` $\rightarrow$ `UnderReview`, `Rejected`
  - `UnderReview` $\rightarrow$ `Shortlisted`, `Rejected`
  - `Shortlisted` $\rightarrow$ `Hired`, `Rejected`, `UnderReview`
- **Terminal States**: `Hired`, `Rejected`, and `Withdrawn` are terminal and cannot be transitioned further (`409 Conflict`).
- **Withdrawal (`DELETE /api/applications/{id}`)**:
  - Permitted when status is `Submitted`, `UnderReview`, or `Shortlisted`.
  - Rejects if the application is already `Hired`, `Rejected`, or `Withdrawn` (`409 Conflict`).
  - Sets status to `Withdrawn` and updates `UpdatedAtUtc`, preserving history.

### C. Duplicate Application Prevention & Normalization
1. **Email Normalization**: Incoming emails are trimmed and converted to uppercase invariant format (`normalizedEmail = email.Trim().ToUpperInvariant()`).
2. **Pre-check**: The service checks for existing records with the same `(JobId, NormalizedEmail)`.
3. **Database Unique Constraint**: `IX_Applications_JobId_NormalizedEmail` enforces uniqueness at the database engine level. Concurrent race conditions trigger a database unique violation, caught and converted to a clean `409 Conflict` ProblemDetails response.

---

## 8. Example Requests & Responses

### 1. Create a Job (`POST /api/jobs`)
**Request:**
```http
POST /api/jobs HTTP/1.1
Host: localhost:5054
Content-Type: application/json

{
  "title": "Senior .NET Backend Developer",
  "description": "Looking for an experienced engineer skilled in ASP.NET Core, EF Core, and SQL Server.",
  "companyName": "Digital Solutions Ltd",
  "location": "Cairo, Egypt",
  "employmentType": "FullTime",
  "workArrangement": "Hybrid",
  "minimumSalary": 35000,
  "maximumSalary": 50000,
  "currencyCode": "EGP",
  "applicationDeadlineUtc": "2026-12-31T23:59:59Z"
}
```

**Response (`201 Created`):**
```json
{
  "id": 1,
  "title": "Senior .NET Backend Developer",
  "description": "Looking for an experienced engineer skilled in ASP.NET Core, EF Core, and SQL Server.",
  "companyName": "Digital Solutions Ltd",
  "location": "Cairo, Egypt",
  "employmentType": "FullTime",
  "workArrangement": "Hybrid",
  "minimumSalary": 35000.00,
  "maximumSalary": 50000.00,
  "currencyCode": "EGP",
  "status": "Draft",
  "createdAtUtc": "2026-10-28T13:20:00Z",
  "updatedAtUtc": "2026-10-28T13:20:00Z",
  "applicationDeadlineUtc": "2026-12-31T23:59:59Z",
  "isAcceptingApplications": false
}
```

### 2. Publish the Job (`PATCH /api/jobs/1/publish`)
**Response (`200 OK`):**
```json
{
  "id": 1,
  "title": "Senior .NET Backend Developer",
  "companyName": "Digital Solutions Ltd",
  "status": "Published",
  "isAcceptingApplications": true
}
```

### 3. Submit an Application (`POST /api/jobs/1/applications`)
**Request:**
```http
POST /api/jobs/1/applications HTTP/1.1
Host: localhost:5054
Content-Type: application/json

{
  "applicantName": "Kareem Hassan",
  "email": "kareem.hassan@example.com",
  "coverLetter": "I have 5 years building scalable web APIs with .NET Core."
}
```

**Response (`201 Created`):**
```json
{
  "id": 1,
  "jobId": 1,
  "applicantName": "Kareem Hassan",
  "email": "kareem.hassan@example.com",
  "coverLetter": "I have 5 years building scalable web APIs with .NET Core.",
  "status": "Submitted",
  "appliedAtUtc": "2026-10-28T13:25:00Z",
  "updatedAtUtc": "2026-10-28T13:25:00Z"
}
```

### 4. Duplicate Application Conflict (`POST /api/jobs/1/applications`)
**Request:**
```http
POST /api/jobs/1/applications HTTP/1.1
Content-Type: application/json

{
  "applicantName": "Kareem Hassan",
  "email": "KAREEM.HASSAN@EXAMPLE.COM"
}
```

**Response (`409 Conflict` ProblemDetails):**
```json
{
  "type": null,
  "title": "Conflict",
  "status": 409,
  "detail": "An application for 'KAREEM.HASSAN@EXAMPLE.COM' has already been submitted for this job.",
  "instance": "/api/jobs/1/applications"
}
```

---

## 9. Running the Automated Tests

The solution contains a complete xUnit test suite featuring unit tests, integration tests via `WebApplicationFactory`, and SQL Server relational constraint tests.

```powershell
# Run all tests in the solution
dotnet test JobBoardSystem.sln
```

### Test Suite Structure
1. **ValidationTests (Unit)**: DataAnnotations rules on DTOs, salary range validation, name/email formatting, and pagination bounding.
2. **JobServiceTests (Unit)**: Lifecycle transitions, draft defaults, deadline verification, salary rules, archiving vs hard-deleting, and public filtering.
3. **ApplicationServiceTests (Unit)**: Application submissions, casing normalization, closed/expired rejection, allowed and forbidden status transitions, and soft withdrawal rules.
4. **JobEndpointsTests (Integration)**: HTTP requests verifying `POST`, `GET`, `PUT`, `DELETE`, `PATCH /publish`, `PATCH /close`, query parameters, and ProblemDetails.
5. **ApplicationEndpointsTests (Integration)**: End-to-end application lifecycle, duplicate 409 responses, pagination, status updates, and history retention.
6. **SqlServerDatabaseTests (Database Integration)**: Direct execution against SQL Server LocalDB to assert that the `IX_Applications_JobId_NormalizedEmail` index triggers a `DbUpdateException`.

---

## 10. Security & Known Limitations

- **Authentication & Authorization**: This portfolio demo focuses on business rules, database schema design, and REST conventions without artificial authentication layers. In a production environment:
  - Employers would authenticate via JWT/OAuth2, and only the employer who owns a job could edit, close, or review its applications.
  - Applicants would authenticate to manage their own applications and withdrawal requests.
- **Resume Files**: File upload and resume parsing are intentionally not implemented for this core data modeling task. An extension point is available by adding a `ResumeStorageKey` or `ResumeBlobUrl` column to the `Application` entity when integrating with blob storage (e.g., Azure Blob Storage or AWS S3).
- **Audit Trails**: Status changes currently update `UpdatedAtUtc`. For complex enterprise requirements, a separate `ApplicationStatusHistory` audit table could record timestamps and user notes for each transition.
