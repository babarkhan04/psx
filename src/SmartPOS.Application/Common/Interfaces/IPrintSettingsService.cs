using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IPrintSettingsService
{
    Task<ReceiptTemplateDto> GetReceiptTemplateAsync(Guid? storeId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptTemplateDto>> GetReceiptTemplatesAsync(Guid? storeId = null, CancellationToken ct = default);
    Task<Result<Guid>> SaveReceiptTemplateAsync(UpdateReceiptTemplateDto dto, CancellationToken ct = default);

    Task<PrintStationSettingDto> GetPrintStationAsync(Guid storeId, Guid? registerId = null, CancellationToken ct = default);
    Task<Result<Guid>> SavePrintStationAsync(UpdatePrintStationSettingDto dto, CancellationToken ct = default);
}
