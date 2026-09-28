# FarmApp

A farm management web application built with ASP.NET Core MVC and SQLite.

## What it does

- **Livestock register** — create and maintain animal records.
- **Market calculator** — view live Feedmaster meat and auction categories (Sheep/Mutton, Beef, Wild/Game, Auctions) and compare prices.
- **Feed-mix & feeding planner** — refresh the Feedmaster product catalog, save locally updated bag prices, build ingredient mixes, and estimate feed quantities and cost over a chosen feeding duration.
- **Weather & fire conditions** — view forecast information and precipitation visualization.
- **Responsive interface** — designed for desktop, tablet, and mobile.

## Get started

1. Open `Farm App.slnx` in Visual Studio.
2. Run the app (the current development setup uses `http://127.0.0.1:5188`).
3. Use the dashboard to open the livestock register and calculators.

The app uses a local SQLite database. Back it up before making manual changes.

## Learn how to use it

**[Open the step-by-step User Guide](docs/USER_GUIDE.md)** for instructions covering the dashboard, livestock register, market calculator, feed-mix calculator, and weather/fire page.

> Screenshots of the live app are still to be captured; this documentation does not use fabricated UI images.

## Technology

- ASP.NET Core MVC / .NET 10
- Entity Framework Core
- SQLite

## Repository

https://github.com/wiekusviljoen/FarmApp
