using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// A point-of-sale terminal / cash register that belongs to a single store.
/// </summary>
public class Register : StoreScopedEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? PrinterName { get; set; }
    public bool HasCashDrawer { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public Store? Store { get; set; }
}
