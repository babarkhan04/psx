# SmartPOS administrator stock adjustment shortcut

Apply this after the earlier backend/Blazor, product-pricing and Reports patches.

## Install

1. Stop SmartPOS and back up existing files listed in the manifest.
2. Extract this ZIP directly into the project root containing SmartPOS.slnx and src. Overwrite the existing files. New files use the same root-relative directory structure.
3. Rebuild and restart the application, then hard-refresh the POS browser tab (Ctrl+F5) to load the updated keyboard module and styles.

```sh
dotnet build SmartPOS.slnx -c Release
dotnet test tests/SmartPOS.Tests/SmartPOS.Tests.csproj -c Release
```

No database migration, new package, or configuration change is needed.

## Behavior

- An out-of-stock product tile now offers Adjust stock to the project's built-in Super Admin and Business Owner roles when the logged-in account also has catalog.inventory.manage permission.
- Clicking opens a focused Stock adjustment dialog inside POS. The current store and selected product are prefilled and fixed. Variant identity is preserved when supplied by POS.
- Enter a positive quantity to add, optionally a reference and notes, then click Post adjustment. Clicking the tile alone does not change inventory.
- The existing inventory service records an Adjustment ledger entry. This flow preserves its store-access checks, permission checks, quantity validation and transaction behavior.
- After successful posting, the POS product list refreshes using the current filters. The existing cart remains in place. Click the restocked tile again to add it to the sale.
- Cancel leaves inventory unchanged. An unsuccessful post keeps the dialog open with the error and allows correction/retry.
- Out-of-stock quick buttons use the same shortcut. In-stock tiles and quick buttons continue adding products to the sale.
- Cashier and Store Manager accounts retain disabled out-of-stock tiles in POS. Store Manager's existing inventory management page remains available according to its existing permissions.
- Only the existing built-in Super Admin and Business Owner role names receive this shortcut. A custom role named Admin or Administrator is not automatically granted it.
- Barcode scanning retains its existing out-of-stock warning behavior.
- POS keyboard shortcuts pause while a Radzen dialog is open and resume when it closes, keeping stock entry separate from billing commands.
- The shortcut does not ask for manager credentials; access follows the logged-in account's roles and inventory permission.

## Files

- src/SmartPOS.Domain/Authorization/PosInventoryAccess.cs (new)
- src/SmartPOS.Web/Components/Pages/Pos.razor
- src/SmartPOS.Web/Components/Pages/Pos.razor.css (new)
- src/SmartPOS.Web/Components/Shared/PosStockAdjustmentDialog.razor (new)
- src/SmartPOS.Web/Components/Shared/PosStockAdjustmentDialog.razor.css (new)
- src/SmartPOS.Web/wwwroot/pos-keyboard.js
- tests/SmartPOS.Tests/PosInventoryAccessTests.cs (new)
- tests/SmartPOS.Tests/PosStockAdjustmentRegressionTests.cs (new)

The existing generic inventory adjustment dialog and product/opening-stock forms are not included in this patch.

## Verification

- .NET 10 Release solution build passed with zero errors.
- 66 automated tests passed, including administrator permission rules, recorded stock movements, checkout after restocking, variant/store isolation, denied cashier changes and inaccessible-store rejection.
- 32 browser checks passed, including both administrator roles, blocked non-administrators, prefilled context, required quantity and precision validation, posting once, stock refresh, cart preservation, Cancel, quick buttons, variants, failed posts, mobile dialog layout and permission rechecks before posting.
- Browser checks used synthetic fixtures; database tests used SQLite. A live SQL Server deployment was not exercised.

## Quick acceptance check

1. Sign in as Super Admin or Business Owner with inventory permission. Add an in-stock item to the POS cart.
2. Click a zero-stock product. Confirm its name/SKU and the POS store in Stock adjustment.
3. Enter the quantity actually being added and post. Confirm the product's stock updates and the existing cart is unchanged.
4. Click the now-stocked tile to add it to the sale. Check Inventory movements for the Adjustment entry.
5. Verify Cancel does not change stock and a Cashier account still cannot click zero-stock tiles.
