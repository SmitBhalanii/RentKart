# RentKart — Rent Anything You Need

RentKart is a local, multi-vendor equipment and item rental marketplace that connects customers with nearby rental businesses. Customers can discover products, check multi-unit availability across date ranges, place multi-item bookings across vendors, collect equipment at physical store locations, return it after use, and leave verified reviews.

---

## Technical Stack & Architecture

- **Platform:** ASP.NET Core MVC on .NET 10.0 (`net10.0`)
- **Persistence:** Entity Framework Core with Microsoft SQL Server (`MSSQL$SQLEXPRESS` / `RentKartDb`)
- **Authentication & Security:** ASP.NET Core Identity with role-based authorization (`Customer`, `Business`, `Admin`)
- **Front-End:** Server-rendered Razor Views with custom modern CSS design tokens and responsive flexbox layouts

### Solution Structure (`RentKart.sln`)

- `src/RentKart.Core`: Domain layer containing entities, value objects, enums, domain interfaces, and business exceptions. Free of external framework dependencies.
- `src/RentKart.Infrastructure`: Data access, Entity Framework Core `ApplicationDbContext`, entity configurations, migrations, and service implementations. Depends on `RentKart.Core`.
- `src/RentKart.Web`: ASP.NET Core MVC presentation layer, Razor views, controllers, ViewModels, and dependency injection composition root. Depends on `RentKart.Core` and `RentKart.Infrastructure`.
- `tests/RentKart.Tests`: Automated unit and integration test suite (xUnit). References `RentKart.Core`, `RentKart.Infrastructure`, and `RentKart.Web`.

---

## Prerequisites

1. **.NET SDK:** .NET 10.0 SDK (`10.0.401` or later).
2. **Database Engine:** Microsoft SQL Server 2025 Express (`localhost\SQLEXPRESS`) or SQL Server LocalDB.
3. **Database Tooling:** `dotnet-ef` global tool and `sqlcmd`.

---

## Getting Started

### 1. Clone & Restore
```bash
git clone https://github.com/SmitBhalanii/RentKart.git
cd RentKart
dotnet restore
```

### 2. Build the Solution
```bash
dotnet build --no-incremental
```

### 3. Run Automated Tests
```bash
dotnet test
```

### 4. Run the Web Application
```bash
dotnet run --project src/RentKart.Web
```
Open `https://localhost:5001` or the assigned HTTP port in your browser.

---

## Security & Secrets Management

- Connection strings in `appsettings.Development.json` use local Windows Integrated Authentication (`Trusted_Connection=True;TrustServerCertificate=True`).
- Secrets and environment-specific credentials must **never** be checked into version control. Use .NET User Secrets (`dotnet user-secrets`) or uncommitted `appsettings.*.local.json` files (ignored in `.gitignore`).

---

## Roadmap

- **Phase 0:** Requirements, Architecture & Blueprint Gate `[Completed]`
- **Phase 1:** Environment & Repository Foundation `[Current]`
- **Phase 2:** Global UI Foundation & Design Tokens
- **Phase 3:** Database Foundation & EF Core Schema
- **Phase 4:** Authentication, Identity & Role Authorization
- **Phase 5:** Business Storefront & Inventory Management
- **Phase 6:** Catalog Discovery, Search & Multi-Store Cart
- **Phase 7:** Availability Engine & Multi-Vendor Booking Lifecycle
- **Phase 8:** Store Pickup Handover, Return Inspection & Reviews
- **Phase 9:** Admin Platform Governance & Audit
- **Phase 10:** End-to-End Hardening & Launch Acceptance
