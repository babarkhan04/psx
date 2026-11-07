# SmartPOS Reports UI and Analytics Patch

Apply this to the project that already has the earlier backend/Blazor fixes and product-pricing patch.

## Install

1. Stop SmartPOS and back up the existing files listed in the manifest.
2. Extract this ZIP into the project root containing SmartPOS.slnx and src. Overwrite the existing files. The ZIP has no extra parent directory.
3. Rebuild, restart, and refresh the browser so the new scoped styles load.

```sh
dotnet build SmartPOS.slnx -c Release
dotnet test tests/SmartPOS.Tests/SmartPOS.Tests.csproj -c Release
```

No database migration, configuration update, or new NuGet package is needed. The patch contains only report-related application files and regression tests. Product creation and opening-stock files are not included.

## Reports workspace

- New overview with sales/profit chart, payment chart, summary cards, best-selling products, and links to stock and refund reports.
- 26 destinations including the overview, grouped by Sales, Products, Inventory, Operations, Purchasing & Tax, and Customers.
- Searchable report navigation on desktop and a searchable report selector on smaller screens.
- One focused table at a time, with sorting, column filtering, paging, resizing, row counts, and empty states.
- Today, 7-day, 30-day, this-month, and last-month shortcuts.
- Store and date edits are applied together. Until Apply is clicked, displayed results and export use the previous applied filters.
- Refresh failures preserve the last loaded results and allow retrying.

## Added reports

| Report | Contents |
| --- | --- |
| Sales register | Invoice, store, cashier, customer, payment methods, status, discounts, tax, invoice total, period refunds and net sales. |
| Purchase register | Received GRNs, supplier, store, receipt date, quantities, tax and totals. Draft/cancelled purchases are excluded. |
| Stock ledger | Individual stock movements, quantities in/out, balance after, reference and notes, including variant identifiers. |
| Slow-moving stock | Current stock with five or fewer gross units sold during the selected period, current value and estimated stock cover. |
| Weekday performance | Monday–Sunday totals and average sales per calendar occurrence, including days with no sales. |

Additional focused views expose brand performance, register performance, and low/out-of-stock lines. Existing report data and the protected Excel-export endpoint are retained.

## Data and export behavior

- Transaction dates, times, daily/hourly groups and date shortcuts use UTC explicitly. The To date is inclusive; the backend receives midnight after that day as its exclusive upper bound.
- Stock and valuation are current snapshots at current catalog costs. Variant-specific costs are used when present. Date filters affect transaction activity, not the current snapshot.
- Slow-moving stock counts gross units from non-voided sales separately for each store, product and variant. Returned quantities do not reduce this sales count. A dash for stock-cover days means no sales were recorded in the period; it does not represent a zero-day stock forecast.
- The sales register lists invoices completed in the selected period. Its refunds column includes period refunds for those invoices. Refunds for older invoices still appear in Returns and the overview; consequently the sales-register sum may differ from overview net sales.
- Register receipts and payment mix are before refunds, as labeled. Returns are reported separately.
- Customer sales follow the chosen store/date filters. Customer balances are current company-wide balances. The existing top-100 customer limit is labeled in the UI.
- Product profitability now includes all matching products rather than the former 50-row limit. Renaming a sold product no longer causes its totals to be counted twice.
- Excel contains 25 sheets, including the new reports and all product-profit rows. The workbook follows applied store/date filters; individual table column filters remain local to that table.
- Existing permissions, tenant filtering and accessible-store restrictions continue to apply to the new report queries and exports.

## Verification

- .NET 10 Release solution build passed with zero errors.
- 52 automated tests passed, including six new report cases covering refunds, weekday averages, variant valuation/slow stock, store access/export isolation, date boundaries, received purchases, full product export and renamed products.
- 40 browser checks passed: all 26 destinations, report search, applied export scope, date presets, failed refresh recovery, desktop/mobile overflow, empty states, single-day charts and no browser runtime errors.
- Browser checks used synthetic fixtures; database regression tests used SQLite. A live SQL Server deployment was not exercised.

## Acceptance check

1. Open Reports and check overview charts against a known period.
2. Change the store/date fields: confirm results and export scope change only after Apply. Try the date shortcuts.
3. Search for and open each new report. Check received purchases, stock references and variant rows against known records.
4. Download the workbook and compare its new sheets with the applied on-screen filters.
5. Check a narrow browser window: use the report selector and scroll wide tables horizontally.
