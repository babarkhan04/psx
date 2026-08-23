using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class StockTransferService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    INumberSequenceService numberSequence,
    DbContextGate gate,
    IStoreAccessService storeAccess) : IStockTransferService
{
    public Task<IReadOnlyList<StockTransferListItemDto>> GetTransfersAsync(StockTransferQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetTransfersCoreAsync(query, ct), ct);

    public Task<StockTransferDetailDto?> GetTransferAsync(Guid transferId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetTransferCoreAsync(transferId, ct), ct);

    public Task<Result<Guid>> CreateRequestedAsync(CreateStockTransferDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CreateRequestedCoreAsync(dto, ct), ct);

    public Task<Result> ApproveAsync(StockTransferActionDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ApproveCoreAsync(dto, ct), ct);

    public Task<Result> DispatchAsync(StockTransferActionDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => DispatchCoreAsync(dto, ct), ct);

    public Task<Result> ReceiveAsync(ReceiveStockTransferDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ReceiveCoreAsync(dto, ct), ct);

    public Task<Result> CancelAsync(StockTransferActionDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CancelCoreAsync(dto, ct), ct);

    private async Task<IReadOnlyList<StockTransferListItemDto>> GetTransfersCoreAsync(
        StockTransferQueryDto query,
        CancellationToken ct)
    {
        var allowedStoreIds = await storeAccess.GetAllowedStoreIdsAsync(ct);
        if (allowedStoreIds.Count == 0)
        {
            return [];
        }

        var transfers = db.StockTransfers.AsNoTracking()
            .Include(t => t.SourceStore)
            .Include(t => t.DestinationStore)
            .Include(t => t.Lines)
            .Where(t => allowedStoreIds.Contains(t.StoreId) || allowedStoreIds.Contains(t.DestinationStoreId))
            .AsQueryable();

        if (query.StoreId is { } storeId && storeId != Guid.Empty)
        {
            if (!allowedStoreIds.Contains(storeId))
            {
                return [];
            }

            transfers = transfers.Where(t => t.StoreId == storeId || t.DestinationStoreId == storeId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<StockTransferStatus>(query.Status, true, out var parsedStatus))
        {
            transfers = transfers.Where(t => t.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            transfers = transfers.Where(t =>
                EF.Functions.Like(t.TransferNumber, $"%{term}%") ||
                (t.Notes != null && EF.Functions.Like(t.Notes, $"%{term}%")));
        }

        return await transfers
            .OrderByDescending(t => t.RequestedOn)
            .Take(query.Take <= 0 ? 100 : query.Take)
            .Select(t => new StockTransferListItemDto
            {
                Id = t.Id,
                TransferNumber = t.TransferNumber,
                SourceStoreName = t.SourceStore == null ? string.Empty : t.SourceStore.Code + " · " + t.SourceStore.Name,
                DestinationStoreName = t.DestinationStore == null ? string.Empty : t.DestinationStore.Code + " · " + t.DestinationStore.Name,
                Status = t.Status.ToString(),
                RequestedOn = t.RequestedOn,
                Notes = t.Notes,
                LineCount = t.Lines.Count,
                TotalRequested = t.Lines.Sum(l => l.QuantityRequested),
                TotalDispatched = t.Lines.Sum(l => l.QuantityDispatched),
                TotalReceived = t.Lines.Sum(l => l.QuantityReceived)
            })
            .ToListAsync(ct);
    }

    private async Task<StockTransferDetailDto?> GetTransferCoreAsync(Guid transferId, CancellationToken ct)
    {
        var transfer = await db.StockTransfers.AsNoTracking()
            .Include(t => t.SourceStore)
            .Include(t => t.DestinationStore)
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer is null || !await CanAccessTransferAsync(transfer.StoreId, transfer.DestinationStoreId, ct))
        {
            return null;
        }

        return MapDetail(transfer);
    }

    private async Task<Result<Guid>> CreateRequestedCoreAsync(CreateStockTransferDto dto, CancellationToken ct)
    {
        if (dto.SourceStoreId == Guid.Empty || dto.DestinationStoreId == Guid.Empty)
        {
            return Result<Guid>.Failure("Source and destination stores are required.", "STORE_REQUIRED");
        }

        if (dto.SourceStoreId == dto.DestinationStoreId)
        {
            return Result<Guid>.Failure("Source and destination stores cannot be the same.", "SAME_STORE");
        }

        if (!await storeAccess.CanAccessStoreAsync(dto.SourceStoreId, ct) ||
            !await storeAccess.CanAccessStoreAsync(dto.DestinationStoreId, ct))
        {
            return Result<Guid>.Failure("You are not allowed to create transfers for one or both selected stores.", "STORE_ACCESS_DENIED");
        }

        var cleanLines = dto.Lines
            .Where(l => l.ProductId != Guid.Empty && l.Quantity > 0)
            .GroupBy(l => new { l.ProductId, l.ProductVariantId })
            .Select(g => new CreateStockTransferLineDto
            {
                ProductId = g.Key.ProductId,
                ProductVariantId = g.Key.ProductVariantId,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        if (cleanLines.Count == 0)
        {
            return Result<Guid>.Failure("Add at least one product line.", "NO_LINES");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var sourceStore = await db.Stores.FirstOrDefaultAsync(s => s.Id == dto.SourceStoreId && s.IsActive, ct);
            var destinationStore = await db.Stores.FirstOrDefaultAsync(s => s.Id == dto.DestinationStoreId && s.IsActive, ct);

            if (sourceStore is null)
            {
                return Result<Guid>.Failure("Source store not found or inactive.", "SOURCE_NOT_FOUND");
            }

            if (destinationStore is null)
            {
                return Result<Guid>.Failure("Destination store not found or inactive.", "DEST_NOT_FOUND");
            }

            var now = clock.UtcNow;
            var transferNumber = await numberSequence.GetNextAsync(
                sourceStore.TenantId,
                sourceStore.Id,
                "TRANSFER",
                $"{sourceStore.Code}-TRF-{now:yyyyMMdd}-",
                now,
                4,
                ct);

            var transfer = new StockTransfer
            {
                StoreId = sourceStore.Id,
                DestinationStoreId = destinationStore.Id,
                TransferNumber = transferNumber,
                Status = StockTransferStatus.Requested,
                RequestedOn = now,
                RequestedByUserId = currentUser.UserId ?? string.Empty,
                Notes = dto.Notes?.Trim()
            };

            var lineNumber = 1;
            foreach (var dtoLine in cleanLines)
            {
                var product = await db.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == dtoLine.ProductId && p.IsActive, ct);

                if (product is null)
                {
                    return Result<Guid>.Failure("One of the selected products was not found or inactive.", "PRODUCT_NOT_FOUND");
                }

                ProductVariant? variant = null;
                if (dtoLine.ProductVariantId is { } variantId)
                {
                    variant = await db.ProductVariants.AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id && v.IsActive, ct);

                    if (variant is null)
                    {
                        return Result<Guid>.Failure($"Variant not found for {product.Name}.", "VARIANT_NOT_FOUND");
                    }
                }

                transfer.Lines.Add(new StockTransferLine
                {
                    LineNumber = lineNumber++,
                    ProductId = product.Id,
                    ProductVariantId = variant?.Id,
                    Sku = variant?.Sku ?? product.Sku,
                    ProductName = product.Name,
                    VariantName = variant?.Name,
                    QuantityRequested = dtoLine.Quantity,
                    QuantityDispatched = 0,
                    QuantityReceived = 0
                });
            }

            AddStatusHistory(transfer, null, StockTransferStatus.Requested, dto.Notes);
            db.StockTransfers.Add(transfer);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Guid>.Success(transfer.Id);
        });
    }

    private async Task<Result> ApproveCoreAsync(StockTransferActionDto dto, CancellationToken ct)
    {
        var transfer = await db.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == dto.TransferId, ct);

        if (transfer is null)
        {
            return Result.Failure("Transfer not found.", "NOT_FOUND");
        }

        if (!await CanAccessTransferAsync(transfer.StoreId, transfer.DestinationStoreId, ct))
        {
            return Result.Failure("You are not allowed to approve this transfer.", "STORE_ACCESS_DENIED");
        }

        if (transfer.Status != StockTransferStatus.Requested)
        {
            return Result.Failure("Only requested transfers can be approved.", "INVALID_STATUS");
        }

        var fromStatus = transfer.Status;
        transfer.Status = StockTransferStatus.Approved;
        transfer.ApprovedOn = clock.UtcNow;
        transfer.ApprovedByUserId = currentUser.UserId ?? string.Empty;
        AddStatusHistory(transfer, fromStatus, StockTransferStatus.Approved, dto.Notes);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> DispatchCoreAsync(StockTransferActionDto dto, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var transfer = await db.StockTransfers
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == dto.TransferId, ct);

            if (transfer is null)
            {
                return Result.Failure("Transfer not found.", "NOT_FOUND");
            }

            if (!await CanAccessTransferAsync(transfer.StoreId, transfer.DestinationStoreId, ct))
            {
                return Result.Failure("You are not allowed to dispatch this transfer.", "STORE_ACCESS_DENIED");
            }

            if (transfer.Status != StockTransferStatus.Approved)
            {
                return Result.Failure("Only approved transfers can be dispatched.", "INVALID_STATUS");
            }

            foreach (var line in transfer.Lines.OrderBy(l => l.LineNumber))
            {
                var remainingToDispatch = line.QuantityRequested - line.QuantityDispatched;
                if (remainingToDispatch <= 0)
                {
                    continue;
                }

                var result = await ApplyStockMovementAsync(
                    transfer.StoreId,
                    line.ProductId,
                    line.ProductVariantId,
                    -remainingToDispatch,
                    transfer.TransferNumber,
                    $"Transfer dispatch to destination store. {dto.Notes}".Trim(),
                    ct);

                if (!result.Succeeded)
                {
                    return result;
                }

                line.QuantityDispatched += remainingToDispatch;
            }

            var fromStatus = transfer.Status;
            transfer.Status = StockTransferStatus.Dispatched;
            transfer.DispatchedOn = clock.UtcNow;
            transfer.DispatchedByUserId = currentUser.UserId ?? string.Empty;
            AddStatusHistory(transfer, fromStatus, StockTransferStatus.Dispatched, dto.Notes);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> ReceiveCoreAsync(ReceiveStockTransferDto dto, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var transfer = await db.StockTransfers
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == dto.TransferId, ct);

            if (transfer is null)
            {
                return Result.Failure("Transfer not found.", "NOT_FOUND");
            }

            if (!await CanAccessTransferAsync(transfer.StoreId, transfer.DestinationStoreId, ct))
            {
                return Result.Failure("You are not allowed to receive this transfer.", "STORE_ACCESS_DENIED");
            }

            if (transfer.Status is not (StockTransferStatus.Dispatched or StockTransferStatus.PartiallyReceived))
            {
                return Result.Failure("Only dispatched transfers can be received.", "INVALID_STATUS");
            }

            var receiveMap = dto.Lines.ToDictionary(x => x.LineId, x => x.QuantityReceived);
            var anyReceived = false;

            foreach (var line in transfer.Lines.OrderBy(l => l.LineNumber))
            {
                var remaining = line.QuantityDispatched - line.QuantityReceived;
                if (remaining <= 0)
                {
                    continue;
                }

                var quantityToReceive = receiveMap.TryGetValue(line.Id, out var requestedQty)
                    ? requestedQty
                    : remaining;

                if (quantityToReceive < 0)
                {
                    return Result.Failure("Receive quantity cannot be negative.", "INVALID_QTY");
                }

                if (quantityToReceive > remaining)
                {
                    return Result.Failure($"Receive quantity for {line.ProductName} cannot exceed remaining quantity {remaining:0.###}.", "INVALID_QTY");
                }

                if (quantityToReceive == 0)
                {
                    continue;
                }

                var result = await ApplyStockMovementAsync(
                    transfer.DestinationStoreId,
                    line.ProductId,
                    line.ProductVariantId,
                    quantityToReceive,
                    transfer.TransferNumber,
                    $"Transfer receive from source store. {dto.Notes}".Trim(),
                    ct);

                if (!result.Succeeded)
                {
                    return result;
                }

                line.QuantityReceived += quantityToReceive;
                anyReceived = true;
            }

            if (!anyReceived)
            {
                return Result.Failure("Enter at least one quantity to receive.", "NO_QTY");
            }

            var fromStatus = transfer.Status;
            var fullyReceived = transfer.Lines.All(l => l.QuantityReceived >= l.QuantityDispatched);
            transfer.Status = fullyReceived ? StockTransferStatus.Received : StockTransferStatus.PartiallyReceived;
            transfer.ReceivedOn = fullyReceived ? clock.UtcNow : transfer.ReceivedOn;
            transfer.ReceivedByUserId = currentUser.UserId ?? string.Empty;
            AddStatusHistory(transfer, fromStatus, transfer.Status, dto.Notes);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> CancelCoreAsync(StockTransferActionDto dto, CancellationToken ct)
    {
        var transfer = await db.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == dto.TransferId, ct);

        if (transfer is null)
        {
            return Result.Failure("Transfer not found.", "NOT_FOUND");
        }

        if (!await CanAccessTransferAsync(transfer.StoreId, transfer.DestinationStoreId, ct))
        {
            return Result.Failure("You are not allowed to cancel this transfer.", "STORE_ACCESS_DENIED");
        }

        if (transfer.Status is StockTransferStatus.Dispatched or StockTransferStatus.PartiallyReceived or StockTransferStatus.Received)
        {
            return Result.Failure("Dispatched or received transfers cannot be cancelled. Create a reverse transfer if needed.", "INVALID_STATUS");
        }

        var fromStatus = transfer.Status;
        transfer.Status = StockTransferStatus.Cancelled;
        transfer.CancelledOn = clock.UtcNow;
        transfer.CancelledByUserId = currentUser.UserId ?? string.Empty;
        transfer.CancellationReason = dto.Notes?.Trim();
        AddStatusHistory(transfer, fromStatus, StockTransferStatus.Cancelled, dto.Notes);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ApplyStockMovementAsync(
        Guid storeId,
        Guid productId,
        Guid? variantId,
        decimal quantityChange,
        string reference,
        string? notes,
        CancellationToken ct)
    {
        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == productId && s.ProductVariantId == variantId, ct);

        if (stock is null)
        {
            stock = new Stock
            {
                StoreId = storeId,
                ProductId = productId,
                ProductVariantId = variantId,
                QuantityOnHand = 0
            };
            db.Stocks.Add(stock);
        }

        var newQty = stock.QuantityOnHand + quantityChange;
        if (newQty < 0)
        {
            var productName = await db.Products.AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct) ?? "selected product";

            return Result.Failure($"Insufficient source stock for {productName}. Resulting quantity would be {newQty:0.###}.", "INSUFFICIENT_STOCK");
        }

        stock.QuantityOnHand = newQty;
        db.StockLedgers.Add(new StockLedger
        {
            StoreId = storeId,
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = StockMovementType.Transfer,
            QuantityChange = quantityChange,
            QuantityAfter = newQty,
            Reference = reference,
            Notes = notes
        });

        return Result.Success();
    }

    private void AddStatusHistory(
        StockTransfer transfer,
        StockTransferStatus? fromStatus,
        StockTransferStatus toStatus,
        string? notes)
    {
        transfer.StatusHistory.Add(new StockTransferStatusHistory
        {
            StockTransferId = transfer.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedOn = clock.UtcNow,
            ChangedByUserId = currentUser.UserId ?? string.Empty,
            Notes = notes?.Trim()
        });
    }

    private async Task<bool> CanAccessTransferAsync(Guid sourceStoreId, Guid destinationStoreId, CancellationToken ct)
    {
        var allowed = await storeAccess.GetAllowedStoreIdsAsync(ct);
        return allowed.Contains(sourceStoreId) || allowed.Contains(destinationStoreId);
    }

    private static StockTransferDetailDto MapDetail(StockTransfer transfer) => new()
    {
        Id = transfer.Id,
        TransferNumber = transfer.TransferNumber,
        SourceStoreId = transfer.StoreId,
        SourceStoreName = transfer.SourceStore == null ? string.Empty : transfer.SourceStore.Code + " · " + transfer.SourceStore.Name,
        DestinationStoreId = transfer.DestinationStoreId,
        DestinationStoreName = transfer.DestinationStore == null ? string.Empty : transfer.DestinationStore.Code + " · " + transfer.DestinationStore.Name,
        Status = transfer.Status.ToString(),
        RequestedOn = transfer.RequestedOn,
        ApprovedOn = transfer.ApprovedOn,
        DispatchedOn = transfer.DispatchedOn,
        ReceivedOn = transfer.ReceivedOn,
        Notes = transfer.Notes,
        Lines = transfer.Lines.OrderBy(l => l.LineNumber).Select(l => new StockTransferLineDto
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            ProductId = l.ProductId,
            ProductVariantId = l.ProductVariantId,
            Sku = l.Sku,
            ProductName = l.ProductName,
            VariantName = l.VariantName,
            QuantityRequested = l.QuantityRequested,
            QuantityDispatched = l.QuantityDispatched,
            QuantityReceived = l.QuantityReceived
        }).ToList()
    };
}
