using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class Brand : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
