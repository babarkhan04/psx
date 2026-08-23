using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Infrastructure.Authorization;
using SmartPOS.Infrastructure.ExceptionHandling;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Persistence.Interceptors;
using SmartPOS.Infrastructure.Services;
using SmartPOS.Infrastructure.Services.Management;

namespace SmartPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplication();
        services.AddHttpContextAccessor();

        services.AddScoped<IUserContextAccessor, UserContextAccessor>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IStoreContext, StoreContext>();
        services.AddScoped<IStoreAccessService, StoreAccessService>();
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IErrorLogService, ErrorLogService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IManagerApprovalService, ManagerApprovalService>();
        services.AddScoped<DbContextGate>();

        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ITaxCategoryService, TaxCategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImportExportService, ProductImportExportService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<IPosShortcutService, PosShortcutService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IReorderService, ReorderService>();
        services.AddScoped<IPrintSettingsService, PrintSettingsService>();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<TenantEntityInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3));

            options.AddInterceptors(
                provider.GetRequiredService<AuditableEntityInterceptor>(),
                provider.GetRequiredService<SoftDeleteInterceptor>(),
                provider.GetRequiredService<TenantEntityInterceptor>(),
                provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
