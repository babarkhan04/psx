namespace SmartPOS.Application.Common.Options;

public sealed class CustomerEngagementOptions
{
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string CountryCallingCode { get; set; } = "92";
    public bool ProcessEmailQueue { get; set; } = true;
    public bool AutomaticBirthdayEmails { get; set; }
    public int AutomaticSendHour { get; set; } = 9;
    public string BirthdaySubject { get; set; } = "Happy birthday, {CustomerName}!";
    public string BirthdayMessage { get; set; } = "Dear {CustomerName},\n\nHappy birthday! Wishing you a wonderful day and a fantastic year ahead.\n\nWarm wishes,\n{BusinessName}";
    public CustomerSmtpOptions Smtp { get; set; } = new();
    public bool EmailConfigured => Smtp.Enabled && !string.IsNullOrWhiteSpace(Smtp.Host) && !string.IsNullOrWhiteSpace(Smtp.FromAddress) && Smtp.Port is > 0 and <= 65535;
}
public sealed class CustomerSmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SmartPOS";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
