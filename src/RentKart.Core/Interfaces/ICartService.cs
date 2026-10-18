namespace RentKart.Core.Interfaces;

public class CartItemDto
{
    public int EquipmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessCity { get; set; } = string.Empty;
    public string BusinessAddressLine { get; set; } = string.Empty;
    public string BusinessPhone { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDepositPerUnit { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysCount { get; set; }
    public decimal ItemRentalSubtotal { get; set; }
    public decimal ItemDepositSubtotal { get; set; }
    public decimal ItemTotal { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class CartBusinessGroupDto
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessCity { get; set; } = string.Empty;
    public string BusinessAddressLine { get; set; } = string.Empty;
    public string BusinessPhone { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new();
    public decimal GroupRentalSubtotal { get; set; }
    public decimal GroupDepositSubtotal { get; set; }
    public decimal GroupTotal { get; set; }
}

public class CartSummaryDto
{
    public List<CartItemDto> Items { get; set; } = new();
    public List<CartBusinessGroupDto> BusinessGroups { get; set; } = new();
    public int TotalItemsCount { get; set; }
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public int DistinctStoresCount { get; set; }
    public bool HasUnavailableItems { get; set; }
    public List<string> AvailabilityWarnings { get; set; } = new();
}

public class CartOperationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int CurrentCartCount { get; set; }

    public static CartOperationResult Ok(string message, int count) =>
        new() { Success = true, Message = message, CurrentCartCount = count };

    public static CartOperationResult Fail(string error) =>
        new() { Success = false, Message = error };
}

public interface ICartService
{
    Task<CartSummaryDto> GetCartAsync();
    Task<CartOperationResult> AddToCartAsync(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate);
    Task<CartOperationResult> UpdateCartItemAsync(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate);
    Task<CartOperationResult> RemoveFromCartAsync(int equipmentId);
    Task ClearCartAsync();
    Task<int> GetCartItemCountAsync();
}
