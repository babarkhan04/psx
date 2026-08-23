namespace SmartPOS.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class AuditableEntity : BaseEntity, IAuditableEntity, ISoftDelete, ITenantEntity
{
    public Guid TenantId { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public abstract class StoreScopedEntity : AuditableEntity, IStoreScopedEntity
{
    public Guid StoreId { get; set; }
}
