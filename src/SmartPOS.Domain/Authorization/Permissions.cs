namespace SmartPOS.Domain.Authorization;

/// <summary>
/// Central catalog of fine-grained permissions. Permissions are persisted as
/// role claims (claim type <see cref="ClaimType"/>) and enforced via policies,
/// so nothing about access control is hardcoded in the UI or endpoints.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Setup
    {
        public const string CompanyView = "setup.company.view";
        public const string CompanyManage = "setup.company.manage";
        public const string StoresView = "setup.stores.view";
        public const string StoresManage = "setup.stores.manage";
        public const string RegistersView = "setup.registers.view";
        public const string RegistersManage = "setup.registers.manage";
        public const string UsersView = "setup.users.view";
        public const string UsersManage = "setup.users.manage";
        public const string RolesView = "setup.roles.view";
        public const string RolesManage = "setup.roles.manage";
        public const string ShortcutsView = "setup.shortcuts.view";
        public const string ShortcutsManage = "setup.shortcuts.manage";
        public const string PrintingView = "setup.printing.view";
        public const string PrintingManage = "setup.printing.manage";
    }

    public static class Sales
    {
        public const string Operate = "sales.pos.operate";
        public const string View = "sales.view";
        public const string Refund = "sales.refund";
        public const string Void = "sales.void";
        public const string Discount = "sales.discount";
        public const string ManagerApproval = "sales.managerapproval";
    }

    public static class Catalog
    {
        public const string ProductsView = "catalog.products.view";
        public const string ProductsManage = "catalog.products.manage";
        public const string InventoryView = "catalog.inventory.view";
        public const string InventoryManage = "catalog.inventory.manage";
        public const string PurchasesView = "catalog.purchases.view";
        public const string PurchasesManage = "catalog.purchases.manage";
        public const string TransfersView = "catalog.transfers.view";
        public const string TransfersManage = "catalog.transfers.manage";
        public const string ReorderView = "catalog.reorder.view";
        public const string ReorderManage = "catalog.reorder.manage";
        public const string LabelsView = "catalog.labels.view";
        public const string LabelsManage = "catalog.labels.manage";
    }

    public static class Relationships
    {
        public const string CustomersView = "relationships.customers.view";
        public const string CustomersManage = "relationships.customers.manage";
        public const string SuppliersView = "relationships.suppliers.view";
        public const string SuppliersManage = "relationships.suppliers.manage";
    }

    public static class Operations
    {
        public const string ShiftsView = "operations.shifts.view";
        public const string ShiftsOpen = "operations.shifts.open";
        public const string ShiftsClose = "operations.shifts.close";
        public const string ShiftsCashMove = "operations.shifts.cashmove";
    }

    public static class Insights
    {
        public const string ReportsView = "insights.reports.view";
        public const string AlertsView = "insights.alerts.view";
        public const string RiskView = "insights.risk.view";
        public const string AuditView = "insights.audit.view";
    }

    /// <summary>Returns every permission key defined in the catalog.</summary>
    public static IReadOnlyList<PermissionDescriptor> All { get; } = BuildCatalog();

    private static IReadOnlyList<PermissionDescriptor> BuildCatalog() =>
    [
        new("Setup", Setup.CompanyView, "View company profile"),
        new("Setup", Setup.CompanyManage, "Manage company profile"),
        new("Setup", Setup.StoresView, "View stores"),
        new("Setup", Setup.StoresManage, "Manage stores"),
        new("Setup", Setup.RegistersView, "View registers"),
        new("Setup", Setup.RegistersManage, "Manage registers"),
        new("Setup", Setup.UsersView, "View users"),
        new("Setup", Setup.UsersManage, "Manage users"),
        new("Setup", Setup.RolesView, "View roles"),
        new("Setup", Setup.RolesManage, "Manage roles & permissions"),
        new("Setup", Setup.ShortcutsView, "View POS shortcut settings"),
        new("Setup", Setup.ShortcutsManage, "Manage POS shortcut settings"),
        new("Setup", Setup.PrintingView, "View receipt and print settings"),
        new("Setup", Setup.PrintingManage, "Manage receipt designer and print station settings"),

        new("Sales", Sales.Operate, "Operate point of sale"),
        new("Sales", Sales.View, "View sales & returns"),
        new("Sales", Sales.Refund, "Process refunds/returns"),
        new("Sales", Sales.Void, "Void completed sales"),
        new("Sales", Sales.Discount, "Apply POS discounts"),
        new("Sales", Sales.ManagerApproval, "Approve restricted POS actions"),

        new("Catalog & Stock", Catalog.ProductsView, "View products"),
        new("Catalog & Stock", Catalog.ProductsManage, "Manage products"),
        new("Catalog & Stock", Catalog.InventoryView, "View inventory"),
        new("Catalog & Stock", Catalog.InventoryManage, "Manage inventory"),
        new("Catalog & Stock", Catalog.PurchasesView, "View purchases"),
        new("Catalog & Stock", Catalog.PurchasesManage, "Manage purchases & GRN"),
        new("Catalog & Stock", Catalog.TransfersView, "View stock transfers"),
        new("Catalog & Stock", Catalog.TransfersManage, "Create/approve/dispatch/receive stock transfers"),
        new("Catalog & Stock", Catalog.ReorderView, "View smart reorder suggestions"),
        new("Catalog & Stock", Catalog.ReorderManage, "Create purchase drafts and transfer requests from smart reorder"),
        new("Catalog & Stock", Catalog.LabelsView, "View barcode and shelf label designer"),
        new("Catalog & Stock", Catalog.LabelsManage, "Print barcode and shelf labels"),

        new("Relationships", Relationships.CustomersView, "View customers"),
        new("Relationships", Relationships.CustomersManage, "Manage customers & credit"),
        new("Relationships", Relationships.SuppliersView, "View suppliers"),
        new("Relationships", Relationships.SuppliersManage, "Manage suppliers"),

        new("Operations", Operations.ShiftsView, "View shifts & cash"),
        new("Operations", Operations.ShiftsOpen, "Open shifts"),
        new("Operations", Operations.ShiftsClose, "Close shifts"),
        new("Operations", Operations.ShiftsCashMove, "Record cash in/out"),

        new("Insights", Insights.ReportsView, "View reports"),
        new("Insights", Insights.AlertsView, "View smart alerts"),
        new("Insights", Insights.RiskView, "View cashier risk center"),
        new("Insights", Insights.AuditView, "View audit trail"),
    ];
}

public sealed record PermissionDescriptor(string Group, string Key, string Description);
