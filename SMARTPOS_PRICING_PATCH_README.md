# SmartPOS product pricing and form UX patch

Apply this after SmartPOS_Backend_Blazor_Fixes_20261023.zip, which you have already installed.

## Install

1. Stop SmartPOS and back up the three files listed below if they exist.
2. Extract this ZIP directly into the project root (the folder containing SmartPOS.slnx and src). Overwrite the existing files. There is no extra parent folder inside the ZIP.
3. Rebuild and restart the application. Refresh the browser so the updated component styles load.

```sh
dotnet build SmartPOS.slnx -c Release
dotnet test tests/SmartPOS.Tests/SmartPOS.Tests.csproj -c Release
```

No database migration, configuration change, or new package is required.

## Changes

- Details now contains cost price, default sell price, tax, reorder level, and the primary barcode.
- Every store automatically displays the default sell price unless it has a custom price. Entering the default price once is enough.
- Store prices and POS visibility are together in an expandable section on Details. Every store price stays editable.
- Changing the default updates stores using it and preserves custom prices, including zero.
- Use default restores automatic pricing for one store; Use default for all stores resets all custom prices shown in the form. These changes take effect when you save the product.
- Description and image are optional expandable fields. Tabs are reduced from six to three: Details, Extra barcodes, and Variants.
- Existing POS visibility selections are preserved. New products retain the existing current-store visibility behavior; setting a price does not enable sales in additional stores.
- Saving is disabled while an image upload is in progress.

## Pricing behavior

The existing backend already resolves a store's price to its custom price or the product's default. The form now displays the effective price directly instead of leaving inherited prices blank. Inherited prices continue following the default after saving and reopening. Existing explicit prices remain custom even when they equal the default; choose Use default to restore inheritance. Stores shown remain limited to the user's allowed active stores. Variant-specific prices retain their existing behavior.

Example: default 100 means stores using the default show 100. Set one store to 90, then change the default to 120: the other stores show 120 and the custom store stays at 90.

## Files

- src/SmartPOS.Web/Components/Shared/ProductDialog.razor
- src/SmartPOS.Web/Components/Shared/ProductDialog.razor.css (new scoped stylesheet)
- tests/SmartPOS.Tests/PosRegressionTests.cs

The test file includes the previous regression tests plus four additional pricing cases. Application changes are confined to the Blazor product form and its stylesheet.

## Verification

- .NET 10 Release solution build: passed, zero errors (16 existing warnings).
- Automated tests: 46 passed, zero failures.
- ProductDialog rendering and event callbacks checked for inherited price display, default changes, custom/zero price preservation, editing, clearing/resetting, visibility preservation, empty stores, and the three-tab layout.
- Pricing persistence and checkout behavior tested with SQLite. A live SQL Server deployment and browser layout were not exercised in this environment.

## Quick acceptance check

1. Add a product, enter SKU/name and a default sell price on Details, and expand Store prices & POS visibility. Confirm that every displayed store has that price.
2. Set one store to a custom price. Change the default; confirm the custom price stays intact and other prices follow the default.
3. Save and reopen. Confirm the prices persist. Choose Use default for the custom store, save, and confirm it follows the next default change.
4. Confirm POS uses the saved price in stores where the product is enabled and has stock.
