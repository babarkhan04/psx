# SmartPOS backend and Blazor fixes

Prepared against `smartpos_code_20261023_214326.zip` on 23 October 2026.

This ZIP contains replacement files and new support/test files, arranged relative to the project root. It is a patch, not a complete copy of the solution.

## Apply the patch

1. Close the running SmartPOS application.
2. Extract the ZIP contents into the directory containing `SmartPOS.slnx`. Allow the matching files under `src` and `tests` to be replaced. Do not place the ZIP inside an extra project subfolder.
3. Restore NuGet packages and build the solution in Visual Studio, or run these commands from the project root:

```powershell
dotnet restore SmartPOS.slnx
dotnet build SmartPOS.slnx -c Release
dotnet test tests/SmartPOS.Tests/SmartPOS.Tests.csproj -c Release
```

4. Restart SmartPOS and sign in with your existing account. Run the DEV checks below before using the patched transaction workflows with live sales.

The patch introduces no database schema changes and needs no manual SQL script. Existing users and data are retained. The connection strings match the uploaded project. If you changed local configuration after that export, preserve those changes when merging the two appsettings files.

## Discounts

Line and invoice discounts now apply directly without a manager ID, email, or password prompt. The discount amount/percentage and reason remain available. Reasons must contain between three and 300 characters.

Both supplied appsettings files use:

```json
"PosSecurity": {
  "RequireDiscountManagerApproval": false,
  "ApprovalExpiryMinutes": 15
}
```

To enable manager approval later, change `RequireDiscountManagerApproval` to `true` in both files and restart. Approvals are checked by the backend, expire, and can be used only once for the matching user, store, action, reference, and amount. Previously parked discounted sales need their discounts re-applied to obtain fresh approvals when that setting is enabled.

Voiding completed sales retains its existing manager approval dialog. Void approval is now validated and consumed by the backend.

## Included fixes

- Checkout validates positive quantities, active stores/registers/products/variants, product-to-store assignment, authoritative store/variant prices, tax rates, and discount/payment values. Stale prices are rejected rather than silently charging a different amount.
- Product names and SKUs are taken from the catalog. Scanned variant SKUs match the checkout records.
- Credit-limit checks run inside the transaction. A zero customer credit limit follows the existing UI rule: cash-only, with no udhaar.
- Returns account for invoice discounts, preserve rounding across partial returns, reject duplicate sale lines, and recheck remaining quantities and refund values inside the transaction.
- Mixed cash/credit returns reverse unpaid credit separately from the amount settled via Cash/Card/Other. The Blazor return receipt shows the breakdown.
- Store access is checked in the receipt service, including calls from the Blazor invoice URL. Sales and inventory queries restrict all-store views to assigned stores. Deactivated users and deleted store assignments are denied.
- Product edits preserve prices and POS visibility for stores the user cannot access. SKU/barcode duplicate checks remain within the active company.
- Product creation saves the product, prices, assignments, barcodes, and variants atomically. Failed operations clear tracked changes so a later save cannot persist an incomplete transaction.
- Stock, credit, and drawer mutations use consistent serializable transactions. Opening stock cannot be reposted after an item already has stock history.
- Drawer balances sum actual movements, including movements sharing a timestamp.
- Dashboard totals include return events and invoice discounts. Report discount totals no longer count line discounts twice; profit calculations include returned stock costs and exclude tax.
- Demo seeding is disabled by default. Hardcoded fallback passwords and the old commented password-reset snippet are removed. Explicit passwords are required if seeding is deliberately enabled. Existing database account passwords are not changed by this patch.
- Authentication runs before antiforgery middleware. Store-access failures return a forbidden response instead of an HTTP 500 response at API endpoints.
- The vulnerable PKCS dependency is pinned to 10.0.13. Test-only SQLite dependencies are also updated.

The React frontend changes discussed earlier are deferred. This patch does not replace ClientApp files or add new React screens.

## Verification

- Full solution Release build: passed, zero errors. Existing compiler/package warnings remain.
- Automated regression tests: 42 passed, zero failures, zero skipped.
- NuGet vulnerability check, including transitive packages: no vulnerable packages reported for the resolved solution dependencies at preparation time.
- Patch archive paths and replacement-file contents checked against the validated working copy.

Service regression tests use relational SQLite and a test document-number generator. SQL Server-specific number-sequence locking, retry behavior under concurrent requests, IIS hosting, and physical receipt-printer behavior require verification in your DEV environment.

## DEV checks

1. Sell a Rs 1,000 item with a Rs 100 invoice discount. Total should be Rs 900, and applying the discount should not open a credential dialog. Also check a line discount.
2. Fully return that sale. The refund must be Rs 900 and the restocked quantity must match the returned quantity. Try three partial returns of a three-unit discounted sale; their amounts must add up to the original paid value.
3. Complete a sale using Rs 400 Cash and Rs 600 Credit. A full return should reverse Rs 600 of unpaid customer credit and settle Rs 400 via the selected refund method. Verify the customer ledger, drawer ledger, and return receipt.
4. Attempt to sell an inactive or store-hidden product, an invalid variant, zero/negative quantity, or more stock than is available. Verify that the sale and stock movement are not saved. Perform another valid operation afterwards to verify no failed transaction leaks into that save.
5. Change a store price while a cart is open. Checkout should reject the stale price. Refresh the POS and re-add the item at the current price.
6. Use a store-limited account to edit a shared product. Another store's price and POS visibility must remain unchanged. Try opening an invoice belonging to an unassigned store; its data must not be displayed.
7. Void a completed sale. Verify that missing, expired, reused, or mismatched approval IDs are rejected and a valid approval is consumed with the void.
8. From two browser sessions, submit returns against the same remaining sale quantity and sales against the last available stock. Verify that committed quantities/refunds do not exceed what is available and that a rejected transaction leaves no extra ledger rows.
9. Check dashboard/report sales, discounts, profit, customer balances, and drawer balances before and after a discounted sale, partial return, full return, and void.
10. Deplete an item that already has stock history, then try posting opening stock again. It should be rejected; use a stock adjustment instead.

`SMARTPOS_PATCH_FILES.txt` lists every replacement and new source file in the patch.

Security advisory references: [Microsoft PKCS advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx) and [related Microsoft advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7).
