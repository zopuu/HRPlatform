# HRPlatform

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet\&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql\&logoColor=white)

ASP.NET Core Web API for managing **Skills** and **Candidates** (with candidate–skill assignments).

The app applies **EF Core migrations** and **seed data** automatically on startup so you can run and test quickly.

---

## Features

* CRUD for **Skills** and **Candidates**
* Assign/remove skills for a candidate
* Search candidates by name, filter by **skill IDs** (`any` / `all`), sort & paginate
* RFC 7807 **ProblemDetails** error responses (404/409/500)
* **Swagger UI** out of the box

---

## Tech

* Target Framework: **.NET 8.0**
* EF Core + PostgreSQL
* xUnit + FluentAssertions (unit tests)


---

## Prerequisites

* **.NET SDK 8** (or newer)
* **PostgreSQL 14+** available locally (native install)

---

## Quick Start

### 1) Clone & restore

```bash
git clone https://github.com/zopuu/HRPlatform
cd HRPlatform
dotnet restore
```

### 2) Start PostgreSQL

* Create a database, e.g. `hr_platform`.
* Keep user/password handy.

### 3) Configure connection string

The app reads `DefaultConnection` from the configuration.
Edit `appsettings.json` so it corresponds with your connection parameters:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=hrplatform;Username=yourusername;Password=yourpassword"
  }
}
```
### 4) Run the API (migrations + seed run automatically)

## CLI:
```bash
dotnet run
```

You should see something like:

```
Now listening on: http://localhost:5120
```

Open **Swagger UI**: `http://localhost:<port>/swagger`


## Visual Studio:
* Open the solution in VS.
* Make sure that the `http` profile is selected
* Press F5.
* On the first run, the app applies migrations and seeds data.
* Swager UI should open. If not, go to http://localhost:5120/swagger/index.html 
---

## Seed data

The seeder is **idempotent** (safe to run multiple times). On first run, it ensures:

**Skills**: `C#`, `Java`, `SQL`, `JavaScript`, `React`, `ASP.NET Core`, `Docker`, `PostgreSQL`

**Sample candidates** (with some skills assigned):

* Mirko Poledica — `mirkop@example.com`
* Ana Petrović — `ana@example.com`
* Marko Marković — `marko@example.com`

---

## API Overview

### Health

`GET /api/health` → `{ "status": "ok" }`

### Skills

* `GET /api/Skills`
* `GET /api/Skills/{id}`
* `POST /api/Skills`

  ```json
  { "name": "Python" }
  ```
* `PUT /api/Skills/{id}`

  ```json
  { "name": "Advanced SQL" }
  ```
* `DELETE /api/Skills/{id}`

> Duplicate names return **409 Conflict**.

### Candidates

* `GET /api/Candidates`

  * Query parameters:

    * `name` (string) – filter by full name (case-insensitive)
    * `skills` skills – repeatable integer IDs: `?skills=1&skills=2`
    * `match` (enum) – `any` (default) or `all`
    * `page` (int, default `1`), `pageSize` (int, default `20`, 1–100)
    * `sortBy` (enum) – `name|dob|email|phone` (default `name`)
    * `dir` (enum) – `asc|desc` (default `asc`)
  * Response headers: `X-Total-Count` (total items for given filter)

* `GET /api/Candidates/{id}`

* `POST /api/Candidates`

  ```json
  {
    "fullName": "Ana Petrović",
    "dateOfBirth": "1997-12-01",
    "email": "ana@example.com",
    "phone": "+38160123456",
    "skillIds": [1, 3]
  }
  ```

* `PUT /api/Candidates/{id}`

  ```json
  {
    "fullName": "Ana P.",
    "dateOfBirth": "1997-12-01",
    "email": "ana@example.com",
    "phone": "+38160123456"
  }
  ```

* `DELETE /api/Candidates/{id}`

* Assign / remove skills:

  * `POST /api/Candidates/{candidateId}/skills`

    ```json
    { "skillIds": [1, 5, 8] }
    ```
  * `DELETE /api/Candidates/{candidateId}/skills/{skillId}`

---

## Running Tests

Visual Studio: Test Explorer → Run All

CLI:
```bash
dotnet test
```

---
