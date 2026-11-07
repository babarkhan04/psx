using System.Reflection;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Authorization;
using SmartPOS.Shared.Contracts;
using SmartPOS.Web.Services;
using SmartPOS.Web.Components.Pages;
using SmartPOS.Web.Components.Shared;
namespace SmartPOS.UiTests;

public sealed class CustomerEngagementUiTests
{
    private static BunitContext Create()
    {
        var c = new BunitContext();
        c.JSInterop.Mode = JSRuntimeMode.Loose;
        c.JSInterop.SetupModule("./pos-keyboard.js").Mode = JSRuntimeMode.Loose;
        c.Services.AddRadzenComponents();
        c.Services.AddAuthorization(); c.Services.AddCascadingAuthenticationState();
        c.Services.AddScoped<AuthenticationStateProvider, PreviewAuth>();
        c.Services.AddSingleton<PreviewState>();
        c.Services.AddSingleton<CustomerDisplayStateService>();
        foreach(var type in new[] {typeof(IPosService),typeof(ICustomerEngagementService),typeof(ICustomerService),typeof(IStoreService),typeof(IStoreContext),typeof(IPosShortcutService),typeof(IShiftService),typeof(IPrintSettingsService)})
            c.Services.AddScoped(type, sp => {var p=(FakeApi)DispatchProxy.Create(type,typeof(FakeApi));p.State=sp.GetRequiredService<PreviewState>();return p;});
        return c;
    }
    private static void Open(IRenderedComponent<CustomerFlowHost> cut) => cut.Find(".pos-customer-actions [data-pos-customer-action]").Click();

    [Fact]
    public void Walk_in_sale_starts_without_a_customer_form()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>();
        cut.WaitForAssertion(()=>Assert.Contains("Optional",cut.Find(".pos-customer-note").TextContent));
        Assert.Empty(cut.FindAll(".customer-workspace"));
    }
    [Fact]
    public void Opening_and_cancelling_customer_form_preserves_cart()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>();
        cut.Find(".pos-product-tile").Click();
        var before=cut.Find(".pos-cart-panel").TextContent;
        Open(cut); cut.WaitForAssertion(()=>Assert.Single(cut.FindAll(".customer-workspace")));
        cut.FindAll(".customer-footer button").Single(b=>b.TextContent.Contains("Cancel")).Click();
        cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll(".customer-workspace")));
        Assert.Equal(before,cut.Find(".pos-cart-panel").TextContent);
    }
    [Fact]
    public void Phone_lookup_shows_history_and_saved_bill_without_changing_cart()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>();
        cut.Find(".pos-product-tile").Click(); var before=cut.Find(".pos-cart-panel").TextContent;
        Open(cut); cut.Find("[data-pos-customer-search]").Input("03001234567");
        cut.FindAll(".customer-search button").First().Click();
        cut.WaitForAssertion(()=>Assert.Contains("INV-HISTORY-001",cut.Find(".customer-history").TextContent));
        cut.FindAll(".customer-history button").Single(b=>b.TextContent.Contains("INV-HISTORY-001")).Click();
        cut.WaitForAssertion(()=>Assert.Contains("Original item",cut.Find(".sp-receipt-dialog-shell").TextContent));
        Assert.Equal(before,cut.Find(".pos-cart-panel").TextContent);
    }
    [Fact]
    public void Shared_phone_lookup_requires_selecting_a_profile()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find("[data-pos-customer-search]").Input("03009999999"); cut.FindAll(".customer-search button").First().Click();
        cut.WaitForAssertion(()=>Assert.Equal(2,cut.FindAll(".customer-matches button").Count));
        Assert.Empty(cut.FindAll(".customer-history"));
    }
    [Fact]
    public void Unmatched_phone_prefills_new_customer_and_walk_in_remains_available()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find("[data-pos-customer-search]").Input("03005555555"); cut.FindAll(".customer-search button").First().Click();
        cut.WaitForAssertion(()=>Assert.Equal("03005555555",cut.Find("input[name=CustomerMobile]").GetAttribute("value")));
        cut.FindAll(".customer-footer button").First().Click();
        cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll(".customer-workspace")));
        Assert.Contains("Optional",cut.Find(".pos-customer-note").TextContent);
    }
    [Fact]
    public void Saved_customer_selection_shows_birthday_and_survives_payment_dialog()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find("[data-pos-customer-search]").Input("03001234567");cut.FindAll(".customer-search button").First().Click();
        cut.FindAll(".customer-form-actions button").First().Click();
        cut.WaitForAssertion(()=>Assert.Contains("Ayesha Test",cut.Find(".pos-customer-note").TextContent));
        Assert.Contains("Birthday today",cut.Find(".pos-birthday-note").TextContent);
        cut.Find(".pos-product-tile").Click();
        cut.FindAll(".pos-pay-btn").First(b=>b.TextContent.Contains("Cash")).Click();
        cut.WaitForAssertion(()=>Assert.Contains("Ayesha Test",cut.Find(".payment-customer-action").TextContent));
    }
    [Fact]
    public void Name_and_phone_are_sufficient_to_register_an_optional_customer()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find("input[name=CustomerName]").Change("New visitor");
        cut.Find("input[name=CustomerMobile]").Change("03005555555");
        cut.Find(".customer-workspace form").Submit();
        cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll(".customer-workspace")));
        Assert.Contains("New visitor",cut.Find(".pos-customer-note").TextContent);
        var saved=c.Services.GetRequiredService<PreviewState>().Profiles.Single(p=>p.Name=="New visitor");
        Assert.Null(saved.DateOfBirth);Assert.Null(saved.Email);Assert.False(saved.BirthdayEmailOptIn);
        Assert.Equal(saved.Phone,saved.WhatsAppNumber);
    }
    [Fact]
    public void Empty_customer_form_does_not_force_registration()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find(".customer-workspace form").Submit();
        Assert.Equal(3,c.Services.GetRequiredService<PreviewState>().Profiles.Count);
        cut.WaitForAssertion(()=>Assert.Contains("required",cut.Find(".customer-workspace").TextContent,StringComparison.OrdinalIgnoreCase));
        cut.FindAll(".customer-footer button").First().Click();
        cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll(".customer-workspace")));
    }
    [Fact]
    public void Walk_in_action_removes_selected_customer_and_preserves_items()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>(); Open(cut);
        cut.Find("[data-pos-customer-search]").Input("03001234567");cut.FindAll(".customer-search button").First().Click();
        cut.FindAll(".customer-form-actions button").First().Click();cut.Find(".pos-product-tile").Click();
        var itemText=cut.Find(".pos-cart-panel").TextContent;
        Open(cut);cut.FindAll(".customer-footer button").First().Click();
        cut.WaitForAssertion(()=>Assert.Contains("Optional",cut.Find(".pos-customer-note").TextContent));
        Assert.Empty(cut.FindAll(".pos-birthday-note"));
        Assert.Contains("Test product",cut.Find(".pos-cart-panel").TextContent);
    }
    [Fact]
    public void Changing_customer_during_cash_payment_preserves_amount_tendered()
    {
        using var c=Create(); var cut=c.Render<CustomerFlowHost>();
        cut.Find(".pos-product-tile").Click();cut.FindAll(".pos-pay-btn").First(b=>b.TextContent.Contains("Cash")).Click();
        var cash=cut.FindComponent<PosCashDialog>();cash.Find("input").Change("150");
        cut.Find(".payment-customer-action").Click();cut.Find("[data-pos-customer-search]").Input("03001234567");cut.FindAll(".customer-search button").First().Click();
        cut.FindAll(".customer-form-actions button").First().Click();
        cut.WaitForAssertion(()=>Assert.Contains("Ayesha Test",cut.Find(".payment-customer-action").TextContent));
        Assert.EndsWith("150.00",cash.Find("input").GetAttribute("value"));
        Assert.Contains("50.00",cash.Find(".pos-pay-change").TextContent);
    }
}
public sealed class CustomerFlowHost : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b) { b.OpenComponent<Pos>(0);b.CloseComponent();b.OpenComponent<RadzenDialog>(1);b.CloseComponent(); }
}

public sealed class PreviewAuth : AuthenticationStateProvider
{
 public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
 Permissions.All.Select(p=>new Claim(Permissions.ClaimType,p.Key)).Append(new Claim(ClaimTypes.NameIdentifier,"preview")).Append(new Claim(ClaimTypes.Role,DefaultRolePermissions.BusinessOwner)),"preview"))));
}
public class PreviewState
{
 public Guid Store=Guid.NewGuid(),Register=Guid.NewGuid(), Product=Guid.NewGuid();
 public List<CustomerProfileDto> Profiles=[new(){Id=Guid.NewGuid(),Name="Ayesha Test",Phone="03001234567",Email="ayesha@example.invalid",DateOfBirth=new DateTime(1990,10,30),IsBirthdayToday=true},new(){Id=Guid.NewGuid(),Name="Shared One",Phone="03009999999"},new(){Id=Guid.NewGuid(),Name="Shared Two",Phone="03009999999"}];
 public Guid? Selected;
 public PosReceiptDto Receipt => new(){SaleId=Guid.NewGuid(),SaleNumber="INV-HISTORY-001",StoreId=Store,RegisterId=Register,StoreName="Preview Store",CustomerName="Historical customer",PaymentMethod="Cash",CompletedOn=DateTime.UtcNow,TotalAmount=500,Subtotal=500,AmountPaid=500,Lines=[new(){Name="Original item",Quantity=1,UnitPrice=500,LineTotal=500}]};
}
public class FakeApi : DispatchProxy
{
 public PreviewState State=null!;
 protected override object? Invoke(MethodInfo? method,object?[]? args)
 {
  var name=method!.Name; args ??=[];
  object? v = name switch {
   "get_StoreId"=>State.Store,"get_AllowedStoreIds"=>new[]{State.Store},
   "GetContextAsync" when method.DeclaringType==typeof(ICustomerEngagementService)=>new CustomerEngagementContextDto{CanManage=true,CanViewBirthdays=true,CanSendBirthdayEmails=true,BusinessDate=new DateTime(2026,10,30),BusinessName="Preview Store"},
   "GetContextAsync"=>new PosContextDto{StoreId=State.Store,RegisterId=State.Register,StoreName="Preview Store",RegisterName="Counter"},
   "GetAllAsync"=>new List<StoreDto>{new(){Id=State.Store,Name="Preview Store",Code="MAIN"}},
   "GetAsync"=>new List<PosShortcutSettingDto>(),
   "GetProductFilterLookupsAsync"=>new PosFilterLookupsDto(),
   "GetProductsAsync"=>new List<PosProductDto>{new(){ProductId=State.Product,Name="Test product",Sku="P001",UnitPrice=100,StockOnHand=100}},
   "SearchLookupAsync"=>State.Profiles.Select(p=>p.ToLookup()).ToList(),
   "FindAsync"=>State.Profiles.Where(p=>p.Name.Contains((string)args[0]!,StringComparison.OrdinalIgnoreCase)||p.Phone.Contains((string)args[0]!)).ToList(),
   "GetProfileAsync"=>State.Profiles.FirstOrDefault(p=>p.Id==(Guid)args[0]!),
   "SaveProfileAsync"=>Save((CustomerProfileDto)args[0]!),
   "GetHistoryAsync"=>new CustomerHistoryDto{TotalCount=1,NetSpend=500,Purchases=[new(){SaleId=Guid.NewGuid(),SaleNumber="INV-HISTORY-001",StoreName="Preview Store",CompletedOn=DateTime.UtcNow,PaymentMethods="Cash",Status="Completed",Total=500}]},
   "GetPurchaseReceiptAsync"=>State.Receipt,
   "GetReceiptTemplateAsync"=>new ReceiptTemplateDto(),
   "GetPrintStationAsync"=>new PrintStationSettingDto(),
   "GetBirthdaysAsync"=>new List<CustomerBirthdayDto>{new(){CustomerId=State.Profiles[0].Id,Name="Ayesha Test",Phone="03001234567",Birthday=new DateTime(2026,10,30),IsToday=true}},
   "CompleteSaleAsync"=>Complete((PosCheckoutDto)args[0]!),
   _=>null
  };
  if(method.ReturnType==typeof(void))return null;
  if(method.ReturnType==typeof(Task))return Task.CompletedTask;
  if(method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition()==typeof(Task<>)){
   var t=method.ReturnType.GenericTypeArguments[0];v??=t.IsValueType?Activator.CreateInstance(t):null;
   return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(t).Invoke(null,[v]);
  }
  return v;
 }
 Result<Guid> Save(CustomerProfileDto p){if(p.Id==Guid.Empty){p.Id=Guid.NewGuid();State.Profiles.Add(p);}return Result<Guid>.Success(p.Id);}
 Result<PosReceiptDto> Complete(PosCheckoutDto p){State.Selected=p.CustomerId;var r=State.Receipt;r.CustomerName=p.CustomerName;r.TotalAmount=p.Lines.Sum(l=>l.LineTotal);r.Subtotal=r.TotalAmount;r.AmountPaid=p.AmountTendered;return Result<PosReceiptDto>.Success(r);}
}
