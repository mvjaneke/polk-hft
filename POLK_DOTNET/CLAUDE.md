# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
dotnet build                         # Build the project
dotnet run                           # Run (http://localhost:5276, https://localhost:7291)
dotnet ef migrations add <Name>      # Create a new EF Core migration
dotnet ef database update            # Apply pending migrations
```

No test project exists in this solution.

## Architecture

ASP.NET Core 9.0 Razor Pages application with SQLite (EF Core). The solution file is `POLK_DOTNET.sln`.

**Hybrid page + API pattern:** Razor Pages (`Pages/`) handle UI with server-side rendering, while REST API controllers (`Controllers/`) expose endpoints at `/api/{resource}` for each entity. Both use the same `ApplicationDbContext`.

### Key Layers

- **Data/** — `ApplicationDbContext`, all entity models (Event, EventRegistration, EventParticipant, Member, MembershipApplication, CommitteeMember, Constitution, GalleryImage, MembershipOption), and `SeedData.cs` for initial data
- **Pages/** — Razor Pages with page-behind models. `Admin.cshtml.cs` is the largest file, handling events, committee, gallery, membership options, applications, constitution, and registration management through multiple `OnPost*Async` handlers
- **Controllers/** — 6 REST API controllers for CRUD operations
- **CustomModelBinders/** — `DecimalInvariantModelBinder` for locale-safe decimal parsing (used on TotalAmount)
- **Migrations/** — EF Core migrations (SQLite)
- **Pages/Shared/** — `_Layout.cshtml` (centralized layout with Tailwind CSS via CDN)

### Authentication

Simple session-based password auth (no ASP.NET Core Identity). Admin password is in app configuration/user secrets. Admin pages check `Session.GetString("IsAuthenticated") == "true"`. Session timeout: 30 minutes.

### Database

SQLite database at `app.db`. Migrations auto-apply on startup in `Program.cs`. Seed data is initialized via `SeedData.Initialize()`.

## Important Conventions

- **Do NOT use Alpine.js** for any frontend functionality. All UI must use pure HTML/CSS with Razor Pages server-side rendering and postbacks.
- Razor Pages handler naming: `OnGet[Name]Async` / `OnPost[Name]Async` with `asp-page-handler="HandlerName"` in forms.
- Admin tab navigation uses a query parameter (`activeTab`) to track the active tab.
- Styling uses Tailwind CSS (CDN). Bootstrap is in wwwroot/lib but Tailwind is primary.
- Image uploads go to `wwwroot/img/`.
