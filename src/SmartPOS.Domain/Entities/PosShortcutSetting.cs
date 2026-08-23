using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class PosShortcutSetting : AuditableEntity
{
    public string ActionCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}
