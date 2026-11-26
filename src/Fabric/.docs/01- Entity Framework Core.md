# Stage 1: Entity Framework Core

Welcome to the interactive Getting Started guide! This document will walk you through the Entity Framework Core architecture in this Bit.Fabric project.

---

## Overview

Entity Framework Core (EF Core) is the primary data access technology used in this project. It provides an object-relational mapping (ORM) layer that allows you to work with databases using .NET objects, eliminating much of the data access code you'd normally need to write.

In this stage, you'll learn about:
- **AppDbContext**: The central database context for server-side data access
- **Entity Models**: How domain entities are defined
- **Nullable Reference Types**: Understanding nullability in entity properties
- **Entity Type Configurations**: Best practices for configuring entity mappings
- **Migrations**: How to manage database schema changes

---

## 1. AppDbContext - The Heart of Data Access

### Location
The main database context is located at:
[`/src/Server/Bit.Fabric.Server.Api/Infrastructure/Data/AppDbContext.cs`](/src/Server/Bit.Fabric.Server.Api/Infrastructure/Data/AppDbContext.cs)

### What is AppDbContext?

`AppDbContext` is the central class that coordinates Entity Framework functionality for your data model. It inherits from `IdentityDbContext`, which provides built-in support for ASP.NET Core Identity (users, roles, authentication).

### Key Features

Here's the `AppDbContext` structure from the actual project:

```csharp
public partial class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
{
    // DbSets represent tables in the database
    public virtual DbSet<Category> Categories { get; set; } = default!;
    public virtual DbSet<Product> Products { get; set; } = default!;
    public virtual DbSet<TodoItem> TodoItems { get; set; } = default!;
    // ... and other DbSets

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```
---

## 2. Entity Models - Defining Your Domain

### Location
Entity models are organized by domain in:
[`/src/Server/Bit.Fabric.Server.Api/Features/`](/src/Server/Bit.Fabric.Server.Api/Features/)

The folder structure is:
```
Features/
├── Categories/
│   ├── Category.cs
│   └── CategoryConfiguration.cs
├── Products/
│   ├── Product.cs
│   └── ProductConfiguration.cs
├── Todo/
│   ├── TodoItem.cs
│   └── TodoConfiguration.cs
├── Identity/
│   ├── Models/
│   │   ├── User.cs, Role.cs, etc.
│   └── Configurations/
│       ├── UserConfiguration.cs, RoleConfiguration.cs, etc.
└── ... other domains
```

### Example: Category Entity

Let's examine the `Category` entity from the project:

**File:** [`/src/Server/Bit.Fabric.Server.Api/Features/Categories/Category.cs`](/src/Server/Bit.Fabric.Server.Api/Features/Categories/Category.cs)

```csharp
using Bit.Fabric.Server.Api.Features.Products;

namespace Bit.Fabric.Server.Api.Features.Categories;

public partial class Category
{
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public string? Name { get; set; }

    public string? Color { get; set; }

    public long Version { get; set; }

    public IList<Product> Products { get; set; } = [];
}
```

### **Version** Concurrency Stamp
```csharp
public long Version { get; set; }
```
- **Critical for optimistic concurrency control**
- Configured as a **row version** in SQL Server
- Automatically prevents lost updates when multiple users edit the same record
- You **must** include this in all entities that will be updated

---

### Understanding Nullable Reference Types

This project uses C# nullable reference types to improve code safety. Understanding nullability patterns in entity properties is crucial:

#### String Properties with [Required]

```csharp
[Required, MaxLength(64)]
public string? Name { get; set; }
```

**Why is it `string?` (nullable) despite the `[Required]` attribute?**

- EF Core sets properties to `null` **before validation occurs**
- The `[Required]` attribute is a **validation rule**, not a nullability constraint
- During entity instantiation, the property starts as `null`
- Only after validation does EF Core enforce the requirement
- Using `string?` prevents compiler warnings while maintaining validation

#### Navigation Property Patterns

The project follows specific nullability patterns for relationships:

##### The "One" Side (Single Entity Reference)

```csharp
[ForeignKey(nameof(CategoryId))]
public Category? Category { get; set; }

public Guid CategoryId { get; set; }
```

**Pattern: Nullable with `?`**

- **Why nullable?** The related entity might not be loaded from the database
- EF Core does **not** automatically load related entities
- Example: When you query `Products`, the `Category` property is `null` unless you explicitly include it:
  ```csharp
  // Category will be null
  var product = await dbContext.Products.FirstAsync();
  
  // Category will be loaded
  var product = await dbContext.Products.Include(p => p.Category).FirstAsync();
  ```

##### The "Many" Side (Collection Navigation Property)

```csharp
public IList<Product> Products { get; set; } = [];
```

**Pattern: Non-nullable and initialized with `= []`**

- **Why non-nullable?** Collections should never be `null` to prevent `NullReferenceException`
- **Why `= []`?** Ensures the collection is always initialized, even if no products exist
- You can safely call `.Add()`, `.Count`, etc., without null checks
- Example:
  ```csharp
  var category = new Category();
  category.Products.Add(new Product()); // ✅ Safe - no null check needed
  ```

#### Nullable Reference Types Summary Table

| Property Type | Nullable? | Example | Reason |
|--------------|-----------|---------|--------|
| **String with [Required]** | ✅ Yes (`string?`) | `public string? Name { get; set; }` | EF Core sets to `null` initially before validation |
| **"One" Side Navigation** | ✅ Yes (`Category?`) | `public Category? Category { get; set; }` | Related entity might not be loaded from database |
| **"Many" Side Collection** | ❌ No (initialized with `= []`) | `public IList<Product> Products { get; set; } = []` | Prevents null reference exceptions; safe to iterate |
| **Value Type (non-nullable)** | ❌ No | `public Guid Id { get; set; }` | Value types have default values (e.g., `Guid.Empty`) |
| **Value Type (nullable)** | ✅ Yes (`DateTimeOffset?`) | `public DateTimeOffset? BirthDate { get; set; }` | Explicitly allows null for optional values |

---

## 3. Entity Type Configurations - The Professional Approach

### Location
Entity configurations are colocated with their entities in:
[`/src/Server/Bit.Fabric.Server.Api/Features/`](/src/Server/Bit.Fabric.Server.Api/Features/)

For the Identity domain, configurations are organized in a dedicated Configurations folder:
```
Features/
├── Categories/
│   ├── Category.cs
│   └── CategoryConfiguration.cs
├── Products/
│   ├── Product.cs
│   └── ProductConfiguration.cs
├── Identity/
│   ├── Models/
│   │   ├── User.cs, Role.cs, etc.
│   └── Configurations/
│       ├── UserConfiguration.cs, RoleConfiguration.cs, etc.
└── ... other features
```

### Example: CategoryConfiguration

**File:** [`/src/Server/Bit.Fabric.Server.Api/Features/Categories/CategoryConfiguration.cs`](/src/Server/Bit.Fabric.Server.Api/Features/Categories/CategoryConfiguration.cs)

```csharp
using Bit.Fabric.Server.Api.Features.Categories;

namespace Bit.Fabric.Server.Api.Features.Categories;

public partial class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // Configure unique index on Name
        builder.HasIndex(p => p.Name).IsUnique();

        // Seed initial data
        var defaultVersion = 1;
        builder.HasData(
            new () { 
                Id = Guid.Parse("31d78bd0-0b4f-4e87-b02f-8f66d4ab2845"), 
                Name = "Ford", 
                Color = "#FFCD56", 
                Version = defaultVersion 
            },
            new () { 
                Id = Guid.Parse("582b8c19-0709-4dae-b7a6-fa0e704dad3c"), 
                Name = "Nissan", 
                Color = "#FF6384", 
                Version = defaultVersion 
            }
        );
    }
}
```

### Understanding the Configuration

#### 1. **Unique Indexes**
```csharp
builder.HasIndex(p => p.Name).IsUnique();
```
- Creates a unique index on the `Name` column
- Ensures no two categories can have the same name
- Database will enforce this constraint at the SQL level

#### 2. **Seed Data with HasData()**
```csharp
builder.HasData(
    new Category { Id = Guid.Parse("..."), Name = "Ford", ... }
);
```
Pre-populates the database with initial data, data is inserted when the database is created

### How Configurations Are Applied

In `AppDbContext.OnModelCreating()`:
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
```

This single line:
- **Scans** the entire assembly for classes implementing `IEntityTypeConfiguration<T>`
- **Automatically applies** all configurations
- You don't need to manually register each configuration class

---

## 4. Migrations

The project uses EF Core migrations from day one. In the Development environment, the app applies them when it starts:

**File:** [`/src/Server/Bit.Fabric.Server.Api/Program.cs`](/src/Server/Bit.Fabric.Server.Api/Program.cs)
```csharp
await using var scope = app.Services.CreateAsyncScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await dbContext.Database.MigrateAsync();
```

[`/src/Tests/Infrastructure/TestsAssemblyInitializer.cs`](/src/Tests/Infrastructure/TestsAssemblyInitializer.cs) does the same for the tests.

While you develop, you do **NOT** need to run `dotnet ef database update` or `Update-Database` manually: the `MigrateAsync()` call above handles it. It only runs in the Development environment, though, so a deployed app never changes its own database. See [Applying Migrations to a Deployed Database](#applying-migrations-to-a-deployed-database) below.

### The Initial Migration

`bit new` adds the `Initial` migration to `/src/Server/Bit.Fabric.Server.Api/Infrastructure/Data/Migrations/` when it creates the project. A project created any other way starts without one, and `MigrateAsync()` refuses to run until it exists. Run, in any folder of the project:

```bash
bit migration add Initial
```

`bit migration` runs EF Core's `dotnet ef` with the `dotnet-ef` version that matches the project's EF Core packages, formats the new migration with `dotnet format`, and when it fails, shows its errors and the command to run it again. Install the bit CLI with `dotnet tool install --global Bit.Cli --version 11.0.0-pre-25`, or run it without installing it: `dnx Bit.Cli --version 11.0.0-pre-25 -- migration add Initial`.

### Adding Future Migrations

When you modify entities or configurations, create a new migration; the app applies it on its next start:

```bash
bit migration add <MigrationName>
```

`bit migration remove` removes the last migration, as long as the development database doesn't have it yet.

If the model changes without a migration, `MigrateAsync()` stops the app with a "pending model changes" error until the migration is added.

### Applying Migrations to a Deployed Database

The CD pipeline applies them. Before each deployment starts the new version, a one-shot `migrations` container runs the api's own image with `MigrateDatabaseAndExit=true` on the server, against the database that deployment uses. A deployment that fails before any new container starts runs the `Down` of the migrations it applied. Once one has started it may have taken requests, so a failure from then on leaves the running version on the newer schema, which each migration has to keep working for it, and undoing the migrations to a person. Deploying an older commit, or another branch to Test, runs the `Down` of the applied migrations it doesn't have, with the image that was running, back to the last migration both know. EF Core runs the `Down` of the migrations after the one it goes back to and of none before it, in the order of their ids, which is the order they were added in and not always the one they reached `main` in. So a migration that only one of the two images has before the one they go back to stays applied, for a deployment that failed as for an older commit, and the deployment names it in a warning: on Production when two branches reach `main` in another order than they added their migrations, and on Test also when two branches each add one and then merge a newer `main`. `DeploymentMigrations` has an example. [Stage 16](/.docs/16-%20CI-CD%20Pipeline%20and%20Environments.md) describes the deployment. To apply them by hand, use a migrations bundle as described below.

A migrations bundle is one executable that holds the migrations. Build it with the `--runtime` of the machine it will run on (`linux-x64` when you leave it out):

```bash
bit migration bundle --runtime linux-x64
```

Copy the `efbundle` file it creates in the `Bit.Fabric.Server.Api` project directory (`efbundle.exe` for a Windows runtime) into the deployed app's folder, where it finds the app's settings and certificate, and run it there before the new version starts. When the app's settings don't hold the production connection string, give it to the bundle in the `ConnectionStrings__<name>` environment variable the app reads it from.

---

---

### AI Wiki

Ask your own question [here](https://bitplatform.dev/ask)

---
