# SmartPOS optional POS customer profiles, history and birthdays

Apply this patch after the Backend/Blazor, Product Pricing, Reports, and Admin Stock Adjustment patches already supplied. Extract into the directory containing SmartPOS.slnx and src, not into src itself. This ZIP contains only changed/new files. Existing application settings and connection strings are not overwritten.

## Installation order

1. Back up the application source, deployed files and SmartPOS database. Stop the site/app pool and any SmartPOS.Worker service while updating.
2. Extract this ZIP into the project root and overwrite matching files.
3. In SSMS, select the application's actual SmartPOS database and manually run database/customer-engagement/01_CustomerEngagement.sql. The script adds contact/birthday columns, backfills normalized phone keys and creates the birthday email outbox and indexes. It is transactional and can be run again. It does not merge existing customers, send email, or alter existing sales/credit balances. The phone backfill uses Pakistan's 92 country code.
4. Run database/customer-engagement/02_VerifyCustomerEngagement.sql in the same database. Confirm all four customer columns and the three named indexes appear. No database scripts were executed against your database during development.
5. Optionally merge the CustomerEngagement section from database/customer-engagement/appsettings.CustomerEngagement.example.json into your actual configuration. This example is documentation; it is not loaded automatically. Use the existing appsettings.Production.json/environment/secrets mechanism and retain connection strings. Set Database:ApplyMigrationsOnStartup to false if this setting already exists. It now defaults to false. Do not enable automatic EF migrations for this patch: no EF migration or model snapshot update is included; the SQL script is the schema update.
6. Restore, build and test using the commands below. Publish normally to IIS, restart and hard refresh the POS page with Ctrl+F5. Existing sessions should sign out/in if role permissions were changed.

```powershell
dotnet restore SmartPOS.slnx
dotnet build SmartPOS.slnx -c Release
dotnet test SmartPOS.slnx -c Release
# Optional JavaScript keyboard checks (Node.js):
node --test tests/browser/pos-keyboard.customer.test.mjs
```

Production application dependencies are unchanged. The added Blazor component test project uses bUnit 2.13.3 and references SmartPOS.Web. NuGet restores that test dependency when building the full solution.

## POS behavior

- Customer registration is optional. Cash/card walk-in checkout works without opening the form. Credit/udhaar retains its existing requirement for a credit customer.
- Customer details/history is available below the payment buttons. Cash and split-payment dialogs also offer Customer (optional) beside the completion actions.
- F4 opens the customer form from POS or the active payment dialog. In the form it focuses phone/name lookup. Existing custom shortcut settings still apply; F4 is the default. Other POS shortcuts pause in dialogs.
- Lookup accepts mobile number or name. Pakistani formats such as 03001234567, +923001234567 and 00923001234567 resolve to the same key after the SQL backfill. Shared numbers show a selection list.
- New customer profiles require full name and mobile. Email, WhatsApp and date of birth are optional. DOB accepts typed dd/MM/yyyy or calendar selection. The future-date check uses the business date.
- WhatsApp can copy the mobile number. This patch records WhatsApp details; it does not send WhatsApp messages.
- Saving an existing customer's profile preserves their credit limit, balance, address and notes. Duplicate numbers require explicit confirmation that another person shares the number; profiles are not merged automatically.
- Use saved customer selects the saved profile; Save & use customer persists edits. Cancel leaves the prior sale selection intact. Continue as walk-in removes the customer association without clearing cart items.
- Customer selection updates POS, including selection from within payment. Opening or cancelling the customer form preserves the cart and amount tendered. Completing/clearing a sale resets its customer selection.
- Purchases link permanently through the existing CustomerId. Changing the phone number does not change old receipt names/prices or lose history. Anonymous historical purchases cannot be linked retroactively just by adding a phone number.
- History shows invoice, date, store, amount, refund amount, payment method and sale/return status; twenty rows per page. Clicking an invoice opens the existing receipt/reprint popup. Receipt access checks both the customer and the user's accessible stores.

## Permissions and data scope

The existing customers-view permission controls lookup/history access; customers-manage controls profile saving. History/bill access also requires sales-view, and only sales from allowed stores appear. Customer profiles are shared within a tenant/business, matching the existing customer model; different tenants cannot read each other's profiles.

The built-in Cashier role already has customer-view/manage permissions. Review custom roles in Setup if the new actions are unavailable. Birthday lists require customer-view plus Super Admin, Business Owner or Store Manager. Sending also requires customer-manage. Active user/business checks run on customer-service calls.

## Birthday alerts and email setup

- Today's and the upcoming seven days' birthdays appear on the dashboard and in the POS Birthdays dialog for authorized staff. Selecting a birthday customer shows a POS indicator.
- CustomerEngagement:TimeZoneId defaults to Asia/Karachi; CountryCallingCode defaults to 92. Year-end birthdays are included. A 29 February birthday is observed on 28 February in non-leap years.
- Birthday email consent is off by default and needs an email address and DOB. Alerts still work without SMTP. Opting out stops queued email from being sent.
- To enable real email, configure CustomerEngagement:Smtp with your provider's Host, Port, EnableSsl, FromAddress, FromName, Username and Password; set Enabled to true. The example contains no working credentials. Store the real password in deployment secrets/environment settings. Port 587 with TLS is the typical configuration for this sender; implicit TLS/SMTPS on port 465 is not supported by System.Net.Mail.SmtpClient. Microsoft/Google providers that require OAuth need a compatible SMTP relay/app-password policy or a separately implemented sender; Gmail addresses are fully supported as customer recipients.
- Authorized staff use Preview email on today's opted-in customer, edit the personalized subject/body and queue it. Placeholders in configured templates are {CustomerName} and {BusinessName}.
- AutomaticBirthdayEmails remains false unless an administrator explicitly changes configuration. If enabled, automatic wishes queue from AutomaticSendHour (default 9) in the configured timezone. This release uses administrator deployment configuration for this option, not a new Settings UI.
- The background dispatcher runs every 30 seconds in the Web host; SQL row claiming and the unique customer/year index prevent two workers from normally sending the same queued row. SmartPOS.Worker is also wired to run the dispatcher if deployed with the same database/email configuration.
- IIS must be running for its worker to process queued mail. Use IIS AlwaysRunning/preload or your continuously running Worker host for reliable delivery when the website is idle. Restart the host after changing settings.
- Statuses: Not queued, Queued, Sending, Sent, Cancelled and Uncertain (shown as Check delivery). Sent means the SMTP server accepted the message; it does not prove final inbox delivery. After a crash/timeout/ambiguous provider response, automatic replay is blocked. Check provider records, then explicitly confirm retry in the preview; duplicate inbox delivery remains possible if an uncertain message was actually accepted. Confirmed Sent messages cannot be sent again through this flow in the same birthday year.
- Delivery rechecks business/customer activity, consent, current email and birthday before sending. An opted-out or changed/ineligible queued item becomes Cancelled.

## Verification completed and DEV acceptance

The Release solution build passed with zero errors (existing package-pruning/nullability warnings remain). All 92 backend tests, 10 Blazor component tests and 7 JavaScript keyboard tests passed. Backend tests use SQLite and include a local SMTP protocol receiver, not a live provider. Component tests render the actual Blazor components with simulated services; these are not live browser/SQL Server/IIS tests. Browser visual verification could not be completed in this execution environment.

Before deploying live, verify in DEV:

1. Apply the SQL script to the selected DEV database, rerun it to verify idempotence, then inspect the verification results. Confirm existing contacts are still intact and formatted phone searches work.
2. Complete an anonymous cash/card sale. Add a product, open customer details/F4, cancel and confirm cart/tender remain unchanged. Check the two customer-button locations and narrow-screen layout.
3. Save a name/mobile-only profile. Add optional email, WhatsApp and typed DOB. Find it with local/international phone formats. Confirm duplicate/shared-number selection and walk-in removal.
4. Select the customer, complete a sale, reopen history and its bill. Edit the phone and verify the same history remains. Verify a store-limited account cannot read another store's bill and a cashier cannot send birthday wishes.
5. Use a consenting customer with today's birthday and an email address you control to test preview/queue/send with your real SMTP provider. Verify opt-out, Sent status and duplicate protection. Confirm the web/Worker host processes the queue after restart/idle as intended.
6. Leave AutomaticBirthdayEmails false until the administrator has reviewed provider configuration and message templates.

Do not send real customer birthday emails during acceptance without appropriate authorization; use your own test address/profile.
