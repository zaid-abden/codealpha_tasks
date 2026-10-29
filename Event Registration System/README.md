# Event Registration System (.NET 10 Web API + SQL Server)

A robust, enterprise-ready ASP.NET Core Web API for browsing events and managing participant registrations with SQL Server and Entity Framework Core. Built with code-first migrations, data integrity protections, concurrent registration safety, and comprehensive unit and integration test coverage.

---

## 1. Project Overview

The **Event Registration System** allows organizers to create and manage events with enforced capacity limits and allows participants to register for upcoming events.

### Key Highlights
- **Framework**: .NET 10 (`net10.0`) Web API with C# nullable reference types.
- **ORM & Database**: Entity Framework Core 10 with Microsoft SQL Server (LocalDB support out-of-the-box).
- **Concurrency & Race Condition Handling**: Server-level transactions paired with `WITH (UPDLOCK, HOLDLOCK)` query hints during capacity checking to serialize registration requests per event without table-level bottlenecks.
- **Database-Level Data Integrity**: Unique index constraint on `(EventId, NormalizedEmail)` preventing duplicate participant registrations, plus `DeleteBehavior.Restrict` on foreign keys.
- **Centralized Error Handling**: Standard RFC 7807 `ProblemDetails` responses with zero stack trace leakage to clients.
- **Testing**: 40 automated tests (unit + integration tests) via xUnit using isolated in-memory relational testing and HTTP verification via `WebApplicationFactory`.

---

## 2. Solution Structure

```
Event Registration System/
├── EventRegistrationSystem.sln            # Standard Visual Studio solution file
├── EventRegistrationSystem.slnx           # Modern .NET solution format
├── dotnet-tools.json                      # Local dotnet-ef tool manifest
├── README.md                              # Complete system documentation
├── src/
│   └── EventRegistration.Api/            # Main ASP.NET Core Web API Project
│       ├── Controllers/
│       │   ├── EventsController.cs        # Event CRUD & registration endpoints
│       │   └── RegistrationsController.cs # Direct registration inspection & cancellation
│       ├── Data/
│       │   ├── EventDbContext.cs          # EF Core DbContext with model configurations
│       │   └── Migrations/                # EF Core Code-First database migrations
│       ├── Dtos/
│       │   ├── Common/                    # PagedResult<T> and PaginationQuery
│       │   ├── Events/                    # CreateEventRequest, UpdateEventRequest, EventResponse
│       │   └── Registrations/             # CreateRegistrationRequest, RegistrationResponse
│       ├── Exceptions/
│       │   └── AppExceptions.cs           # NotFoundException, ConflictException, ValidationException
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs # Centralized exception handling
│       ├── Models/
│       │   ├── Event.cs                   # Event entity
│       │   └── Registration.cs            # Registration entity
│       ├── Services/
│       │   ├── IEventService.cs           # Event business contract
│       │   ├── EventService.cs            # Event business logic implementation
│       │   ├── IRegistrationService.cs    # Registration contract
│       │   └── RegistrationService.cs     # Registration business logic & concurrency handling
│       ├── Properties/
│       │   └── launchSettings.json        # HTTP (5080) and HTTPS (7080) profiles
│       ├── appsettings.json               # Default configuration template
│       ├── appsettings.Development.json   # LocalDB connection configuration
│       └── Program.cs                     # Application bootstrap & dependency injection
└── tests/
    └── EventRegistration.Tests/          # Automated Test Suite (40 tests)
        ├── UnitTests/
        │   ├── EventServiceTests.cs       # Event business rules & validation tests
        │   └── RegistrationServiceTests.cs# Registration, duplicate, & capacity tests
        └── IntegrationTests/
            ├── CustomWebApplicationFactory.cs # Test host with isolated test database
            ├── EventsApiTests.cs          # End-to-end HTTP tests for /api/events
            └── RegistrationsApiTests.cs   # End-to-end HTTP tests for registrations
```

---

## 3. Prerequisites

- **.NET SDK**: .NET 10.0.402 or higher (`dotnet --version`).
- **SQL Server**:
  - Microsoft SQL Server LocalDB (`(localdb)\MSSQLLocalDB`), OR
  - Any local/remote SQL Server 2019/2022 instance.
- **EF Core CLI Tools**: Included locally in `dotnet-tools.json`.

---

## 4. Database Configuration & Connection Strings

### Local Development (LocalDB)
The project is configured by default to connect to LocalDB in `src/EventRegistration.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=EventRegistrationDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
}
```

### Non-Development / Production Environment
In `src/EventRegistration.Api/appsettings.json`, a safe template placeholder is provided:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=EventRegistrationDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

You can also supply the connection string via an environment variable without modifying configuration files:
```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=EventRegistrationDb;User Id=sa;Password=StrongPassword123!;TrustServerCertificate=True;"
```

---

## 5. Applying Database Migrations

The database schema is managed via EF Core Code-First migrations.

1. Ensure your SQL Server instance is started:
   ```powershell
   sqllocaldb start MSSQLLocalDB
   ```

2. Restore tools and apply migrations to create or update `EventRegistrationDb`:
   ```powershell
   dotnet tool restore
   dotnet ef database update --project src\EventRegistration.Api\EventRegistration.Api.csproj
   ```

To rollback or check migration status:
```powershell
dotnet ef migrations list --project src\EventRegistration.Api\EventRegistration.Api.csproj
```

---

## 6. Running the API & Swagger Documentation

### Starting the API
From the project root:

```powershell
dotnet run --project src\EventRegistration.Api\EventRegistration.Api.csproj --launch-profile "http"
```

The API will listen on:
- **HTTP**: `http://localhost:5080`
- **HTTPS**: `https://localhost:7080` (when using `--launch-profile "https"`)

### Swagger / OpenAPI UI
When running in Development mode, interactive Swagger documentation is available at:
- **Swagger UI**: `http://localhost:5080/swagger` (or `http://localhost:5080/swagger/index.html`)
- **Swagger JSON**: `http://localhost:5080/swagger/v1/swagger.json`

---

## 7. API Endpoints Reference

| Method | Endpoint | Description | Success Code | Error Codes |
|---|---|---|---|---|
| `GET` | `/api/events` | Paginated event list. Supports `search`, `upcomingOnly`, `pageNumber`, `pageSize`. | `200 OK` | `400` |
| `GET` | `/api/events/{id}` | Get event details by ID, including `registeredCount` and `remainingSpots`. | `200 OK` | `404` |
| `POST` | `/api/events` | Create a new event. Validates required fields, positive capacity, and dates (`end > start`). | `201 Created` | `400` |
| `PUT` | `/api/events/{id}` | Update an existing event. Rejects capacity reduction below current registration count. | `200 OK` | `400`, `404`, `409` |
| `DELETE` | `/api/events/{id}` | Delete event. Rejects deletion if active registrations exist to maintain integrity. | `204 No Content` | `404`, `409` |
| `POST` | `/api/events/{eventId}/registrations` | Register a participant. Rejects if started, full, or duplicate email. | `201 Created` | `400`, `404`, `409` |
| `GET` | `/api/events/{eventId}/registrations` | Paginated list of participants registered for the given event. | `200 OK` | `404` |
| `GET` | `/api/registrations/{id}` | Get registration details by ID. | `200 OK` | `404` |
| `DELETE` | `/api/registrations/{id}` | Cancel registration. Rejects cancellation if event has already started. | `204 No Content` | `404`, `409` |

---

## 8. Business Logic & Concurrency Architecture

### 1. Concurrent Registration Safety
A naive `CountAsync()` check followed by an `Add()` is susceptible to race conditions under concurrent load, which could allow total registrations to exceed event capacity.

**Implementation Strategy**:
- When handling registrations with SQL Server, `RegistrationService` executes within a database transaction and acquires an update lock on the target event row using:
  ```sql
  SELECT * FROM Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = @eventId
  ```
- `UPDLOCK` ensures concurrent registration transactions for the *same* event serialize their capacity checks, while registrations for *different* events proceed concurrently without blocking each other.
- `HOLDLOCK` (equivalent to `SERIALIZABLE` range locking on the row) ensures that the lock is maintained until the transaction commits or rolls back.
- After locking the event row, the service validates `currentCount < event.Capacity`.
- If capacity is reached, a `ConflictException` (HTTP 409) is returned before writing.

### 2. Duplicate Registration Prevention
- Emails are normalized via `Trim().ToUpperInvariant()`.
- The database enforces an explicit unique index:
  ```sql
  CREATE UNIQUE INDEX IX_Registrations_EventId_NormalizedEmail ON Registrations (EventId, NormalizedEmail);
  ```
- If a race condition or direct insert bypasses the application layer, the SQL engine catches the violation (error numbers `2601` and `2627`), and EF Core translates this into a clean HTTP 409 Conflict response.

### 3. Timestamp & Timezone Consistency
- All stored dates (`StartDateTime`, `EndDateTime`, `CreatedAt`, `RegisteredAt`) are strictly stored in **UTC**.
- Input timestamps are converted to UTC via `NormalizeToUtc`, preventing unintended server-local timezone drifts.

### 4. Integrity Protection Rules
- **Event Deletion**: If an event has active registrations, `DELETE /api/events/{id}` rejects the deletion with `409 Conflict` ("Cannot delete event because it has active registrations"). Registrations are never silently cascaded.
- **Capacity Reductions**: If an organizer attempts to update an event capacity to a number below the active registration count, `PUT /api/events/{id}` rejects the update with `409 Conflict`.
- **Started Event Protections**:
  - Participants cannot register for events that have already started (`event.StartDateTime <= DateTime.UtcNow`).
  - Participants cannot cancel registrations for events that have already started.

---

## 9. Example HTTP Requests & Responses

### 1. Create an Event
**Request:**
```http
POST /api/events HTTP/1.1
Content-Type: application/json

{
  "title": "Global Tech Summit 2026",
  "description": "Annual developer conference covering cloud and backend systems",
  "location": "Convention Center Hall A, New York",
  "startDateTime": "2026-11-15T09:00:00Z",
  "endDateTime": "2026-11-15T18:00:00Z",
  "capacity": 100
}
```

**Response (`201 Created`):**
```json
{
  "id": 1,
  "title": "Global Tech Summit 2026",
  "description": "Annual developer conference covering cloud and backend systems",
  "location": "Convention Center Hall A, New York",
  "startDateTime": "2026-11-15T09:00:00Z",
  "endDateTime": "2026-11-15T18:00:00Z",
  "capacity": 100,
  "registeredCount": 0,
  "remainingSpots": 100,
  "createdAt": "2026-10-28T12:47:20.123Z"
}
```

---

### 2. Register a Participant
**Request:**
```http
POST /api/events/1/registrations HTTP/1.1
Content-Type: application/json

{
  "participantName": "Jane Doe",
  "email": "jane.doe@example.com"
}
```

**Response (`201 Created`):**
```json
{
  "id": 1,
  "eventId": 1,
  "participantName": "Jane Doe",
  "email": "jane.doe@example.com",
  "registeredAt": "2026-10-28T12:47:20.543Z",
  "eventTitle": "Global Tech Summit 2026"
}
```

---

### 3. Duplicate Registration (`409 Conflict`)
**Request:**
```http
POST /api/events/1/registrations HTTP/1.1
Content-Type: application/json

{
  "participantName": "Jane Doe (Alternate)",
  "email": "JANE.DOE@example.com"
}
```

**Response (`409 Conflict`):**
```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Participant with this email is already registered for this event.",
  "instance": "/api/events/1/registrations"
}
```

---

### 4. Query Events with Pagination & Filter
**Request:**
```http
GET /api/events?upcomingOnly=true&search=Tech&pageNumber=1&pageSize=10 HTTP/1.1
```

**Response (`200 OK`):**
```json
{
  "items": [
    {
      "id": 1,
      "title": "Global Tech Summit 2026",
      "description": "Annual developer conference covering cloud and backend systems",
      "location": "Convention Center Hall A, New York",
      "startDateTime": "2026-11-15T09:00:00Z",
      "endDateTime": "2026-11-15T18:00:00Z",
      "capacity": 100,
      "registeredCount": 1,
      "remainingSpots": 99,
      "createdAt": "2026-10-28T12:47:20.123Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## 10. Running Automated Tests

The solution contains a comprehensive test suite in `tests/EventRegistration.Tests`.

### Test Summary
- **Unit Tests**:
  - `EventServiceTests`: Verifies valid creation, invalid dates (`end <= start`), non-positive capacities, missing events, capacity reduction limits, deletion safety, and upcoming/search filters.
  - `RegistrationServiceTests`: Verifies registration flow, missing events, past event registration rejections, full event capacity enforcement, case-insensitive duplicate email handling, cancellations before/after event start, and concurrency limits.
- **Integration Tests**:
  - `EventsApiTests` & `RegistrationsApiTests`: End-to-end HTTP pipeline tests verifying route binding, status codes (`200`, `201`, `204`, `400`, `404`, `409`), Location headers, and RFC 7807 problem details.

### Execute Tests
Run from the root directory:

```powershell
dotnet test EventRegistrationSystem.sln
```

**Observed Test Results**:
```
Test run for EventRegistration.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 40, Skipped: 0, Total: 40, Duration: 2 s
```

---

## 11. Known Limitations & Learning Scope

- **Authentication & Authorization**: In accordance with the internship assignment specification, this API is an educational demonstration and does not enforce authentication (e.g. JWT / OAuth2) or role-based authorization. In production, administrative routes (`POST/PUT/DELETE /api/events`) should be restricted to authenticated administrators, while registration endpoints should verify participant identity.
- **Distributed Locks**: While SQL Server `WITH (UPDLOCK, HOLDLOCK)` handles concurrency across multiple application server instances connecting to the same SQL Server database, multi-database or microservice architectures would benefit from distributed locks (e.g., Redis Redlock) or message queuing.
