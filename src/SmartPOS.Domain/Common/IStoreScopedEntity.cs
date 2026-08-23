namespace SmartPOS.Domain.Common;

public interface IStoreScopedEntity : ITenantEntity
{
    Guid StoreId { get; set; }
}
