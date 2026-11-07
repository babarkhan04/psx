using System.Security.Claims;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Options;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Customers;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Services;
using SmartPOS.Infrastructure.Services.Management;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Tests;

public sealed class CustomerContactRulesTests
{
    [Theory]
    [InlineData("03001234567", "+923001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("0092-300-1234567", "+923001234567")]
    [InlineData("3001234567", "+923001234567")]
    [InlineData("923001234567", "+923001234567")]
    [InlineData("+1 (415) 555-2671", "+14155552671")]
    [InlineData("abc12345678", null)]
    [InlineData("++923001234567", null)]
    [InlineData("", null)]
    public void Phone_formats_resolve_to_a_canonical_lookup_key(string input, string? expected) =>
        Assert.Equal(expected, CustomerContactRules.NormalizePhone(input));

    [Fact]
    public void Birthdays_handle_leap_day_and_year_end()
    {
        Assert.Equal(new DateTime(2025, 2, 28), CustomerContactRules.BirthdayInYear(new DateTime(2000, 2, 29), 2025));
        Assert.Equal(new DateTime(2028, 2, 29), CustomerContactRules.BirthdayInYear(new DateTime(2000, 2, 29), 2028));
        Assert.Equal(new DateTime(2027, 1, 2), CustomerContactRules.NextBirthday(new DateTime(1990, 1, 2), new DateTime(2026, 12, 29)));
    }
}

public sealed partial class PosRegressionTests
{
    private static CustomerEngagementOptions MailOptions(bool enabled = true) => new()
    {
        Smtp = new CustomerSmtpOptions { Enabled = enabled, Host = "localhost", FromAddress = "shop@example.invalid" }
    };
    private static void AllowContacts(PosFixture fixture)
    {
        var identity = (ClaimsIdentity)fixture.Context.Principal!.Identity!;
        identity.AddClaim(new Claim(Permissions.ClaimType, Permissions.Relationships.CustomersView));
        identity.AddClaim(new Claim(Permissions.ClaimType, Permissions.Relationships.CustomersManage));
    }
    private static CustomerEngagementService Contacts(PosFixture f, CustomerEngagementOptions? options = null, IDateTimeProvider? time = null)
    {
        AllowContacts(f);
        return new(f.Db, new DbContextGate(f.Db), f.Context, f.Tenant, f.Access, time ?? f.Clock,
            Options.Create(options ?? MailOptions()), f.Pos);
    }
    private static CustomerProfileDto Contact(string phone = "03001234567", bool birthday = false) => new()
    {
        Name = "Customer", Phone = phone, Email = "customer@example.invalid",
        DateOfBirth = birthday ? new DateTime(1990, 10, 23) : null,
        BirthdayEmailOptIn = birthday
    };
    private sealed class BirthdayTestClock(DateTime now) : IDateTimeProvider { public DateTime UtcNow => now; }
    private sealed class RecordingCustomerSender : ICustomerEmailSender
    {
        public int Count { get; private set; }
        public bool Throw { get; set; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
        {
            Count++;
            if (Throw) throw new SmtpException("Simulated unconfirmed SMTP result");
            return Task.CompletedTask;
        }
    }
    private static CustomerBirthdayDispatcher Dispatcher(PosFixture f, CustomerEngagementOptions options, ICustomerEmailSender sender) =>
        new(f.Db, f.Tenant, f.Clock, Options.Create(options), sender, NullLogger<CustomerBirthdayDispatcher>.Instance);

    [Fact]
    public async Task Walk_in_sales_remain_optional_and_do_not_create_customers()
    {
        await using var f = await PosFixture.CreateAsync();
        var sale = await f.Pos.CompleteSaleAsync(f.Checkout());
        Assert.True(sale.Succeeded, sale.Error);
        Assert.Null((await f.Db.Sales.SingleAsync()).CustomerId);
        Assert.Empty(await f.Db.Customers.ToListAsync());
    }

    [Fact]
    public async Task Completed_sales_are_linked_to_the_optional_customer_profile()
    {
        await using var f = await PosFixture.CreateAsync();
        var service = Contacts(f);
        var id = (await service.SaveProfileAsync(Contact())).Data;
        var checkout = f.Checkout(); checkout.CustomerId = id;
        var result = await f.Pos.CompleteSaleAsync(checkout);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(id, (await f.Db.Sales.SingleAsync()).CustomerId);
        Assert.Equal(result.Data!.SaleId, Assert.Single((await service.GetHistoryAsync(id)).Purchases).SaleId);
        Assert.NotNull(await service.GetPurchaseReceiptAsync(id, result.Data.SaleId));
    }

    [Fact]
    public async Task Phone_search_detects_duplicates_and_shared_numbers_require_explicit_confirmation()
    {
        await using var f = await PosFixture.CreateAsync();
        var service = Contacts(f);
        var first = await service.SaveProfileAsync(Contact());
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(first.Data, Assert.Single(await service.FindAsync("+92 300 1234567")).Id);
        var second = Contact("+923001234567"); second.Name = "Different person";
        Assert.Equal("DUPLICATE_PHONE", (await service.SaveProfileAsync(second)).ErrorCode);
        Assert.Single(await f.Db.Customers.ToListAsync());
        second.AllowSharedPhone = true;
        Assert.True((await service.SaveProfileAsync(second)).Succeeded);
        Assert.Equal(2, (await service.FindAsync("03001234567")).Count);
    }

    [Fact]
    public async Task Profile_updates_preserve_credit_and_history_when_phone_changes()
    {
        await using var f = await PosFixture.CreateAsync();
        var service = Contacts(f);
        var created = await service.SaveProfileAsync(Contact());
        var customer = await f.Db.Customers.SingleAsync(); customer.CreditLimit = 5000; customer.Balance = 250;
        await f.Db.SaveChangesAsync();
        var sale = ReportSale(f, "CUSTOMER-OLD"); sale.CustomerId = created.Data; sale.CustomerName = "Original receipt name";
        f.Db.Sales.Add(sale); await f.Db.SaveChangesAsync();
        var changed = (await service.GetProfileAsync(created.Data))!;
        changed.Phone = "03011234567"; changed.Name = "Updated customer"; changed.CreditLimit = 0; changed.Balance = 0;
        Assert.True((await service.SaveProfileAsync(changed)).Succeeded);
        var record = await f.Db.Customers.AsNoTracking().SingleAsync();
        Assert.Equal(5000m, record.CreditLimit); Assert.Equal(250m, record.Balance);
        var history = await service.GetHistoryAsync(created.Data);
        Assert.Equal("CUSTOMER-OLD", Assert.Single(history.Purchases).SaleNumber);
        var receipt = await service.GetPurchaseReceiptAsync(created.Data, sale.Id);
        Assert.Equal("Original receipt name", receipt!.CustomerName);
        Assert.Equal(created.Data, Assert.Single(await service.FindAsync("+923011234567")).Id);
    }

    [Fact]
    public async Task Invalid_birthdays_and_opt_in_without_email_are_rejected()
    {
        await using var f = await PosFixture.CreateAsync();
        var service = Contacts(f);
        var profile = Contact(); profile.DateOfBirth = new DateTime(2099, 1, 1);
        Assert.Equal("INVALID_BIRTHDAY", (await service.SaveProfileAsync(profile)).ErrorCode);
        profile.DateOfBirth = new DateTime(1990, 10, 23); profile.Email = null; profile.BirthdayEmailOptIn = true;
        Assert.Equal("INVALID_CONSENT", (await service.SaveProfileAsync(profile)).ErrorCode);
        Assert.Empty(await f.Db.Customers.ToListAsync());
    }

    [Fact]
    public async Task Customer_history_and_receipts_are_limited_to_assigned_stores_and_correct_customer()
    {
        await using var f = await PosFixture.CreateAsync();
        var customer = new Customer { Name = "Customer", Phone = "03001234567" };
        var otherCustomer = new Customer { Name = "Someone else" };
        var branch = new Store { Name = "Private branch", Code = "PRIVATE" };
        f.Db.AddRange(customer, otherCustomer, branch, new UserStore { UserId = f.CurrentUser.UserId!, StoreId = f.Store.Id });
        var mainSale = ReportSale(f, "MAIN-SALE"); mainSale.CustomerId = customer.Id;
        var hiddenSale = ReportSale(f, "HIDDEN-SALE"); hiddenSale.CustomerId = customer.Id; hiddenSale.StoreId = branch.Id;
        var otherSale = ReportSale(f, "OTHER-CUSTOMER"); otherSale.CustomerId = otherCustomer.Id;
        f.Db.AddRange(mainSale, hiddenSale, otherSale); await f.Db.SaveChangesAsync();
        f.Context.RestrictToManager();
        var service = Contacts(f);
        Assert.Equal("MAIN-SALE", Assert.Single((await service.GetHistoryAsync(customer.Id)).Purchases).SaleNumber);
        Assert.Null(await service.GetPurchaseReceiptAsync(customer.Id, hiddenSale.Id));
        Assert.Null(await service.GetPurchaseReceiptAsync(customer.Id, otherSale.Id));
    }

    [Fact]
    public async Task Foreign_company_profiles_and_deactivated_users_are_blocked()
    {
        await using var f = await PosFixture.CreateAsync();
        var foreign = new Tenant { Name = "Other company" };
        var c = new Customer { TenantId = foreign.Id, Name = "Private customer", Phone = "03009999999" };
        f.Db.AddRange(foreign, c); await f.Db.SaveChangesAsync();
        var service = Contacts(f);
        Assert.Null(await service.GetProfileAsync(c.Id));
        Assert.Empty(await service.FindAsync("Private customer"));
        (await f.Db.Users.SingleAsync()).IsActive = false; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetContextAsync());
    }

    [Fact]
    public async Task Birthday_email_is_queued_once_sent_once_and_status_is_persisted()
    {
        await using var f = await PosFixture.CreateAsync();
        var options = MailOptions(); var service = Contacts(f, options);
        var id = (await service.SaveProfileAsync(Contact(birthday: true))).Data;
        var wish = await service.GetBirthdayWishAsync(id);
        Assert.Contains("Customer", wish.Subject); Assert.Contains("Company", wish.Body);
        Assert.True((await service.QueueBirthdayWishAsync(wish)).Succeeded);
        Assert.Equal("DUPLICATE_WISH", (await service.QueueBirthdayWishAsync(wish)).ErrorCode);
        var sender = new RecordingCustomerSender(); var dispatcher = Dispatcher(f, options, sender);
        await dispatcher.RunOnceAsync(); await dispatcher.RunOnceAsync();
        Assert.Equal(1, sender.Count);
        var mail = await f.Db.CustomerBirthdayEmails.AsNoTracking().SingleAsync();
        Assert.Equal("Sent", mail.Status); Assert.NotNull(mail.SentOn); Assert.Equal(1, mail.Attempts);
        Assert.Equal("DUPLICATE_WISH", (await service.QueueBirthdayWishAsync(wish)).ErrorCode);
        Assert.Equal("Sent", Assert.Single(await service.GetBirthdaysAsync()).EmailStatus);
    }

    [Fact]
    public async Task Opting_out_after_queueing_cancels_delivery()
    {
        await using var f = await PosFixture.CreateAsync();
        var options = MailOptions(); var service = Contacts(f, options);
        var id = (await service.SaveProfileAsync(Contact(birthday: true))).Data;
        Assert.True((await service.QueueBirthdayWishAsync(await service.GetBirthdayWishAsync(id))).Succeeded);
        var profile = (await service.GetProfileAsync(id))!; profile.BirthdayEmailOptIn = false;
        Assert.True((await service.SaveProfileAsync(profile)).Succeeded);
        var sender = new RecordingCustomerSender(); await Dispatcher(f, options, sender).RunOnceAsync();
        Assert.Equal(0, sender.Count);
        Assert.Equal("Cancelled", (await f.Db.CustomerBirthdayEmails.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Unconfirmed_delivery_requires_explicit_retry_and_is_never_automatically_repeated()
    {
        await using var f = await PosFixture.CreateAsync();
        var options = MailOptions(); var service = Contacts(f, options);
        var id = (await service.SaveProfileAsync(Contact(birthday: true))).Data;
        var wish = await service.GetBirthdayWishAsync(id);
        await service.QueueBirthdayWishAsync(wish);
        var sender = new RecordingCustomerSender { Throw = true }; var dispatcher = Dispatcher(f, options, sender);
        await dispatcher.RunOnceAsync(); await dispatcher.RunOnceAsync();
        Assert.Equal(1, sender.Count);
        Assert.Equal("Uncertain", (await f.Db.CustomerBirthdayEmails.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("DUPLICATE_WISH", (await service.QueueBirthdayWishAsync(wish)).ErrorCode);
        wish.ConfirmRetry = true;
        Assert.True((await service.QueueBirthdayWishAsync(wish)).Succeeded);
        sender.Throw = false; await dispatcher.RunOnceAsync();
        Assert.Equal(2, sender.Count);
        Assert.Equal("Sent", (await f.Db.CustomerBirthdayEmails.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Interrupted_sending_is_flagged_for_review_instead_of_replayed()
    {
        await using var f = await PosFixture.CreateAsync();
        var options = MailOptions(); var service = Contacts(f, options);
        var id = (await service.SaveProfileAsync(Contact(birthday: true))).Data;
        f.Db.CustomerBirthdayEmails.Add(new CustomerBirthdayEmail
        {
            CustomerId = id, BirthdayYear = 2026, Recipient = "customer@example.invalid", Subject = "Birthday", Body = "Wish",
            Status = "Sending", LeaseUntil = f.Clock.UtcNow.AddMinutes(-1)
        });
        await f.Db.SaveChangesAsync();
        var sender = new RecordingCustomerSender(); await Dispatcher(f, options, sender).RunOnceAsync();
        Assert.Equal(0, sender.Count);
        Assert.Equal("Uncertain", (await f.Db.CustomerBirthdayEmails.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Automatic_birthdays_are_disabled_by_default_and_opt_in_is_required()
    {
        await using var f = await PosFixture.CreateAsync();
        var options = MailOptions(); var service = Contacts(f, options);
        await service.SaveProfileAsync(Contact(birthday: true));
        var sender = new RecordingCustomerSender(); var dispatcher = Dispatcher(f, options, sender);
        await dispatcher.RunOnceAsync(); Assert.Equal(0, sender.Count);
        options.AutomaticBirthdayEmails = true;
        await dispatcher.RunOnceAsync(); await dispatcher.RunOnceAsync();
        Assert.Equal(1, sender.Count);
        Assert.Single(await f.Db.CustomerBirthdayEmails.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Disabled_smtp_is_reported_and_no_wish_is_falsely_marked_sent()
    {
        await using var f = await PosFixture.CreateAsync();
        var service = Contacts(f, MailOptions(false));
        var id = (await service.SaveProfileAsync(Contact(birthday: true))).Data;
        Assert.False((await service.GetContextAsync()).EmailConfigured);
        Assert.Equal("EMAIL_NOT_CONFIGURED", (await service.QueueBirthdayWishAsync(await service.GetBirthdayWishAsync(id))).ErrorCode);
        Assert.Empty(await f.Db.CustomerBirthdayEmails.ToListAsync());
    }

    [Fact]
    public async Task Local_business_date_drives_birthday_alerts_across_utc_midnight()
    {
        await using var f = await PosFixture.CreateAsync();
        var clock = new BirthdayTestClock(new DateTime(2026, 10, 22, 20, 0, 0, DateTimeKind.Utc));
        var service = Contacts(f, time: clock);
        await service.SaveProfileAsync(Contact(birthday: true));
        Assert.Equal(new DateTime(2026, 10, 23), (await service.GetContextAsync()).BusinessDate);
        Assert.True(Assert.Single(await service.GetBirthdaysAsync()).IsToday);
    }

    [Fact]
    public async Task Cashier_can_capture_contacts_but_cannot_read_birthday_lists_or_send_wishes()
    {
        await using var f = await PosFixture.CreateAsync();
        f.Context.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, f.CurrentUser.UserId!), new Claim(ClaimTypes.Role, DefaultRolePermissions.Cashier)
        }, "test")));
        var service = Contacts(f);
        Assert.True((await service.SaveProfileAsync(Contact())).Succeeded);
        Assert.False((await service.GetContextAsync()).CanViewBirthdays);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBirthdaysAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.QueueBirthdayWishAsync(new BirthdayWishDto()));
    }
}

public sealed class CustomerSmtpSenderTests
{
    [Fact]
    public async Task Configured_sender_transmits_to_a_local_smtp_receiver()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost SMTP test");
            var data = new StringBuilder();
            while (await reader.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
                else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO")) await writer.WriteLineAsync("250 OK");
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 send data");
                    while (await reader.ReadLineAsync(deadline.Token) is { } body && body != ".") data.AppendLine(body);
                    await writer.WriteLineAsync("250 accepted");
                }
                else if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
                else await writer.WriteLineAsync("250 OK");
            }
            return data.ToString();
        }, deadline.Token);
        var sender = new CustomerSmtpSender(Options.Create(new CustomerEngagementOptions
        {
            Smtp = new CustomerSmtpOptions { Enabled = true, Host = "127.0.0.1", Port = port, EnableSsl = false, FromAddress = "shop@example.invalid" }
        }));
        await sender.SendAsync("customer@example.invalid", "Birthday wish", "Happy birthday!", deadline.Token);
        var wire = await received;
        Assert.Contains("customer@example.invalid", wire);
        Assert.Contains("Subject:", wire);
        var bodyStart = wire.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(bodyStart >= 0, "SMTP message must have a MIME header/body boundary.");
        var headers = wire[..bodyStart];
        var encodedBody = wire[(bodyStart + 2)..].Trim();
        var decodedBody = headers.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(encodedBody))
            : headers.Contains("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase)
                ? System.Text.RegularExpressions.Regex.Replace(encodedBody.Replace("=\n", ""), "=([0-9A-Fa-f]{2})",
                    match => ((char)Convert.ToByte(match.Groups[1].Value, 16)).ToString())
                : encodedBody;
        Assert.Contains("Happy birthday!", decodedBody);
    }
}
