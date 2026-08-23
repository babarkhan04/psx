namespace SmartPOS.Shared.Contracts;

public sealed class PosShortcutSettingDto
{
    public string ActionCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AllowWhenTyping { get; set; }
    public bool IsCartShortcut { get; set; }
    public int SortOrder { get; set; }
    public string ShortcutText { get; set; } = string.Empty;
}

public sealed class UpdatePosShortcutDto
{
    public string ActionCode { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool IsEnabled { get; set; } = true;
}
