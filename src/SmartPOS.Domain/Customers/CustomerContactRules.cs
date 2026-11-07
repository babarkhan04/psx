using System.Security.Claims;
using SmartPOS.Domain.Authorization;
namespace SmartPOS.Domain.Customers;

public static class CustomerContactRules
{
    public static string? NormalizePhone(string? input, string countryCode = "92")
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (countryCode.Length is < 1 or > 3 || countryCode.Any(c => !char.IsAsciiDigit(c))) return null;
        var trimmed = input.Trim();
        if (trimmed.Count(c => c == '+') > 1 || (trimmed.Contains('+') && !trimmed.StartsWith('+'))) return null;
        if (trimmed.Any(c => !char.IsAsciiDigit(c) && !"+ ()-./".Contains(c))) return null;
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00")) digits = digits[2..];
        else if (digits.StartsWith('0')) digits = countryCode + digits[1..];
        else if (!trimmed.StartsWith('+') && !digits.StartsWith(countryCode) && digits.Length == 10) digits = countryCode + digits;
        return digits.Length is >= 8 and <= 15 && !digits.StartsWith('0') ? "+" + digits : null;
    }
    public static DateTime BirthdayInYear(DateTime birthDate, int year) =>
        new(year, birthDate.Month, Math.Min(birthDate.Day, DateTime.DaysInMonth(year, birthDate.Month)));
    public static DateTime NextBirthday(DateTime birthDate, DateTime today)
    {
        var date = BirthdayInYear(birthDate, today.Year);
        return date >= today.Date ? date : BirthdayInYear(birthDate, today.Year + 1);
    }
    public static bool CanRead(ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true && user.HasClaim(Permissions.ClaimType, Permissions.Relationships.CustomersView);
    public static bool CanManage(ClaimsPrincipal? user) => CanRead(user) && user!.HasClaim(Permissions.ClaimType, Permissions.Relationships.CustomersManage);
    public static bool CanViewBirthdays(ClaimsPrincipal? user) => CanRead(user) &&
        (user!.IsInRole(DefaultRolePermissions.SuperAdmin) || user.IsInRole(DefaultRolePermissions.BusinessOwner) || user.IsInRole(DefaultRolePermissions.StoreManager));
    public static bool CanSendBirthdays(ClaimsPrincipal? user) => CanViewBirthdays(user) && CanManage(user);
}
