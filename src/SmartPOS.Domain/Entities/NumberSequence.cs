using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class NumberSequence : StoreScopedEntity
{
    public string SequenceType { get; set; } = string.Empty;
    public DateTime SequenceDate { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public long CurrentNumber { get; set; }
    public int Padding { get; set; } = 4;
    public string? LastGeneratedNumber { get; set; }
    public DateTime? LastGeneratedOn { get; set; }
}
