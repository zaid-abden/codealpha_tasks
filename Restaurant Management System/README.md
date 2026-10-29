# Restaurant Management System (.NET 10 + SQL Server)

An internship-quality RESTful Web API backend for managing restaurant operations including menu items, restaurant tables, reservations with overlap prevention, order lifecycle processing, recipe-based inventory auto-deduction, and sales reporting.

Built with **C#**, **ASP.NET Core Web API**, **Entity Framework Core 10**, and **Microsoft SQL Server (LocalDB)**.

---

## 1. Architecture Overview

The solution follows a clean, maintainable, and pragmatic layered architecture avoiding unnecessary over-engineering:

- **Controllers**: Expose RESTful endpoints adhering to HTTP standards, using DTOs for request/response payloads, and producing standardized RFC 7807 `ProblemDetails` for errors.
- **Services**: Encapsulate all business logic, concurrency control, reservation scheduling rules, order calculations, and recipe-based inventory deductions.
- **Data (EF Core)**: DbContext with fluent configuration enforcing unique constraints, decimal precision for currency and stock quantities, specific delete behaviors, and indexes.
- **Models & Enums**: Strongly typed domain entities and lifecycle status enums (`ReservationStatus`, `OrderType`, `OrderStatus`, `InventoryMovementType`).
- **DTOs**: Segregated contracts with DataAnnotation validation rules for menu, tables, reservations, orders, inventory, and reports.
- **Middleware**: Centralized global exception handler mapping domain exceptions to HTTP 400, 404, 409, and 500 without leaking stack traces.
- **Tests**: Comprehensive xUnit unit and integration test suites covering domain rules, edge cases, concurrent reservation race prevention, and SQL Server relational constraints.

---

## 2. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (Version `10.0.402` or newer)
- Microsoft SQL Server LocalDB (`MSSQLLocalDB`) or SQL Server Express/Enterprise
- `dotnet-ef` CLI tool (installed locally via `dotnet tool restore`)

---

## 3. Database Setup & Migrations

### Connection String Configuration
The development connection string is configured in [`src/RestaurantManagement.Api/appsettings.Development.json`](file:///d:/back_end/code_alfa/Restaurant%20Management%20System/src/RestaurantManagement.Api/appsettings.Development.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RestaurantManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

Production connection strings should be provided via environment variables (`ConnectionStrings__DefaultConnection`).

### Applying Migrations
Restore local tools and apply migrations to the dedicated `RestaurantManagementDb`:

```powershell
# Restore EF Core local CLI tool
dotnet tool restore

# Apply migrations to database
dotnet dotnet-ef database update --project src/RestaurantManagement.Api/RestaurantManagement.Api.csproj
```

---

## 4. How to Run the API

From the root repository directory:

```powershell
dotnet run --project src/RestaurantManagement.Api/RestaurantManagement.Api.csproj
```

The service will start listening on:
- **HTTP**: `http://localhost:5038`
- **HTTPS**: `https://localhost:7112`

### Swagger / OpenAPI UI
Once running, browse to:
```
http://localhost:5038/swagger
```
Interactive OpenAPI documentation with XML schema descriptions is served at `/swagger/v1/swagger.json`.

---

## 5. API Endpoints Reference

| Category | Method | Route | Description | Status Codes |
|---|---|---|---|---|
| **Menu** | `GET` | `/api/menu` | Paginated active menu items (filters: category, keyword, isAvailable) | `200` |
| | `GET` | `/api/menu/{id}` | Get active menu item details | `200`, `404` |
| | `POST` | `/api/menu` | Create menu item with optional recipe ingredients | `201`, `400` |
| | `PUT` | `/api/menu/{id}` | Update menu item details, price, and ingredients | `200`, `400`, `404` |
| | `PATCH` | `/api/menu/{id}/availability` | Toggle item availability | `200`, `404` |
| | `DELETE` | `/api/menu/{id}` | Soft deactivates if historical orders exist | `204`, `404` |
| **Tables** | `GET` | `/api/tables` | List tables (filters: minCapacity, isActive) | `200` |
| | `GET` | `/api/tables/{id}` | Get table details | `200`, `404` |
| | `POST` | `/api/tables` | Create table (unique table number, capacity > 0) | `201`, `400`, `409` |
| | `PUT` | `/api/tables/{id}` | Update table. Prevents deactivation if active reservations exist | `200`, `400`, `404`, `409` |
| **Reservations** | `POST` | `/api/reservations` | Create reservation (auto or requested table assignment) | `201`, `400`, `409` |
| | `GET` | `/api/reservations` | Paginated reservations (filters: date, status, tableId) | `200` |
| | `GET` | `/api/reservations/{id}` | Get reservation details | `200`, `404` |
| | `PATCH` | `/api/reservations/{id}/status` | Transition status (`Pending`, `Confirmed`, `Seated`, `Completed`, `Cancelled`) | `200`, `404`, `409` |
| | `DELETE` | `/api/reservations/{id}` | Soft cancel reservation and release table | `200`, `404`, `409` |
| **Orders** | `POST` | `/api/orders` | Create order. Server snapshots prices and calculates totals | `201`, `400`, `409` |
| | `GET` | `/api/orders` | Paginated orders (filters: fromDate, toDate, status, orderType) | `200` |
| | `GET` | `/api/orders/{id}` | Get order details with line items | `200`, `404` |
| | `POST` | `/api/orders/{id}/items` | Add items to existing `Pending` order; recalculates totals | `200`, `400`, `404`, `409` |
| | `PATCH` | `/api/orders/{id}/status` | Transition status. Moving to `Preparing` deducts recipe inventory | `200`, `404`, `409` |
| **Inventory** | `GET` | `/api/inventory` | Paginated inventory items (filters: keyword, lowStockOnly, isActive) | `200` |
| | `GET` | `/api/inventory/low-stock` | Items where `CurrentQuantity <= ReorderThreshold` | `200` |
| | `GET` | `/api/inventory/{id}` | Get inventory item details | `200`, `404` |
| | `GET` | `/api/inventory/{id}/movements` | Audit trail of inventory movements | `200`, `404` |
| | `POST` | `/api/inventory` | Create inventory item with unique normalized name | `201`, `400`, `409` |
| | `PUT` | `/api/inventory/{id}` | Update metadata without overwriting stock levels | `200`, `400`, `404`, `409` |
| | `POST` | `/api/inventory/{id}/adjustments` | Stock adjustment with audit record. Prevents negative stock | `200`, `400`, `404`, `409` |
| **Reports** | `GET` | `/api/reports/daily-sales` | Daily sales by date (`YYYY-MM-DD`), grouped by order type | `200` |
| | `GET` | `/api/reports/low-stock` | Low-stock items and deficit report | `200` |
| | `GET` | `/api/reports/order-summary` | Aggregated order counts grouped by status | `200` |

---

## 6. Business Rules & Logic

### Reservation Overlap Rules
- Overlap detection formula:
  $$\text{existing.StartAtUtc} < \text{requested.EndAtUtc} \quad \text{and} \quad \text{existing.EndAtUtc} > \text{requested.StartAtUtc}$$
- Intervals are **half-open** $[StartAtUtc, EndAtUtc)$, meaning a reservation ending at `14:00` does not conflict with a reservation starting at `14:00`.
- Cancelled reservations (`Status == Cancelled`) **never block availability**.
- Table deactivation policy: If deactivating a table (`IsActive = false`), the system checks for any upcoming active reservations (`EndAtUtc > DateTime.UtcNow` and `Status in (Pending, Confirmed, Seated)`). If found, the update is rejected with `409 Conflict`.
- Concurrency protection: Executed within transactions to prevent race conditions during concurrent bookings.

### Order Lifecycle & State Machine
```
[Pending] ───────► [Preparing] ───────► [Ready] ───────► [Served] ───────► [Completed]
    │                   │                   │                │
    ▼                   ▼                   ▼                ▼
[Cancelled]         [Cancelled]         [Cancelled]      [Cancelled]
```
- **Source of Truth for Prices**: Database prices are fetched at order creation time and snapshotted into `OrderItem.UnitPrice` and `OrderItem.ItemNameSnapshot`. Client-provided prices are ignored.
- **Totals Calculation**: Server calculates `LineTotal = UnitPrice * Quantity`, `Subtotal = sum(LineTotal)`, and `Total = Subtotal`.
- **Modifications**: Items can only be added to orders while in `Pending` status. Modifying an order in `Preparing` or later states yields `409 Conflict`.
- Invalid transitions (e.g. `Completed -> Cancelled`, `Cancelled -> Preparing`, or skipping steps) return `409 Conflict`.

### Recipe Model & Inventory Auto-Deduction
- Each `MenuItem` can have 0 or more `MenuItemIngredient` entries (`QuantityRequired`).
- **Recipe-less Items Policy**: Menu items without configured recipes are permitted to be ordered; they simply do not trigger automatic stock deductions.
- **Trigger**: Stock is deducted **exactly once** when an order transitions from `Pending` to `Preparing`.
- **Consumption Calculation**:
  $$\text{Total Required} = \sum (\text{Ingredient.QuantityRequired} \times \text{OrderItem.Quantity})$$
- **Atomic Deduction & Rollback**: All ingredients are validated before any stock is deducted. If **any** ingredient has insufficient stock (`CurrentQuantity < required`), the entire status update is rejected with `409 Conflict`, leaving all stock levels and the order unchanged.
- **Idempotency**: Repeated or concurrent requests to set status to `Preparing` check existing audit records to ensure stock is never deducted twice.
- **Cancellation Restock Policy**:
  - If cancelled before preparation (`Pending -> Cancelled`): No inventory was deducted.
  - If cancelled after preparation has begun (`Preparing / Ready / Served -> Cancelled`): Ingredients that have already been prepared/cooked are **not** automatically restocked. Manual adjustments via `/api/inventory/{id}/adjustments` must be used if ingredients were reclaimed.
- **Sign Convention**:
  - Negative values (e.g., `-2.0000`) indicate deductions (such as order preparation).
  - Positive values (e.g., `+10.0000`) indicate additions (such as restocks or initial stock).

### Daily Sales Reporting Rules
- Daily sales strictly query orders with `Status == OrderStatus.Completed` created within the UTC date range.
- **Cancelled orders are strictly excluded from sales revenue**.
- Returns aggregate order count, total sales revenue, and breakdown grouped by `OrderType` (`DineIn` vs `Takeaway`).

---

## 7. Database Entity Relationships

```
+--------------------+            +------------------------+
|  RestaurantTable   | 1        * |      Reservation       |
|--------------------|------------|------------------------|
| Id (PK)            |            | Id (PK)                |
| TableNumber (UQ)   |            | TableId (FK)           |
| Capacity           |            | CustomerName           |
| IsActive           |            | StartAtUtc, EndAtUtc   |
+--------------------+            | Status                 |
         | 1                      +------------------------+
         |
         | *
+--------------------+            +------------------------+
|       Order        | 1        * |       OrderItem        |
|--------------------|------------|------------------------|
| Id (PK)            |            | Id (PK)                |
| OrderNumber (UQ)   |            | OrderId (FK)           |
| TableId (FK, Null) |            | MenuItemId (FK)        |
| OrderType, Status  |            | ItemNameSnapshot       |
| Subtotal, Total    |            | UnitPrice, Quantity    |
+--------------------+            | LineTotal              |
         | 1                      +------------------------+
         |                                     | *
         | *                                   |
+--------------------+                         | 1
| InventoryMovement  |            +------------------------+
|--------------------|            |        MenuItem        |
| Id (PK)            |            |------------------------|
| OrderId (FK, Null) |            | Id (PK)                |
| InventoryItemId(FK)|            | Name, Category, Price  |
| QuantityChange     |            | IsAvailable, IsActive  |
| MovementType       |            +------------------------+
| Reason, CreatedAt  |                         | 1
+--------------------+                         |
         | *                                   | *
         |                        +------------------------+
         | 1                      |   MenuItemIngredient   |
+--------------------+            |------------------------|
|   InventoryItem    | 1        * | Id (PK)                |
|--------------------|------------| MenuItemId (FK)        |
| Id (PK)            |            | InventoryItemId (FK)   |
| Name, Unit         |            | QuantityRequired       |
| NormalizedName (UQ)|            +------------------------+
| CurrentQuantity    |
| ReorderThreshold   |
| RowVersion         |
+--------------------+
```

---

## 8. Running Automated Tests

The test suite contains 46 comprehensive unit and integration tests executing across in-memory and Microsoft SQL Server LocalDB providers:

```powershell
dotnet test
```

### Test Coverage Highlights
- **Menu**: Creation, validation of prices/quantities, filtering, availability toggles, soft-deletion when referenced by historical orders.
- **Tables**: Creation, capacity bounds, duplicate table number prevention, active reservation deactivation locks.
- **Reservations**: Exact overlap checks, adjacent (half-open) reservations, cancelled reservations releasing capacity, auto table assignment choosing best-fitting table, past-time rejection.
- **Orders**: Price snapshotting, total calculation, DineIn table requirement, Takeaway table exemption, item addition to pending orders, invalid transition rejection.
- **Inventory & Recipes**: Multi-ingredient recipe deduction, insufficient stock rollback, idempotency (no duplicate deduction), cancelled orders policy, negative adjustment prevention.
- **Reporting**: Daily sales calculations, cancelled order exclusion, status distribution summaries.
- **SQL Server Relational**: Physical unique constraints, transaction rollback integrity, and foreign key rules tested on LocalDB.

---

## 9. Security & Production Deployment Considerations

> [!WARNING]
> **Authentication & Authorization Notice**:
> This project is designed as an internship/demo reference implementation and intentionally does **not** include authentication or role-based authorization.
>
> In a production deployment:
> 1. Administrative endpoints (menu editing, table configuration, inventory adjustments, and sales reports) must be secured with authentication (e.g. JWT Bearer / OAuth2 / OpenID Connect) and role-based policies (`Admin`, `Manager`).
> 2. Waitstaff/kitchen terminals should operate under restricted role permissions (`Staff`, `Kitchen`).
> 3. Rate limiting and CORS configurations must be tuned for production origins.
> 4. Ensure production connection strings use secure credentials injected via Azure Key Vault, AWS Secrets Manager, or environment variables.
