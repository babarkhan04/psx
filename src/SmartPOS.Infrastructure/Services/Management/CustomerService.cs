using System.Data;
using SmartPOS.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class CustomerService(AppDbContext db, IDateTimeProvider clock, DbContextGate gate) : ICustomerService
{
    public Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(string? search = null, bool includeInactive = true, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var query = db.Customers.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var like = $"%{search.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.Name, like) ||
                    (c.Phone != null && EF.Functions.Like(c.Phone, like)) ||
                    (c.City != null && EF.Functions.Like(c.City, like)));
            }

            return (IReadOnlyList<CustomerListItemDto>)await query
                .OrderBy(c => c.Name)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    City = c.City,
                    CreditLimit = c.CreditLimit,
                    Balance = c.Balance,
                    IsActive = c.IsActive
                })
                .ToListAsync(ct);
        }, ct);

    public Task<CustomerDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? null : new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Email = c.Email,
                AddressLine = c.AddressLine,
                City = c.City,
                CreditLimit = c.CreditLimit,
                Balance = c.Balance,
                Notes = c.Notes,
                IsActive = c.IsActive
            };
        }, ct);

    public Task<IReadOnlyList<CustomerLookupDto>> SearchLookupAsync(string? term, int take = 10, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var query = db.Customers.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var like = $"%{term.Trim()}%";
                var normalized = CustomerContactRules.NormalizePhone(term);
                query = query.Where(c =>
                    EF.Functions.Like(c.Name, like) ||
                    (c.Phone != null && EF.Functions.Like(c.Phone, like)) ||
                    (normalized != null && c.NormalizedPhone == normalized));
            }

            return (IReadOnlyList<CustomerLookupDto>)await query
                .OrderBy(c => c.Name)
                .Take(take <= 0 ? 10 : take)
                .Select(c => new CustomerLookupDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    CreditLimit = c.CreditLimit,
                    Balance = c.Balance
                })
                .ToListAsync(ct);
        }, ct);

    public Task<Result<Guid>> CreateAsync(CustomerDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<Guid>.Failure("Customer name is required.", "VALIDATION");
            }

            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                var customer = new Customer
                {
                    Name = name,
                    Phone = dto.Phone?.Trim(),
                    NormalizedPhone = CustomerContactRules.NormalizePhone(dto.Phone),
                    Email = dto.Email?.Trim(),
                    AddressLine = dto.AddressLine?.Trim(),
                    City = dto.City?.Trim(),
                    CreditLimit = dto.CreditLimit,
                    Balance = 0,
                    Notes = dto.Notes?.Trim(),
                    IsActive = dto.IsActive
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(ct);

                if (dto.OpeningBalance > 0)
                {
                    customer.Balance = dto.OpeningBalance;
                    db.CustomerLedgers.Add(new CustomerLedger
                    {
                        CustomerId = customer.Id,
                        EntryType = CustomerLedgerEntryType.Opening,
                        Amount = dto.OpeningBalance,
                        BalanceAfter = dto.OpeningBalance,
                        Reference = "OPENING",
                        EntryDate = clock.UtcNow
                    });
                    await db.SaveChangesAsync(ct);
                }

                return Result<Guid>.Success(customer.Id);
            });
        }, ct);

    public Task<Result> UpdateAsync(CustomerDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (customer is null)
            {
                return Result.Failure("Customer not found.", "NOT_FOUND");
            }

            customer.Name = dto.Name.Trim();
            customer.Phone = dto.Phone?.Trim();
            customer.NormalizedPhone = CustomerContactRules.NormalizePhone(dto.Phone);
            customer.Email = dto.Email?.Trim();
            customer.AddressLine = dto.AddressLine?.Trim();
            customer.City = dto.City?.Trim();
            customer.CreditLimit = dto.CreditLimit;
            customer.Notes = dto.Notes?.Trim();
            customer.IsActive = dto.IsActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (customer is null)
            {
                return Result.Failure("Customer not found.", "NOT_FOUND");
            }

            customer.IsActive = isActive;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (customer is null)
            {
                return Result.Failure("Customer not found.", "NOT_FOUND");
            }

            if (customer.Balance != 0)
            {
                return Result.Failure("Cannot delete a customer with an outstanding balance.", "HAS_BALANCE");
            }

            if (await db.Sales.AnyAsync(s => s.CustomerId == id, ct))
            {
                return Result.Failure("Cannot delete a customer who has sales history.", "HAS_SALES");
            }

            db.Customers.Remove(customer);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    public Task<Result> ReceivePaymentAsync(ReceivePaymentDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            if (dto.Amount <= 0)
            {
                return Result.Failure("Enter an amount greater than zero.", "VALIDATION");
            }

            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == dto.CustomerId, ct);
                if (customer is null)
                {
                    return Result.Failure("Customer not found.", "NOT_FOUND");
                }

                if (dto.Amount > customer.Balance)
                {
                    return Result.Failure(
                        $"Payment exceeds the outstanding balance of {customer.Balance:0.00}.", "OVERPAY");
                }

                customer.Balance -= dto.Amount;
                db.CustomerLedgers.Add(new CustomerLedger
                {
                    CustomerId = customer.Id,
                    EntryType = CustomerLedgerEntryType.Payment,
                    Amount = -dto.Amount,
                    BalanceAfter = customer.Balance,
                    Reference = dto.Reference?.Trim(),
                    Notes = dto.Notes?.Trim(),
                    EntryDate = clock.UtcNow
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Result.Success();
            });
        }, ct);

    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(Guid customerId, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
            (IReadOnlyList<CustomerLedgerEntryDto>)await db.CustomerLedgers.AsNoTracking()
                .Where(l => l.CustomerId == customerId)
                .OrderByDescending(l => l.EntryDate)
                .Select(l => new CustomerLedgerEntryDto
                {
                    EntryDate = l.EntryDate,
                    EntryType = l.EntryType.ToString(),
                    Amount = l.Amount,
                    BalanceAfter = l.BalanceAfter,
                    Reference = l.Reference,
                    Notes = l.Notes
                })
                .ToListAsync(ct),
        ct);
}
