# URL Shortener Backend (ASP.NET Core Web API & SQL Server)

A clean, robust, and beginner-friendly URL Shortener backend service built with **ASP.NET Core Web API**, **Entity Framework Core**, and **Microsoft SQL Server**.

---

## 1. Project Purpose & Features

This project provides a reliable RESTful API to shorten long destination URLs, redirect short links to their original locations, and query metadata about previously shortened links.

### Key Capabilities
- **Feature 1: Create a Short URL (`POST /api/urls`)**
  - Accepts a JSON payload with a `longUrl`.
  - Validates URL syntax and ensures strict `http` or `https` protocol schemes.
  - Generates a cryptographically secure Base62 random short code (`0-9`, `a-z`, `A-Z`).
  - Enforces database uniqueness on the short code.
  - Returns `HTTP 201 Created` with original URL, short code, and full short URL.
- **Feature 2: Redirect to Original URL (`GET /{shortCode}`)**
  - Looks up the original URL for a given short code.
  - Returns an `HTTP 302 Found` redirect header (`Location`).
  - Returns `HTTP 404 Not Found` if the code does not exist.
  - Does not accept arbitrary open redirect destinations.
  - Avoids conflicting with `/swagger` and `/api/*` routes.
- **Feature 3: Retrieve URL Information (`GET /api/urls/{shortCode}`)**
  - Retrieves the metadata (ID, short code, original URL, short URL, and UTC creation timestamp).
  - Returns `HTTP 200 OK` or `HTTP 404 Not Found`.

---

## 2. Technology Stack & Prerequisites

- **Language**: C# (.NET 10 / .NET SDK compatible)
- **Web Framework**: ASP.NET Core Web API
- **ORM**: Entity Framework Core 10
- **Database**: Microsoft SQL Server / LocalDB (SSMS compatible)
- **Documentation**: Swagger / OpenAPI (`Swashbuckle.AspNetCore`)
- **Testing**: xUnit & `Microsoft.AspNetCore.Mvc.Testing`

### Prerequisites
1. **.NET SDK**: Run `dotnet --version` in your terminal to ensure you have .NET SDK installed (e.g., .NET 10 / .NET 8).
2. **SQL Server**:
   - **Option A (Recommended for local dev)**: SQL Server LocalDB (`(localdb)\mssqllocaldb`), included automatically with Visual Studio.
   - **Option B**: Full SQL Server instance (`localhost` or `.`), managed via SQL Server Management Studio (SSMS).
3. **Tools (Optional)**:
   - Visual Studio 2022 (version 17.10+ recommended) or VS Code with C# Dev Kit.
   - SQL Server Management Studio (SSMS) or Azure Data Studio.

---

## 3. Project Structure

```text
UrlShortener/
├── UrlShortener.sln                       # Classic solution file
├── UrlShortener.slnx                      # Modern solution file
├── dotnet-tools.json                      # Local tool manifest (dotnet-ef)
├── README.md                              # Comprehensive guide & reference
│
├── UrlShortener.Api/                      # ASP.NET Core Web API Project
│   ├── Controllers/
│   │   └── UrlsController.cs              # REST API endpoints & route handling
│   ├── Data/
│   │   └── ApplicationDbContext.cs        # EF Core database context & constraints
│   ├── DTOs/
│   │   ├── CreateShortUrlRequest.cs       # Input validation model
│   │   └── ShortUrlResponse.cs            # Output response model
│   ├── Migrations/                        # EF Core migration files
│   │   └── 20261028115841_InitialCreate.cs
│   ├── Models/
│   │   └── Url.cs                         # Url database entity model
│   ├── Properties/
│   │   └── launchSettings.json            # Development port settings
│   ├── Services/
│   │   ├── IUrlShortenerService.cs        # Service contract
│   │   └── UrlShortenerService.cs         # Cryptographic generator & business logic
│   ├── appsettings.json                   # Production / fallback configuration
│   ├── appsettings.Development.json       # Development connection string (LocalDB)
│   ├── Program.cs                         # Application entry point & DI configuration
│   └── UrlShortener.Api.csproj            # API project definition
│
└── UrlShortener.Tests/                    # Automated Test Project
    ├── UrlShortenerServiceTests.cs        # Unit tests (validation, uniqueness, generation)
    ├── UrlsApiIntegrationTests.cs         # Integration tests (HTTP endpoints & status codes)
    └── UrlShortener.Tests.csproj          # Test project definition
```

---

## 4. Database Configuration & Migrations

### Connection Strings
The connection string is configured in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=UrlShortenerDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

If you are using a standard SQL Server instance (e.g., standard `MSSQLSERVER` with SSMS), update the connection string in `appsettings.Development.json` or `appsettings.json` to:

```json
"DefaultConnection": "Server=localhost;Database=UrlShortenerDb;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Applying Migrations

Open **Windows PowerShell** in the repository root (`D:\back_end\code_alfa\URL Shortener`):

```powershell
# 1. Restore local tools (includes dotnet-ef)
dotnet tool restore

# 2. Apply EF Core migrations to SQL Server
dotnet ef database update --project UrlShortener.Api\UrlShortener.Api.csproj
```

EF Core will automatically create the `UrlShortenerDb` database and `Urls` table with the unique index on `ShortCode`.

---

## 5. How to Run the Application

### Option A: Using the .NET CLI (PowerShell)

```powershell
# Run the API project
dotnet run --project UrlShortener.Api\UrlShortener.Api.csproj
```

The application will start and listen on the configured addresses (for example: `https://localhost:7001` or `http://localhost:5000`).

### Option B: Using Visual Studio 2022
1. Double-click `UrlShortener.sln` or `UrlShortener.slnx` to open the solution in Visual Studio 2022.
2. In the Solution Explorer, right-click `UrlShortener.Api` and select **Set as Startup Project**.
3. Press **F5** or click the green **Play** button (`https`).
4. Visual Studio will build the solution, start the Web API, and automatically open Swagger in your browser.

---

## 6. Testing Endpoints (Swagger & Postman)

### 1. Interactive Swagger UI
When running in `Development` mode, navigate to:
```
https://localhost:7001/swagger
```
From Swagger UI you can directly invoke:
- `POST /api/urls`
- `GET /api/urls/{shortCode}`
- `GET /{shortCode}`

### 2. Postman / cURL Examples

#### Feature 1: Create a Short URL
- **Method**: `POST`
- **URL**: `https://localhost:7001/api/urls`
- **Headers**: `Content-Type: application/json`
- **Request Body**:
  ```json
  {
    "longUrl": "https://example.com/products/123"
  }
  ```
- **Response (`HTTP 201 Created`)**:
  ```json
  {
    "id": 1,
    "shortCode": "BjqU5Ry",
    "originalUrl": "https://example.com/products/123",
    "shortUrl": "https://localhost:7001/BjqU5Ry",
    "createdAt": "2026-10-28T12:06:06.3736298Z"
  }
  ```

#### Feature 2: Redirect to the Original URL
- **Method**: `GET`
- **URL**: `https://localhost:7001/BjqU5Ry`
- **Response**: `HTTP 302 Found` with header:
  ```http
  Location: https://example.com/products/123
  ```
- Entering `https://localhost:7001/BjqU5Ry` in any web browser will automatically forward you to `https://example.com/products/123`.

#### Feature 3: Query URL Details
- **Method**: `GET`
- **URL**: `https://localhost:7001/api/urls/BjqU5Ry`
- **Response (`HTTP 200 OK`)**:
  ```json
  {
    "id": 1,
    "shortCode": "BjqU5Ry",
    "originalUrl": "https://example.com/products/123",
    "shortUrl": "https://localhost:7001/BjqU5Ry",
    "createdAt": "2026-10-28T12:06:06.3736298Z"
  }
  ```

#### Feature 4: Invalid URL Handling
- **Method**: `POST`
- **URL**: `https://localhost:7001/api/urls`
- **Request Body**:
  ```json
  {
    "longUrl": "not-a-valid-url"
  }
  ```
- **Response (`HTTP 400 Bad Request`)**:
  ```json
  {
    "error": "The provided URL is invalid. It must be a valid absolute URL with an HTTP or HTTPS scheme (e.g., https://example.com)."
  }
  ```

---

## 7. Automated Tests

The test project `UrlShortener.Tests` uses **xUnit** and **Microsoft.AspNetCore.Mvc.Testing**.

### Running Tests in PowerShell

```powershell
dotnet test UrlShortener.sln
```

### Test Coverage Highlights
1. **Valid Short URL Creation**: Verifies that a short URL is created with proper length, valid URL output, and saved to the database.
2. **Rejection of Invalid URLs**: Tests empty strings, whitespaces, malformed strings, and non-HTTP/HTTPS schemes (e.g. `ftp://`, `javascript:`).
3. **Redirect Endpoint (302 Found)**: Tests that `GET /{shortCode}` returns `302 Found` with the exact `Location` header.
4. **404 Not Found Handling**: Tests that queries for nonexistent codes return `404 Not Found`.
5. **URL Details Retrieval**: Tests `GET /api/urls/{shortCode}` returns correct payload matching the created entity.
6. **Code Uniqueness**: Verifies multiple sequential creations yield distinct codes without collision.
7. **HTTP Status Codes**: Verifies `201 Created`, `200 OK`, `302 Found`, `400 Bad Request`, and `404 Not Found` responses.

> [!NOTE]
> **Test Database Provider Isolation**:
> Automated tests execute against an isolated **EF Core InMemory Database**, preventing modifications to your live SQL Server development database. InMemory validates business logic and API routing; the real SQL Server schema and constraints are managed through EF Core migrations against the SQL Server instance.

---

## 8. Common Errors & Troubleshooting

1. **Cannot connect to SQL Server / LocalDB**:
   - Ensure the LocalDB instance is started:
     ```powershell
     sqllocaldb start mssqllocaldb
     ```
   - If using a full SQL Server instance (`MSSQLSERVER`), verify the Windows service is running:
     ```powershell
     Get-Service MSSQLSERVER
     ```
   - If connecting to localhost with SSMS, verify that TCP/IP or Named Pipes is enabled in **SQL Server Configuration Manager**.

2. **Certificate Trust Warning (`TrustServerCertificate`)**:
   - For local development, `TrustServerCertificate=True` is included in the connection string to allow SSL/TLS handshake with self-signed local certificates.

3. **Port Conflicts**:
   - If port `7001` or `5000` is already occupied, modify the application port in `UrlShortener.Api/Properties/launchSettings.json`.
