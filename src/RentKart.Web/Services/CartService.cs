using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Web.Services;

public class CartSessionItem
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; } = 1;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class CartService : ICartService
{
    private const string CartSessionKey = "_RentKart_Cart";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _dbContext;
    private readonly IBookingService _bookingService;

    public CartService(
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext dbContext,
        IBookingService bookingService)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _bookingService = bookingService;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public async Task<CartSummaryDto> GetCartAsync()
    {
        var sessionItems = GetSessionItems();
        var summary = new CartSummaryDto();

        if (sessionItems.Count == 0)
        {
            return summary;
        }

        var equipmentIds = sessionItems.Select(i => i.EquipmentId).Distinct().ToList();
        var equipments = await _dbContext.Equipments
            .Include(e => e.Business)
            .Include(e => e.Images)
            .Include(e => e.Category)
            .Where(e => equipmentIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mappedItems = new List<CartItemDto>();

        foreach (var sItem in sessionItems)
        {
            if (!equipments.TryGetValue(sItem.EquipmentId, out var equipment))
            {
                continue; // Stale item, skip
            }

            if (!DateOnly.TryParse(sItem.StartDate, out var startDate))
            {
                startDate = today.AddDays(1);
            }
            if (!DateOnly.TryParse(sItem.EndDate, out var endDate) || endDate < startDate)
            {
                endDate = startDate.AddDays(1);
            }

            int daysCount = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);
            decimal rentalSubtotal = equipment.DailyRentalRate * sItem.Quantity * daysCount;
            decimal depositSubtotal = equipment.SecurityDeposit * sItem.Quantity;

            // Real-time authoritative availability calculation
            int availableQty = await _bookingService.GetAuthoritativeAvailableQuantityAsync(equipment.Id, startDate, endDate);
            bool isAvailable = availableQty >= sItem.Quantity && equipment.IsActive && equipment.Status == EquipmentStatus.Active && equipment.Business.Status == BusinessStatus.Approved;

            if (!isAvailable)
            {
                summary.HasUnavailableItems = true;
                if (!equipment.IsActive || equipment.Status != EquipmentStatus.Active)
                {
                    summary.AvailabilityWarnings.Add($"'{equipment.Title}' is no longer active for rent.");
                }
                else if (equipment.Business.Status != BusinessStatus.Approved)
                {
                    summary.AvailabilityWarnings.Add($"The store for '{equipment.Title}' is currently suspended or inactive.");
                }
                else
                {
                    summary.AvailabilityWarnings.Add($"'{equipment.Title}' only has {availableQty} unit(s) available for the selected dates (requested: {sItem.Quantity}).");
                }
            }

            var itemDto = new CartItemDto
            {
                EquipmentId = equipment.Id,
                Title = equipment.Title,
                PrimaryImageUrl = equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                    ?? equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl,
                BusinessId = equipment.BusinessId,
                BusinessName = equipment.Business.ShopName,
                BusinessCity = equipment.Business.City,
                BusinessAddressLine = equipment.Business.AddressLine,
                BusinessPhone = equipment.Business.ContactPhone,
                OperatingHours = equipment.Business.OperatingHours,
                Quantity = sItem.Quantity,
                DailyRentalRate = equipment.DailyRentalRate,
                SecurityDepositPerUnit = equipment.SecurityDeposit,
                StartDate = startDate,
                EndDate = endDate,
                DaysCount = daysCount,
                ItemRentalSubtotal = rentalSubtotal,
                ItemDepositSubtotal = depositSubtotal,
                ItemTotal = rentalSubtotal + depositSubtotal,
                AvailableQuantity = availableQty,
                IsAvailable = isAvailable
            };

            mappedItems.Add(itemDto);
        }

        summary.Items = mappedItems;
        summary.TotalItemsCount = mappedItems.Sum(i => i.Quantity);
        summary.TotalRentalFee = mappedItems.Sum(i => i.ItemRentalSubtotal);
        summary.TotalSecurityDeposit = mappedItems.Sum(i => i.ItemDepositSubtotal);
        summary.GrandTotal = summary.TotalRentalFee + summary.TotalSecurityDeposit;

        // Group by Business for multi-store checkout breakdown
        summary.BusinessGroups = mappedItems
            .GroupBy(i => i.BusinessId)
            .Select(g =>
            {
                var first = g.First();
                return new CartBusinessGroupDto
                {
                    BusinessId = first.BusinessId,
                    BusinessName = first.BusinessName,
                    BusinessCity = first.BusinessCity,
                    BusinessAddressLine = first.BusinessAddressLine,
                    BusinessPhone = first.BusinessPhone,
                    OperatingHours = first.OperatingHours,
                    Items = g.ToList(),
                    GroupRentalSubtotal = g.Sum(i => i.ItemRentalSubtotal),
                    GroupDepositSubtotal = g.Sum(i => i.ItemDepositSubtotal),
                    GroupTotal = g.Sum(i => i.ItemTotal)
                };
            })
            .ToList();

        summary.DistinctStoresCount = summary.BusinessGroups.Count;

        return summary;
    }

    public async Task<CartOperationResult> AddToCartAsync(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate)
    {
        if (quantity <= 0)
        {
            return CartOperationResult.Fail("Quantity must be at least 1.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (startDate < today)
        {
            return CartOperationResult.Fail("Rental start date cannot be in the past.");
        }

        if (endDate < startDate)
        {
            return CartOperationResult.Fail("Rental end date cannot be earlier than start date.");
        }

        var equipment = await _dbContext.Equipments
            .Include(e => e.Business)
            .FirstOrDefaultAsync(e => e.Id == equipmentId);

        if (equipment == null || !equipment.IsActive || equipment.Status != EquipmentStatus.Active)
        {
            return CartOperationResult.Fail("The selected equipment is not available for rent.");
        }

        if (equipment.Business.Status != BusinessStatus.Approved)
        {
            return CartOperationResult.Fail("The store providing this equipment is not approved to accept rentals.");
        }

        // Check availability
        int available = await _bookingService.GetAuthoritativeAvailableQuantityAsync(equipmentId, startDate, endDate);
        if (available < quantity)
        {
            return CartOperationResult.Fail($"Only {available} unit(s) available for the selected dates.");
        }

        var items = GetSessionItems();
        var existing = items.FirstOrDefault(i => i.EquipmentId == equipmentId);
        if (existing != null)
        {
            int newQuantity = existing.Quantity + quantity;
            if (newQuantity > available)
            {
                return CartOperationResult.Fail($"Cannot add {quantity} more. Maximum available for these dates is {available} (already in cart: {existing.Quantity}).");
            }

            existing.Quantity = newQuantity;
            existing.StartDate = startDate.ToString("yyyy-MM-dd");
            existing.EndDate = endDate.ToString("yyyy-MM-dd");
        }
        else
        {
            items.Add(new CartSessionItem
            {
                EquipmentId = equipmentId,
                Quantity = quantity,
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd")
            });
        }

        SaveSessionItems(items);
        return CartOperationResult.Ok("Item added to cart.", items.Sum(i => i.Quantity));
    }

    public async Task<CartOperationResult> UpdateCartItemAsync(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate)
    {
        if (quantity <= 0)
        {
            return await RemoveFromCartAsync(equipmentId);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (startDate < today)
        {
            return CartOperationResult.Fail("Rental start date cannot be in the past.");
        }

        if (endDate < startDate)
        {
            return CartOperationResult.Fail("Rental end date cannot be earlier than start date.");
        }

        var items = GetSessionItems();
        var existing = items.FirstOrDefault(i => i.EquipmentId == equipmentId);
        if (existing == null)
        {
            return CartOperationResult.Fail("Item not found in cart.");
        }

        int available = await _bookingService.GetAuthoritativeAvailableQuantityAsync(equipmentId, startDate, endDate);
        if (available < quantity)
        {
            return CartOperationResult.Fail($"Only {available} unit(s) available for the selected dates.");
        }

        existing.Quantity = quantity;
        existing.StartDate = startDate.ToString("yyyy-MM-dd");
        existing.EndDate = endDate.ToString("yyyy-MM-dd");

        SaveSessionItems(items);
        return CartOperationResult.Ok("Cart updated.", items.Sum(i => i.Quantity));
    }

    public Task<CartOperationResult> RemoveFromCartAsync(int equipmentId)
    {
        var items = GetSessionItems();
        items.RemoveAll(i => i.EquipmentId == equipmentId);
        SaveSessionItems(items);
        return Task.FromResult(CartOperationResult.Ok("Item removed from cart.", items.Sum(i => i.Quantity)));
    }

    public Task ClearCartAsync()
    {
        Session?.Remove(CartSessionKey);
        return Task.CompletedTask;
    }

    public Task<int> GetCartItemCountAsync()
    {
        var items = GetSessionItems();
        return Task.FromResult(items.Sum(i => i.Quantity));
    }

    private List<CartSessionItem> GetSessionItems()
    {
        var session = Session;
        if (session == null) return new List<CartSessionItem>();

        var json = session.GetString(CartSessionKey);
        if (string.IsNullOrWhiteSpace(json)) return new List<CartSessionItem>();

        try
        {
            return JsonSerializer.Deserialize<List<CartSessionItem>>(json) ?? new List<CartSessionItem>();
        }
        catch
        {
            return new List<CartSessionItem>();
        }
    }

    private void SaveSessionItems(List<CartSessionItem> items)
    {
        var session = Session;
        if (session == null) return;

        var json = JsonSerializer.Serialize(items);
        session.SetString(CartSessionKey, json);
    }
}
