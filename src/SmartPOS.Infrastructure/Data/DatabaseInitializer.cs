using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;

namespace SmartPOS.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        if (config.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        await EnsureRolesAndPermissionsAsync(roleManager);

        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        var seed = config.GetSection("Seed");
        if (!seed.GetValue("Enabled", false))
        {
            logger.LogInformation("Seeding disabled via configuration; skipping demo data.");
            return;
        }

        var seedAdminPassword = seed["Admin:Password"];
        if (string.IsNullOrWhiteSpace(seedAdminPassword) || seedAdminPassword.Length < 8)
            throw new InvalidOperationException("Set Seed:Admin:Password explicitly before enabling database seeding.");
        var seedCashierEnabled = seed.GetValue<bool>("Cashier:Enabled");
        var seedCashierPassword = seed["Cashier:Password"];
        if (seedCashierEnabled && (string.IsNullOrWhiteSpace(seedCashierPassword) || seedCashierPassword.Length < 8))
            throw new InvalidOperationException("Set Seed:Cashier:Password explicitly before enabling the demo cashier.");

        // ── Tenant ───────────────────────────────────────────────────────────
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name       = seed["Company:Name"]      ?? "SmartPOS Mart Pakistan",
            LegalName  = seed["Company:LegalName"] ?? "SmartPOS Retail (Pvt) Ltd",
            Email      = seed["Company:Email"]     ?? "owner@smartpos.pk",
            Phone      = seed["Company:Phone"]     ?? "+92 21 111 222 333",
            IsActive   = true,
            CreatedOn  = DateTime.UtcNow
        };
        db.Tenants.Add(tenant);

        // ── Stores ───────────────────────────────────────────────────────────
        var storeMain = new Store
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Code = "KHI", Name = "Karachi Main",
            City = "Karachi", Region = "Sindh", Country = "Pakistan",
            Phone = "+92 21 3456 7890", Email = "karachi@smartpos.pk",
            IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var storeDown = new Store
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Code = "LHR", Name = "Lahore Branch",
            City = "Lahore", Region = "Punjab", Country = "Pakistan",
            Phone = "+92 42 3567 8901", Email = "lahore@smartpos.pk",
            IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        db.Stores.AddRange(storeMain, storeDown);

        // ── Registers ────────────────────────────────────────────────────────
        var reg1 = new Register
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
            Code = "R1", Name = "Register 1", Location = "Counter A",
            HasCashDrawer = true, IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var reg2 = new Register
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
            Code = "R2", Name = "Register 2", Location = "Counter B",
            HasCashDrawer = true, IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var reg3 = new Register
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeDown.Id,
            Code = "R1", Name = "Downtown Register", Location = "Main Counter",
            HasCashDrawer = true, IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        db.Registers.AddRange(reg1, reg2, reg3);

        // ── Tax Categories (Pakistan GST / Sales Tax) ──────────────────────────
        var taxStd  = new TaxCategory { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "GST 17% (Standard)", Rate = 17m, IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var taxLow  = new TaxCategory { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Reduced 5%",         Rate = 5m,  IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var taxZero = new TaxCategory { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Exempt / Zero (0%)",  Rate = 0m,  IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        db.TaxCategories.AddRange(taxStd, taxLow, taxZero);

        // ── Units ─────────────────────────────────────────────────────────────
        var unitPc  = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Piece",   Abbreviation = "Pc",  IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var unitKg  = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Kilogram", Abbreviation = "Kg",  IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var unitL   = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Litre",   Abbreviation = "L",   IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var unitBox = new UnitOfMeasure { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Box",     Abbreviation = "Box", IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        db.Units.AddRange(unitPc, unitKg, unitL, unitBox);

        // ── Categories ────────────────────────────────────────────────────────
        var catGrocery = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Grocery & Staples", IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var catBev     = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Beverages",         IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var catSnacks  = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Snacks & Confectionery", IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var catDairy   = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Dairy & Bakery",    IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var catHome    = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Home Care",         IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var catElec    = new Category { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Mobile & Electronics", IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        db.Categories.AddRange(catGrocery, catBev, catSnacks, catDairy, catHome, catElec);

        // ── Brands (popular in Pakistan) ───────────────────────────────────────
        var bNestle  = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Nestlé",     IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bTapal   = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Tapal",      IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bNurpur  = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Nurpur",     IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bNational= new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "National",   IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bLays    = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Lay's",      IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bSamsung = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Samsung",    IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        var bGeneric = new Brand { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Generic",    IsActive = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" };
        db.Brands.AddRange(bNestle, bTapal, bNurpur, bNational, bLays, bSamsung, bGeneric);

        await db.SaveChangesAsync();

        // ── Products (Pakistani retail goods, prices in PKR) ───────────────────
        var products = new List<Product>
        {
            // Grocery & staples
            P("RICE-SELLA-5", "Guard Sella Basmati Rice 5kg",  catGrocery.Id, bGeneric.Id,  unitBox.Id, taxZero.Id, 2200m, 2750m,  20, tenant.Id, "8964000100015"),
            P("ATTA-10KG",    "Chakki Atta 10kg",              catGrocery.Id, bGeneric.Id,  unitBox.Id, taxZero.Id, 1150m, 1390m,  25, tenant.Id, "8964000100022"),
            P("SUGAR-1KG",    "Refined Sugar 1kg",             catGrocery.Id, bGeneric.Id,  unitKg.Id,  taxZero.Id,  140m,  170m,  60, tenant.Id, "8964000100039"),
            P("OIL-DALDA-1L", "Dalda Cooking Oil 1L",          catGrocery.Id, bGeneric.Id,  unitL.Id,   taxStd.Id,   520m,  640m,  40, tenant.Id, "8964000100046"),
            P("SALT-800G",    "National Iodized Salt 800g",    catGrocery.Id, bNational.Id, unitPc.Id,  taxZero.Id,   45m,   60m,  50, tenant.Id, "8964000100053"),
            P("MASALA-BIRYANI","National Bombay Biryani Masala",catGrocery.Id,bNational.Id, unitPc.Id,  taxStd.Id,    90m,  120m,  45, tenant.Id, "8964000100060"),

            // Beverages
            P("TEA-DANEDAR-950","Tapal Danedar Tea 950g",      catBev.Id,     bTapal.Id,    unitBox.Id, taxStd.Id,  1450m, 1690m,  30, tenant.Id, "8964000200014"),
            P("COKE-1.5L",    "Coca-Cola 1.5L",                catBev.Id,     bGeneric.Id,  unitL.Id,   taxStd.Id,   130m,  170m,  80, tenant.Id, "8964000200021"),
            P("WATER-1.5L",   "Nestlé Pure Life Water 1.5L",   catBev.Id,     bNestle.Id,   unitL.Id,   taxStd.Id,    55m,   80m, 120, tenant.Id, "8964000200038"),
            P("JUICE-NESTLE", "Nestlé Fruita Vitals Mango 1L", catBev.Id,     bNestle.Id,   unitL.Id,   taxStd.Id,   180m,  240m,  40, tenant.Id, "8964000200045"),

            // Snacks & confectionery
            P("LAYS-MASALA",  "Lay's Masala Chips 70g",        catSnacks.Id,  bLays.Id,     unitPc.Id,  taxStd.Id,    70m,  100m, 150, tenant.Id, "8964000300013"),
            P("KITKAT-4F",    "Nestlé KitKat 4-Finger",        catSnacks.Id,  bNestle.Id,   unitPc.Id,  taxStd.Id,    90m,  130m, 100, tenant.Id, "8964000300020"),

            // Dairy & bakery
            P("MILK-NURPUR-1L","Nurpur UHT Milk 1L",           catDairy.Id,   bNurpur.Id,   unitL.Id,   taxStd.Id,   240m,  290m,  60, tenant.Id, "8964000400012"),
            P("BUTTER-NURPUR","Nurpur Butter 200g",            catDairy.Id,   bNurpur.Id,   unitPc.Id,  taxStd.Id,   480m,  560m,  25, tenant.Id, "8964000400029"),

            // Home care
            P("SOAP-LIFEBUOY","Lifebuoy Soap 130g",            catHome.Id,    bGeneric.Id,  unitPc.Id,  taxStd.Id,    95m,  130m,  70, tenant.Id, "8964000500011"),
            P("SURF-1KG",     "Surf Excel Washing Powder 1kg", catHome.Id,    bGeneric.Id,  unitKg.Id,  taxStd.Id,   560m,  690m,  35, tenant.Id, "8964000500028"),

            // Mobile & electronics
            P("GALAXY-A15",   "Samsung Galaxy A15 (128GB)",    catElec.Id,    bSamsung.Id,  unitPc.Id,  taxStd.Id, 48000m, 56999m,  6, tenant.Id, "8964000600010"),
            P("CHARGER-25W",  "Samsung 25W Fast Charger",      catElec.Id,    bSamsung.Id,  unitPc.Id,  taxStd.Id,  1800m,  2700m, 20, tenant.Id, "8964000600027"),
            P("USB-C-CABLE",  "USB-C Charging Cable 1m",       catElec.Id,    bGeneric.Id,  unitPc.Id,  taxStd.Id,   250m,   450m, 40, tenant.Id, "8964000600034"),
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // ── Store-specific prices (Lahore runs a small markup) ─────────────────
        var storePrices = new List<ProductPrice>
        {
            SP(products[0].Id,  storeDown.Id, 2820m, tenant.Id),
            SP(products[6].Id,  storeDown.Id, 1740m, tenant.Id),
            SP(products[16].Id, storeDown.Id, 57999m, tenant.Id),
        };
        db.ProductPrices.AddRange(storePrices);

        // ── Opening stock for Main Store ──────────────────────────────────────
        var stocks = new List<Stock>();
        var ledgers = new List<StockLedger>();
        foreach (var p in products)
        {
            var qty = p.ReorderLevel * 5 + 10;
            stocks.Add(new Stock
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                StoreId = storeMain.Id, ProductId = p.Id,
                QuantityOnHand = qty,
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            });
            ledgers.Add(new StockLedger
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                StoreId = storeMain.Id, ProductId = p.Id,
                MovementType = StockMovementType.Opening,
                QuantityChange = qty, QuantityAfter = qty,
                Reference = "OPENING",
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            });
        }

        // Downtown store — subset of products with lower stock
        foreach (var p in products.Take(8))
        {
            var qty = p.ReorderLevel * 2 + 5;
            stocks.Add(new Stock
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                StoreId = storeDown.Id, ProductId = p.Id,
                QuantityOnHand = qty,
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            });
            ledgers.Add(new StockLedger
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                StoreId = storeDown.Id, ProductId = p.Id,
                MovementType = StockMovementType.Opening,
                QuantityChange = qty, QuantityAfter = qty,
                Reference = "OPENING",
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            });
        }

        db.Stocks.AddRange(stocks);
        db.StockLedgers.AddRange(ledgers);

        // ── Customers (with some carrying udhaar) ──────────────────────────────
        var custWalkIn = new Customer
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Ahmed Raza", Phone = "+92 300 1234567", City = "Karachi",
            CreditLimit = 20000m, Balance = 0m, IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var custShop = new Customer
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Bilal General Store", Phone = "+92 321 9876543", City = "Lahore",
            CreditLimit = 100000m, Balance = 35000m, IsActive = true,
            Notes = "Wholesale buyer — settles monthly.",
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var custReg = new Customer
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Sana Khan", Phone = "+92 333 5556677", City = "Karachi",
            CreditLimit = 15000m, Balance = 4500m, IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var custCash = new Customer
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Imran Traders", Phone = "+92 301 4445566", City = "Lahore",
            CreditLimit = 0m, Balance = 0m, IsActive = true,
            Notes = "Cash only.",
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        db.Customers.AddRange(custWalkIn, custShop, custReg, custCash);

        // Opening-balance ledger entries for customers carrying udhaar.
        db.CustomerLedgers.AddRange(
            new CustomerLedger
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, CustomerId = custShop.Id,
                EntryType = CustomerLedgerEntryType.Opening, Amount = 35000m, BalanceAfter = 35000m,
                Reference = "OPENING", EntryDate = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            },
            new CustomerLedger
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, CustomerId = custReg.Id,
                EntryType = CustomerLedgerEntryType.Opening, Amount = 4500m, BalanceAfter = 4500m,
                Reference = "OPENING", EntryDate = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow, CreatedBy = "system"
            });

        // ── Suppliers ─────────────────────────────────────────────────────────
        var supMetro = new Supplier
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Metro Cash & Carry", Phone = "+92 21 111 000 111", City = "Karachi",
            TaxNumber = "1234567-8", IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var supUnilever = new Supplier
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Unilever Pakistan Distributors", Phone = "+92 42 111 222 333", City = "Lahore",
            TaxNumber = "7654321-0", IsActive = true,
            Notes = "FMCG — beverages, home care.",
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        var supLocal = new Supplier
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            Name = "Sabzi Mandi Wholesale", Phone = "+92 300 9998877", City = "Karachi",
            IsActive = true,
            CreatedOn = DateTime.UtcNow, CreatedBy = "system"
        };
        db.Suppliers.AddRange(supMetro, supUnilever, supLocal);

        // ── Seed admin user ───────────────────────────────────────────────────
        await db.SaveChangesAsync();

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail    = seed["Admin:Email"]       ?? "admin@smartpos.pk";
        var adminPassword = seedAdminPassword;
        var adminDisplay  = seed["Admin:DisplayName"] ?? "System Administrator";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail, Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = adminDisplay,
                TenantId = tenant.Id,
                IsActive = true,
                CreatedOn = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to seed admin user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        await userManager.AddToRoleAsync(adminUser, DefaultRolePermissions.SuperAdmin);
        db.UserStores.AddRange(
            new UserStore { UserId = adminUser.Id, StoreId = storeMain.Id, TenantId = tenant.Id, IsDefault = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system" },
            new UserStore { UserId = adminUser.Id, StoreId = storeDown.Id, TenantId = tenant.Id, IsDefault = false, CreatedOn = DateTime.UtcNow, CreatedBy = "system" }
        );

        // ── Cashier demo user ─────────────────────────────────────────────────
        var cashierEmail = seed["Cashier:Email"] ?? "cashier@smartpos.pk";
        if (seedCashierEnabled && await userManager.FindByEmailAsync(cashierEmail) is null)
        {
            var cashier = new ApplicationUser
            {
                UserName = cashierEmail, Email = cashierEmail,
                EmailConfirmed = true, DisplayName = "Demo Cashier",
                TenantId = tenant.Id, IsActive = true, CreatedOn = DateTime.UtcNow
            };
            var cr = await userManager.CreateAsync(cashier, seedCashierPassword!);
            if (cr.Succeeded)
            {
                await userManager.AddToRoleAsync(cashier, DefaultRolePermissions.Cashier);
                db.UserStores.Add(new UserStore
                {
                    UserId = cashier.Id, StoreId = storeMain.Id, TenantId = tenant.Id,
                    IsDefault = true, CreatedOn = DateTime.UtcNow, CreatedBy = "system"
                });
            }
        }

        // ── Open demo shifts (POS requires an active shift on cash registers) ─
        var shiftOpenedOn = DateTime.UtcNow;
        var shiftKhi1 = new Shift
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
            RegisterId = reg1.Id, ShiftNumber = $"KHI-R1-{shiftOpenedOn:yyyyMMdd}-001",
            OpenedByUserId = adminUser.Id, Status = ShiftStatus.Open,
            OpeningFloat = 10000m, OpenedOn = shiftOpenedOn,
            CreatedOn = shiftOpenedOn, CreatedBy = "system"
        };
        var shiftKhi2 = new Shift
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
            RegisterId = reg2.Id, ShiftNumber = $"KHI-R2-{shiftOpenedOn:yyyyMMdd}-001",
            OpenedByUserId = adminUser.Id, Status = ShiftStatus.Open,
            OpeningFloat = 5000m, OpenedOn = shiftOpenedOn,
            CreatedOn = shiftOpenedOn, CreatedBy = "system"
        };
        var shiftLhr = new Shift
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeDown.Id,
            RegisterId = reg3.Id, ShiftNumber = $"LHR-R1-{shiftOpenedOn:yyyyMMdd}-001",
            OpenedByUserId = adminUser.Id, Status = ShiftStatus.Open,
            OpeningFloat = 8000m, OpenedOn = shiftOpenedOn,
            CreatedOn = shiftOpenedOn, CreatedBy = "system"
        };
        db.Shifts.AddRange(shiftKhi1, shiftKhi2, shiftLhr);
        db.CashMovements.AddRange(
            new CashMovement
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
                ShiftId = shiftKhi1.Id, MovementType = CashMovementType.OpeningFloat,
                Amount = 10000m, BalanceAfter = 10000m, Reference = shiftKhi1.ShiftNumber,
                OccurredOn = shiftOpenedOn, CreatedOn = shiftOpenedOn, CreatedBy = "system"
            },
            new CashMovement
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
                ShiftId = shiftKhi2.Id, MovementType = CashMovementType.OpeningFloat,
                Amount = 5000m, BalanceAfter = 5000m, Reference = shiftKhi2.ShiftNumber,
                OccurredOn = shiftOpenedOn, CreatedOn = shiftOpenedOn, CreatedBy = "system"
            },
            new CashMovement
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeDown.Id,
                ShiftId = shiftLhr.Id, MovementType = CashMovementType.OpeningFloat,
                Amount = 8000m, BalanceAfter = 8000m, Reference = shiftLhr.ShiftNumber,
                OccurredOn = shiftOpenedOn, CreatedOn = shiftOpenedOn, CreatedBy = "system"
            });

        // ── Demo received GRN (stock already seeded; documents purchase history) ─
        if (products.Count >= 2)
        {
            var grnNow = DateTime.UtcNow.AddDays(-3);
            var demoPurchase = new Purchase
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, StoreId = storeMain.Id,
                SupplierId = supMetro.Id,
                PurchaseNumber = "KHI-PO-" + grnNow.ToString("yyyyMMdd") + "-0001",
                Status = PurchaseStatus.Received,
                ReceivedByUserId = adminUser.Id,
                Subtotal = 85000m, TaxAmount = 14450m, TotalAmount = 99450m,
                Notes = "Weekly grocery restock",
                OrderedOn = grnNow, ReceivedOn = grnNow,
                CreatedOn = grnNow, CreatedBy = "system"
            };
            db.Purchases.Add(demoPurchase);
            db.PurchaseLines.AddRange(
                new PurchaseLine
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, PurchaseId = demoPurchase.Id, LineNumber = 1,
                    ProductId = products[0].Id, Sku = products[0].Sku, ProductName = products[0].Name,
                    Quantity = 50, UnitCost = 1100m, TaxRate = 17m, TaxAmount = 9350m, LineTotal = 64350m,
                    CreatedOn = grnNow, CreatedBy = "system"
                },
                new PurchaseLine
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, PurchaseId = demoPurchase.Id, LineNumber = 2,
                    ProductId = products[2].Id, Sku = products[2].Sku, ProductName = products[2].Name,
                    Quantity = 100, UnitCost = 350m, TaxRate = 17m, TaxAmount = 5950m, LineTotal = 40950m,
                    CreatedOn = grnNow, CreatedBy = "system"
                });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("SmartPOS seeded: tenant='{Tenant}', {Products} products, 2 stores, 3 registers, 3 open shifts, 3 suppliers.",
            tenant.Name, products.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Product P(
        string sku, string name,
        Guid categoryId, Guid brandId, Guid unitId, Guid taxCategoryId,
        decimal costPrice, decimal sellPrice, decimal reorderLevel,
        Guid tenantId, string? barcode = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId,
        Sku = sku, Name = name, Barcode = barcode,
        CategoryId = categoryId, BrandId = brandId,
        UnitId = unitId, TaxCategoryId = taxCategoryId,
        CostPrice = costPrice, SellPrice = sellPrice,
        ReorderLevel = reorderLevel,
        IsActive = true,
        CreatedOn = DateTime.UtcNow, CreatedBy = "system"
    };

    private static ProductPrice SP(Guid productId, Guid storeId, decimal price, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId,
        ProductId = productId, StoreId = storeId, Price = price,
        CreatedOn = DateTime.UtcNow, CreatedBy = "system"
    };

    // ── Role bootstrap ────────────────────────────────────────────────────────

    private static async Task EnsureRolesAndPermissionsAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in DefaultRolePermissions.AllRoles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
            }

            if (!DefaultRolePermissions.Map.TryGetValue(roleName, out var permissions))
            {
                continue;
            }

            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            {
                await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
            }
        }
    }
}
