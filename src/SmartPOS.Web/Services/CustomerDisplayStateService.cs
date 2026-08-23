using System.Collections.Concurrent;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Web.Services;

public sealed class CustomerDisplayStateService
{
    private readonly ConcurrentDictionary<Guid, CustomerDisplayStateDto> _states = new();

    public event Action<Guid>? StateChanged;

    public CustomerDisplayStateDto Get(Guid registerId)
    {
        if (_states.TryGetValue(registerId, out var state))
        {
            return state;
        }

        return new CustomerDisplayStateDto
        {
            RegisterId = registerId,
            Status = "Waiting for POS",
            LastUpdatedOn = DateTime.UtcNow
        };
    }

    public void Set(CustomerDisplayStateDto state)
    {
        state.LastUpdatedOn = DateTime.UtcNow;
        _states[state.RegisterId] = state;
        StateChanged?.Invoke(state.RegisterId);
    }

    public void Clear(Guid registerId, string storeName, string registerName)
    {
        Set(new CustomerDisplayStateDto
        {
            RegisterId = registerId,
            StoreName = storeName,
            RegisterName = registerName,
            CustomerName = "Walk-In Customer",
            Status = "Ready"
        });
    }
}
