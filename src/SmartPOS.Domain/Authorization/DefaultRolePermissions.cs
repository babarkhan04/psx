namespace SmartPOS.Domain.Authorization;

/// <summary>
/// Default permission grants applied to the built-in roles when the system is
/// first seeded. Administrators can later refine these from the Roles screen.
/// </summary>
public static class DefaultRolePermissions
{
    public const string SuperAdmin = "Super Admin";
    public const string BusinessOwner = "Business Owner";
    public const string StoreManager = "Store Manager";
    public const string Cashier = "Cashier";

    public static IReadOnlyList<string> AllRoles { get; } =
        [SuperAdmin, BusinessOwner, StoreManager, Cashier];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Map { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            // Super Admin & Business Owner receive every permission.
            [SuperAdmin] = Permissions.All.Select(p => p.Key).ToList(),
            [BusinessOwner] = Permissions.All.Select(p => p.Key).ToList(),

            [StoreManager] =
            [
                Permissions.Setup.StoresView,
                Permissions.Setup.RegistersView,
                Permissions.Setup.RegistersManage,
                Permissions.Setup.UsersView,
                Permissions.Setup.ShortcutsView,
                Permissions.Setup.ShortcutsManage,
                Permissions.Setup.PrintingView,
                Permissions.Setup.PrintingManage,

                Permissions.Sales.Operate,
                Permissions.Sales.View,
                Permissions.Sales.Refund,
                Permissions.Sales.Void,
                Permissions.Sales.Discount,
                Permissions.Sales.ManagerApproval,

                Permissions.Catalog.ProductsView,
                Permissions.Catalog.ProductsManage,
                Permissions.Catalog.InventoryView,
                Permissions.Catalog.InventoryManage,
                Permissions.Catalog.PurchasesView,
                Permissions.Catalog.PurchasesManage,
                Permissions.Catalog.TransfersView,
                Permissions.Catalog.TransfersManage,
                Permissions.Catalog.ReorderView,
                Permissions.Catalog.ReorderManage,
                Permissions.Catalog.LabelsView,
                Permissions.Catalog.LabelsManage,

                Permissions.Relationships.CustomersView,
                Permissions.Relationships.CustomersManage,
                Permissions.Relationships.SuppliersView,
                Permissions.Relationships.SuppliersManage,

                Permissions.Operations.ShiftsView,
                Permissions.Operations.ShiftsOpen,
                Permissions.Operations.ShiftsClose,
                Permissions.Operations.ShiftsCashMove,

                Permissions.Insights.ReportsView,
                Permissions.Insights.AlertsView,
                Permissions.Insights.RiskView,
                Permissions.Insights.AuditView,
            ],

            [Cashier] =
            [
                Permissions.Sales.Operate,
                Permissions.Sales.View,
                Permissions.Sales.Discount,
                Permissions.Catalog.ProductsView,
                Permissions.Catalog.InventoryView,
                Permissions.Relationships.CustomersView,
                Permissions.Relationships.CustomersManage,
                Permissions.Operations.ShiftsView,
                Permissions.Operations.ShiftsOpen,
                Permissions.Operations.ShiftsClose,
                Permissions.Operations.ShiftsCashMove,
            ],
        };
}
