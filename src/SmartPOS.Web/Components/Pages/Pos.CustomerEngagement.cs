using Microsoft.AspNetCore.Components;
using SmartPOS.Shared.Contracts;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Web.Components.Shared;
using Radzen;

namespace SmartPOS.Web.Components.Pages;

public partial class Pos
{
    [Inject] private ICustomerEngagementService CustomerEngagement { get; set; } = default!;
    private bool _canUseCustomerForm, _canSeeBirthdays, _selectedCustomerBirthday;
    private async Task OpenCustomerFormAsync()
    {
        if (_busy || !_canUseCustomerForm) return;
        _busy = true;
        try
        {
            var result = await DialogService.OpenAsync<PosCustomerDialog>("Customer details & history",
                new Dictionary<string, object?> { [nameof(PosCustomerDialog.CustomerId)] = _selectedCustomerId },
                new DialogOptions { Width = "900px", CloseDialogOnOverlayClick = false, CloseDialogOnEsc = false, ShowClose = false });
            if (result is PosCustomerSelectionDto selection) await ApplyCustomerSelectionAsync(selection);
        }
        catch (Exception ex) { Logger.LogError(ex, "Unable to open customer details"); NotifyWarn("Customer details", "Customer details could not be opened. You can continue billing without them."); }
        finally { _busy = false; await FocusSearch(); }
    }
    private Task ApplyCustomerSelectionAsync(PosCustomerSelectionDto selection)
    {
        _selectedCustomer = selection.WalkIn ? null : selection.Customer;
        _selectedCustomerId = _selectedCustomer?.Id;
        _selectedCustomerBirthday = !selection.WalkIn && selection.IsBirthdayToday;
        _customerName = _selectedCustomer?.Name ?? "Walk-In Customer";
        if (_selectedCustomer is not null)
        {
            _customers.RemoveAll(c => c.Id == _selectedCustomer.Id);
            _customers.Insert(0, _selectedCustomer);
        }
        UpdateCustomerDisplay();
        return Task.CompletedTask;
    }
    private async Task OpenBirthdaysAsync()
    {
        if (_busy || !_canSeeBirthdays) return;
        _busy = true;
        try { await DialogService.OpenAsync<CustomerBirthdaysDialog>("Customer birthdays", null, new DialogOptions { Width = "1000px", CloseDialogOnOverlayClick = false }); }
        catch (Exception ex) { Logger.LogError(ex, "Unable to open customer birthdays"); NotifyWarn("Customer birthdays", "Birthday alerts could not be opened. Please try again."); }
        finally { _busy = false; await FocusSearch(); }
    }
    private Dictionary<string, object?> CashCustomerParameters() => new()
    {
        [nameof(PosCashDialog.Total)] = CartTotal,
        [nameof(PosCashDialog.CustomerId)] = _selectedCustomerId,
        [nameof(PosCashDialog.CustomerName)] = _customerName,
        [nameof(PosCashDialog.CustomerChanged)] = EventCallback.Factory.Create<PosCustomerSelectionDto>(this, ApplyCustomerSelectionAsync)
    };
    private Dictionary<string, object?> SplitCustomerParameters() => new()
    {
        [nameof(PosSplitPaymentDialog.Total)] = CartTotal,
        [nameof(PosSplitPaymentDialog.CustomerId)] = _selectedCustomerId,
        [nameof(PosSplitPaymentDialog.CustomerName)] = _customerName,
        [nameof(PosSplitPaymentDialog.CustomerChanged)] = EventCallback.Factory.Create<PosCustomerSelectionDto>(this, ApplyCustomerSelectionAsync)
    };
}
