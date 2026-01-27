# Project Overview

This is a web application for the Pretoria East Air Rifle Club (PEARC). It's a self-contained web application built with .NET Core and Razor Pages. The site provides information about the club, Hunter Field Target (HFT) shooting, upcoming events, membership options, and contact details.

The application is designed as a single-page application (SPA), where the initial page is loaded and subsequent content is rendered dynamically on the client-side using JavaScript.

## Main Technologies

*   **Framework:** .NET 9
*   **Language:** C#
*   **UI:** Razor Pages
*   **Styling:** Tailwind CSS
*   **Database ORM:** Entity Framework Core
*   **Database:** SQLite

## Architecture

The application follows a standard .NET Core Razor Pages structure.

*   `Pages/Index.cshtml`: The main and only page of the application. It's a single-page layout with smooth-scrolling navigation and client-side JavaScript for dynamic content.
*   `Pages/Shared/_Layout.cshtml`: The root layout for the application.
*   `Controllers/`: Contains API controllers that provide data to the client-side JavaScript.
*   `Data/`: Contains the Entity Framework Core `DbContext` and data models.
*   `wwwroot/`: Contains static assets like CSS, JavaScript, and images.
*   `app.db`: The SQLite database file.

# Building and Running

## Development

To run the development server:

```bash
dotnet run
```

The application will be available at the URLs specified in the console output (e.g., `http://localhost:5000` and `https://localhost:5001`).

# Development Conventions

*   **Styling:** Utility-first CSS with Tailwind CSS.
*   **Data Fetching:** The main page (`Index.cshtml`) contains client-side JavaScript that fetches data from the application's own API controllers (e.g., `/api/events`).
*   **Database:** The application uses a SQLite database (`app.db`). The database is created and seeded on startup. See `Data/SeedData.cs`.