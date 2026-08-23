using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class TaxCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Tax rate as a percentage, e.g. 15.00 for 15%.</summary>
    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = [];
}
