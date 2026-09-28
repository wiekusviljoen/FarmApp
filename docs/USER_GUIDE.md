# FarmApp — User Guide

FarmApp is a farm management web application for livestock records, farm calculations, and weather/fire information.

## 1. Start the application

1. Open `Farm App.slnx` in Visual Studio.
2. Set **Farm App.AppHost** (or the main **Farm App** project) as the startup project, depending on your setup.
3. Run the project.
4. Open the local URL shown in the terminal or browser. The current development URL is `http://127.0.0.1:5188`.

> The application uses a local SQLite database. Back it up before making manual changes.

## 2. Dashboard

The dashboard is the starting point. Use it to get an overview and navigate to the main tools.

## 3. Livestock register

1. Open the livestock register from the navigation.
2. Review the existing animal records.
3. Choose the add/new action and enter the requested animal details.
4. Open a record to review or update its information.
5. Use edit/delete actions carefully because they change your farm records.

**Tip:** Keep animal identifiers consistent and update records after births, purchases, sales, or deaths.

## 4. Market calculator

The live Feedmaster price table groups its tabs into three farm-friendly categories:

- **Sheep** — Feedmaster's **Mutton** tab, including sheep/lamb grade entries.
- **Beef** — Feedmaster's **Beef** tab, with grades and the listed price sources.
- **Wild** — Feedmaster's **Game** tab, with game species and average/minimum/maximum prices.

The **Auctions** tab appears as its own **Auctions** category and is not mixed with meat grades. The table shows the latest available date for each grade/animal and source; a zero or unavailable quote is omitted. Auction prices are excluded from the meat-price trend chart because the pricing basis may differ.

1. Open the market calculator and use **Refresh prices** for the latest Feedmaster meat and auction figures.
2. Review the category, grade/animal, source, date, and published price.
3. Use **Use price** to copy a displayed quote into the abattoir comparison, then enter the carcass weight.
4. Treat the result as an estimate; confirm actual buyer prices, grading, deductions, and fees before a sale.

### Feed product and feeding-cost calculator

- Use **Refresh products** to pull the current product names and package sizes from Feedmaster's public product catalog.
- Choose a product, enter the current supplier price for its bag, and change the bag size only if your actual package differs (the displayed catalog mass is the default).
- The current bag price is saved in this browser/device for that product and the last-updated time is shown. It is not a supplier-provided live price: Feedmaster does not publish bag prices on its public catalog.
- Add the selected product and amount to the mix to calculate ingredient cost.
- In **Feeding duration estimator**, enter animal count, daily feed per animal, and a duration in days, weeks, or 30-day months. The calculator estimates total feed, whole bags required, and cost by kg and by full bags.
- Confirm current prices and appropriate rations with your supplier or animal nutrition adviser before purchasing or feeding.

Calculations are estimates. Confirm buyer prices, grading, deductions, and fees before a sale.

## 5. Feed-mix calculator

1. Open the feed-mix calculator.
2. Enter ingredient quantities and any other requested inputs.
3. Review the calculated mix and totals.
4. Verify the formulation with a qualified nutritionist or feed specialist before feeding livestock.

## 6. Weather and fire conditions

1. Navigate to the weather/fire page.
2. Review forecast conditions and the precipitation visualization.
3. Use the information as one input for farm planning.

Forecasts can change and are not a substitute for official warnings or on-farm observations.

## 7. Data and troubleshooting

- **Records are missing:** Check that the app is using the intended local database and environment.
- **The app will not start:** Check the startup project and run output for errors.
- **Before updates:** Back up the SQLite database and keep secrets out of source control.
- **Need help?** Open a GitHub issue with clear reproduction steps. Do not include passwords, secrets, or private farm data.

## Screenshots

Screenshots of the live application will be added once captured from the running app. This guide intentionally avoids mock screenshots that could misrepresent the interface.
