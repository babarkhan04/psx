using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class UnitOfMeasure : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
