using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Common;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Identity;
using UserStore = SmartPOS.Domain.Entities.UserStore;

namespace SmartPOS.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Register> Registers => Set<Register>();
    public DbSet<UserStore> UserStores => Set<UserStore>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductStoreAssignment> ProductStoreAssignments => Set<ProductStoreAssignment>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<StockTransferStatusHistory> StockTransferStatusHistories => Set<StockTransferStatusHistory>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();
    public DbSet<HeldSaleLine> HeldSaleLines => Set<HeldSaleLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ReceiptReprintLog> ReceiptReprintLogs => Set<ReceiptReprintLog>();
    public DbSet<PosShortcutSetting> PosShortcutSettings => Set<PosShortcutSetting>();
    public DbSet<ReceiptTemplate> ReceiptTemplates => Set<ReceiptTemplate>();
    public DbSet<PrintStationSetting> PrintStationSettings => Set<PrintStationSetting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLedger> CustomerLedgers => Set<CustomerLedger>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashDenominationCount> CashDenominationCounts => Set<CashDenominationCount>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ManagerApprovalLog> ManagerApprovalLogs => Set<ManagerApprovalLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(250);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.TaxNumber).HasMaxLength(100);
            entity.HasIndex(x => x.Name);
        });

        builder.Entity<Store>(entity =>
        {
            entity.ToTable("Stores");
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.Stores).HasForeignKey(x => x.TenantId);
        });

        builder.Entity<Register>(entity =>
        {
            entity.ToTable("Registers");
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(100);
            entity.Property(x => x.PrinterName).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.Code }).IsUnique();
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserStore>(entity =>
        {
            entity.ToTable("UserStores");
            entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.StoreId }).IsUnique();
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<Brand>(entity =>
        {
            entity.ToTable("Brands");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("Units");
            entity.Property(x => x.Name).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Abbreviation).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<TaxCategory>(entity =>
        {
            entity.ToTable("TaxCategories");
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Rate).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Barcode).HasMaxLength(60);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.SellPrice).HasPrecision(18, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            entity.Property(x => x.IsPosFavorite).HasDefaultValue(false);
            entity.Property(x => x.PosFavoriteSortOrder).HasDefaultValue(0);
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            entity.HasIndex(x => x.Barcode);
            entity.HasIndex(x => new { x.TenantId, x.IsPosFavorite, x.PosFavoriteSortOrder });
            entity.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Unit).WithMany(x => x.Products).HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.TaxCategory).WithMany(x => x.Products).HasForeignKey(x => x.TaxCategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductBarcode>(entity =>
        {
            entity.ToTable("ProductBarcodes");
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasOne(x => x.Product).WithMany(x => x.Barcodes).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductPrice>(entity =>
        {
            entity.ToTable("ProductPrices");
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ProductId, x.StoreId }).IsUnique();
            entity.HasOne(x => x.Product).WithMany(x => x.StorePrices).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProductStoreAssignment>(entity =>
        {
            entity.ToTable("ProductStoreAssignments");
            entity.Property(x => x.AssignedByUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.TenantId, x.ProductId, x.StoreId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.IsVisibleOnPos });
            entity.HasOne(x => x.Product).WithMany(x => x.StoreAssignments).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("ProductVariants");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Barcode).HasMaxLength(60);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.SellPrice).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            entity.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stocks");
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.StoreId, x.ProductId, x.ProductVariantId }).IsUnique();
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockLedger>(entity =>
        {
            entity.ToTable("StockLedgers");
            entity.Property(x => x.QuantityChange).HasPrecision(18, 3);
            entity.Property(x => x.QuantityAfter).HasPrecision(18, 3);
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => x.CreatedOn);
            entity.HasIndex(x => new { x.StoreId, x.ProductId });
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("StockTransfers");
            entity.Property(x => x.TransferNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ApprovedByUserId).HasMaxLength(450);
            entity.Property(x => x.DispatchedByUserId).HasMaxLength(450);
            entity.Property(x => x.ReceivedByUserId).HasMaxLength(450);
            entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.TransferNumber }).IsUnique();
            entity.HasIndex(x => new { x.StoreId, x.Status, x.RequestedOn });
            entity.HasIndex(x => new { x.DestinationStoreId, x.Status, x.RequestedOn });
            entity.HasOne(x => x.SourceStore).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DestinationStore).WithMany().HasForeignKey(x => x.DestinationStoreId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockTransferLine>(entity =>
        {
            entity.ToTable("StockTransferLines");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.VariantName).HasMaxLength(120);
            entity.Property(x => x.QuantityRequested).HasPrecision(18, 3);
            entity.Property(x => x.QuantityDispatched).HasPrecision(18, 3);
            entity.Property(x => x.QuantityReceived).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.StockTransferId, x.LineNumber }).IsUnique();
            entity.HasOne(x => x.StockTransfer).WithMany(x => x.Lines).HasForeignKey(x => x.StockTransferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockTransferStatusHistory>(entity =>
        {
            entity.ToTable("StockTransferStatusHistory");
            entity.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.StockTransferId, x.ChangedOn });
            entity.HasOne(x => x.StockTransfer).WithMany(x => x.StatusHistory).HasForeignKey(x => x.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");
            entity.Property(x => x.SaleNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CashierUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountReason).HasMaxLength(300);
            entity.Property(x => x.DiscountApprovedByUserId).HasMaxLength(450);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.Property(x => x.ChangeDue).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.SaleNumber }).IsUnique();
            entity.HasIndex(x => x.CompletedOn);
            entity.HasIndex(x => new { x.StoreId, x.CompletedOn });
            entity.HasOne(x => x.Register).WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany(x => x.Sales).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleLine>(entity =>
        {
            entity.ToTable("SaleLines");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.CostPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountReason).HasMaxLength(300);
            entity.Property(x => x.TaxRate).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.SaleId, x.LineNumber }).IsUnique();
            entity.HasOne(x => x.Sale).WithMany(x => x.Lines).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<HeldSale>(entity =>
        {
            entity.ToTable("HeldSales");
            entity.Property(x => x.HeldNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CashierUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.RetrievedByUserId).HasMaxLength(450);
            entity.Property(x => x.CancelledByUserId).HasMaxLength(450);
            entity.Property(x => x.ExpiryReason).HasMaxLength(300);
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountReason).HasMaxLength(300);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.HeldNumber }).IsUnique();
            entity.HasIndex(x => new { x.StoreId, x.RegisterId, x.Status, x.HeldOn });
            entity.HasIndex(x => new { x.StoreId, x.RegisterId, x.Status, x.ExpiresOn });
            entity.HasIndex(x => x.HeldOn);
            entity.HasIndex(x => x.ExpiresOn);
            entity.HasOne(x => x.Register).WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HeldSaleLine>(entity =>
        {
            entity.ToTable("HeldSaleLines");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountReason).HasMaxLength(300);
            entity.Property(x => x.TaxRate).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.HeldSaleId, x.LineNumber }).IsUnique();
            entity.HasOne(x => x.HeldSale).WithMany(x => x.Lines).HasForeignKey(x => x.HeldSaleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.HasIndex(x => x.SaleId);
            entity.HasOne(x => x.Sale).WithMany(x => x.Payments).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReceiptReprintLog>(entity =>
        {
            entity.ToTable("ReceiptReprintLogs");
            entity.Property(x => x.SaleNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReprintedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ReprintedByName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => new { x.SaleId, x.ReprintedOn });
            entity.HasIndex(x => new { x.StoreId, x.ReprintedOn });
            entity.HasOne(x => x.Sale).WithMany(x => x.ReceiptReprintLogs).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        });


        builder.Entity<PosShortcutSetting>(entity =>
        {
            entity.ToTable("PosShortcutSettings");
            entity.Property(x => x.ActionCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ActionName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ActionCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });
        });



        builder.Entity<ReceiptTemplate>(entity =>
        {
            entity.ToTable("ReceiptTemplates");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PaperSize).HasMaxLength(20).IsRequired();
            entity.Property(x => x.BusinessName).HasMaxLength(160);
            entity.Property(x => x.Subtitle).HasMaxLength(160);
            entity.Property(x => x.LogoUrl).HasMaxLength(500);
            entity.Property(x => x.HeaderText).HasMaxLength(500);
            entity.Property(x => x.FooterText).HasMaxLength(500);
            entity.Property(x => x.ReturnPolicyText).HasMaxLength(600);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.IsDefault });
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PrintStationSetting>(entity =>
        {
            entity.ToTable("PrintStationSettings");
            entity.Property(x => x.StationName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.DefaultReceiptMode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReceiptPrinterName).HasMaxLength(160);
            entity.Property(x => x.LabelPrinterName).HasMaxLength(160);
            entity.Property(x => x.LocalPrintAgentUrl).HasMaxLength(250);
            entity.Property(x => x.CashDrawerCommandHex).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.RegisterId }).IsUnique();
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Register).WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReceiptTemplate).WithMany().HasForeignKey(x => x.ReceiptTemplateId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.AddressLine).HasMaxLength(300);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TenantId, x.Phone });
        });

        builder.Entity<CustomerLedger>(entity =>
        {
            entity.ToTable("CustomerLedgers");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.CustomerId, x.EntryDate });
            entity.HasOne(x => x.Customer).WithMany(x => x.Ledger).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Shift>(entity =>
        {
            entity.ToTable("Shifts");
            entity.Property(x => x.ShiftNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.OpenedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ClosedByUserId).HasMaxLength(450);
            entity.Property(x => x.OpeningFloat).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            entity.Property(x => x.CountedCash).HasPrecision(18, 2);
            entity.Property(x => x.Variance).HasPrecision(18, 2);
            entity.Property(x => x.OpeningNotes).HasMaxLength(500);
            entity.Property(x => x.ClosingNotes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.ShiftNumber }).IsUnique();
            entity.HasIndex(x => new { x.RegisterId, x.Status });
            entity.HasIndex(x => x.OpenedOn);
            entity.HasOne(x => x.Register).WithMany().HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashMovement>(entity =>
        {
            entity.ToTable("CashMovements");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.ShiftId, x.OccurredOn });
            entity.HasOne(x => x.Shift).WithMany(x => x.Movements).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CashDenominationCount>(entity =>
        {
            entity.ToTable("CashDenominationCounts");
            entity.Property(x => x.DenominationValue).HasPrecision(18, 2);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ShiftId, x.DenominationValue }).IsUnique();
            entity.HasOne(x => x.Shift).WithMany(x => x.CashDenominationCounts).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SaleReturn>(entity =>
        {
            entity.ToTable("SaleReturns");
            entity.Property(x => x.ReturnNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ProcessedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.ReturnReason).HasMaxLength(300).IsRequired();
            entity.Property(x => x.RefundReference).HasMaxLength(120);
            entity.Property(x => x.ExchangeReference).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique();
            entity.HasIndex(x => x.ProcessedOn);
            entity.HasIndex(x => new { x.OriginalSaleId, x.ProcessedOn });
            entity.HasOne(x => x.OriginalSale).WithMany().HasForeignKey(x => x.OriginalSaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleReturnLine>(entity =>
        {
            entity.ToTable("SaleReturnLines");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.QuantityReturned).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.Property(x => x.Disposition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.LineReason).HasMaxLength(300);
            entity.HasIndex(x => new { x.SaleReturnId, x.LineNumber }).IsUnique();
            entity.HasOne(x => x.SaleReturn).WithMany(x => x.Lines).HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OriginalSaleLine).WithMany().HasForeignKey(x => x.OriginalSaleLineId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.AddressLine).HasMaxLength(300);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.TaxNumber).HasMaxLength(50);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Name });
        });

        builder.Entity<Purchase>(entity =>
        {
            entity.ToTable("Purchases");
            entity.Property(x => x.PurchaseNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReceivedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.PurchaseNumber }).IsUnique();
            entity.HasIndex(x => x.ReceivedOn);
            entity.HasIndex(x => new { x.StoreId, x.ReceivedOn });
            entity.HasOne(x => x.Supplier).WithMany(x => x.Purchases).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseLine>(entity =>
        {
            entity.ToTable("PurchaseLines");
            entity.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.PurchaseId, x.LineNumber }).IsUnique();
            entity.HasOne(x => x.Purchase).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NumberSequence>(entity =>
        {
            entity.ToTable("NumberSequences");
            entity.Property(x => x.SequenceType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SequenceDate).HasColumnType("date").IsRequired();
            entity.Property(x => x.Prefix).HasMaxLength(60).IsRequired();
            entity.Property(x => x.LastGeneratedNumber).HasMaxLength(80);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.SequenceType, x.SequenceDate }).IsUnique();
            entity.HasIndex(x => new { x.StoreId, x.SequenceType, x.SequenceDate });
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.UserName).HasMaxLength(256);
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.CorrelationId).HasMaxLength(64);
            entity.HasIndex(x => x.CreatedOn);
            entity.HasIndex(x => new { x.TenantId, x.CreatedOn });
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.CreatedOn });
            entity.HasIndex(x => new { x.TenantId, x.EntityName, x.EntityId });
            entity.HasIndex(x => new { x.TenantId, x.Action, x.CreatedOn });
        });

        builder.Entity<ManagerApprovalLog>(entity =>
        {
            entity.ToTable("ManagerApprovalLogs");
            entity.Property(x => x.ApprovalType).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.RequestedByUserId).HasMaxLength(450);
            entity.Property(x => x.RequestedByUserName).HasMaxLength(256);
            entity.Property(x => x.ApprovedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.ApprovedByUserName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => x.ApprovedOn);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.ApprovedOn });
            entity.HasIndex(x => new { x.TenantId, x.ApprovalType, x.ApprovedOn });
        });

        builder.Entity<ErrorLog>(entity =>
        {
            entity.ToTable("ErrorLogs");
            entity.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TraceId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ErrorCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.OccurredOn);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.TenantId);
        });

        ApplyTenantFilters(builder);
    }

    private void ApplyTenantFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [builder]);
        }
    }

    // Combines tenant isolation with soft-delete into a single query filter so
    // neither is silently replaced (EF Core treats unnamed filters as one-per-entity).
    private void SetTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantEntity
    {
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Entity<TEntity>().HasQueryFilter(entity =>
                (tenantContext.TenantId == null || entity.TenantId == tenantContext.TenantId)
                && !((ISoftDelete)entity).IsDeleted);
        }
        else
        {
            builder.Entity<TEntity>().HasQueryFilter(entity =>
                tenantContext.TenantId == null || entity.TenantId == tenantContext.TenantId);
        }
    }
}
