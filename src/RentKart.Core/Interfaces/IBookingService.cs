using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class CreateBookingItemRequestDto
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class CreateBookingRequestDto
{
    public List<CreateBookingItemRequestDto> Items { get; set; } = new();
    public string? CustomerNotes { get; set; }
}

public class BookingOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? BookingId { get; set; }
    public string? BookingNumber { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public static BookingOperationResult Ok(int bookingId, string bookingNumber) =>
        new() { Success = true, BookingId = bookingId, BookingNumber = bookingNumber };

    public static BookingOperationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error, ValidationErrors = new() { error } };

    public static BookingOperationResult Fail(List<string> errors) =>
        new() { Success = false, ErrorMessage = string.Join("; ", errors), ValidationErrors = errors };
}

public class BookingGroupOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public BookingGroupStatus? NewStatus { get; set; }

    public static BookingGroupOperationResult Ok(BookingGroupStatus newStatus) =>
        new() { Success = true, NewStatus = newStatus };

    public static BookingGroupOperationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

public class CustomerBookingSummaryDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public BookingStatus Status { get; set; }
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public int TotalGroupsCount { get; set; }
    public int TotalItemsCount { get; set; }
    public List<CustomerBookingGroupSummaryDto> Groups { get; set; } = new();
}

public class CustomerBookingGroupSummaryDto
{
    public int Id { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessCity { get; set; } = string.Empty;
    public string PickupVerificationCode { get; set; } = string.Empty;
    public BookingGroupStatus Status { get; set; }
    public decimal SubtotalRentalFee { get; set; }
    public decimal SubtotalSecurityDeposit { get; set; }
    public int ItemsCount { get; set; }
    public DateOnly EarliestStartDate { get; set; }
    public DateOnly LatestEndDate { get; set; }
}

public class CustomerBookingDetailsDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public BookingStatus Status { get; set; }
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public List<BookingGroupDetailDto> Groups { get; set; } = new();
}

public class BookingGroupDetailDto
{
    public int Id { get; set; }
    public int ParentBookingId { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddressLine { get; set; } = string.Empty;
    public string BusinessCity { get; set; } = string.Empty;
    public string BusinessPostalCode { get; set; } = string.Empty;
    public string BusinessPhone { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public string PickupVerificationCode { get; set; } = string.Empty;
    public BookingGroupStatus Status { get; set; }
    public decimal SubtotalRentalFee { get; set; }
    public decimal SubtotalSecurityDeposit { get; set; }
    public string? HandoverNotes { get; set; }
    public DateTime? HandedOverAtUtc { get; set; }
    public string? ReturnInspectionNotes { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public List<BookingItemDetailDto> Items { get; set; } = new();
}

public class BookingItemDetailDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysCount { get; set; }
    public decimal DailyRentalRate { get; set; }
    public decimal ItemSecurityDeposit { get; set; }
    public decimal TotalItemPrice { get; set; }
}

public class BusinessBookingGroupListItemDto
{
    public int Id { get; set; }
    public int ParentBookingId { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public BookingGroupStatus Status { get; set; }
    public decimal SubtotalRentalFee { get; set; }
    public decimal SubtotalSecurityDeposit { get; set; }
    public int ItemsCount { get; set; }
    public DateOnly EarliestStartDate { get; set; }
    public DateOnly LatestEndDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public interface IBookingService
{
    /// <summary>
    /// Concurrency-safe creation of a multi-item, multi-business booking inside a serializable/UPDLOCK database transaction.
    /// Recalculates real-time availability over each item's requested dates, snapshots rates and deposits,
    /// creates business-specific booking groups, and aggregates parent totals.
    /// </summary>
    Task<BookingOperationResult> CreateBookingAsync(string customerUserId, CreateBookingRequestDto request);

    /// <summary>
    /// Gets paginated customer bookings for the authenticated customer.
    /// </summary>
    Task<(List<CustomerBookingSummaryDto> Items, int TotalCount)> GetCustomerBookingsAsync(string customerUserId, int page = 1, int pageSize = 10);

    /// <summary>
    /// Gets full booking details for a customer with strict ownership validation.
    /// </summary>
    Task<CustomerBookingDetailsDto?> GetCustomerBookingDetailsAsync(int bookingId, string customerUserId);

    /// <summary>
    /// Customer cancellation of a specific booking group before handover. Releases reserved inventory.
    /// </summary>
    Task<BookingGroupOperationResult> CancelBookingGroupByCustomerAsync(int bookingGroupId, string customerUserId, string? reason);

    /// <summary>
    /// Gets paginated booking groups for an approved business owner.
    /// </summary>
    Task<(List<BusinessBookingGroupListItemDto> Items, int TotalCount)> GetBusinessBookingGroupsAsync(int businessId, BookingGroupStatus? statusFilter = null, int page = 1, int pageSize = 15);

    /// <summary>
    /// Gets detailed booking group info for a business owner with strict ownership verification.
    /// </summary>
    Task<BookingGroupDetailDto?> GetBusinessBookingGroupDetailsAsync(int bookingGroupId, int businessId);

    /// <summary>
    /// Business accepts a booking group (transitions from PendingApproval -> Confirmed).
    /// </summary>
    Task<BookingGroupOperationResult> AcceptBookingGroupAsync(int bookingGroupId, int businessId);

    /// <summary>
    /// Business rejects a booking group (transitions from PendingApproval -> Rejected). Releases inventory.
    /// </summary>
    Task<BookingGroupOperationResult> RejectBookingGroupAsync(int bookingGroupId, int businessId, string? rejectionReason);

    /// <summary>
    /// Business records handover of equipment to the customer using verification code (Confirmed -> HandedOver).
    /// </summary>
    Task<BookingGroupOperationResult> HandoverBookingGroupAsync(int bookingGroupId, int businessId, string verificationCode, string? handoverNotes);

    /// <summary>
    /// Business records detailed handover with per-item quantity and condition checklist.
    /// </summary>
    Task<BookingGroupOperationResult> HandoverBookingGroupDetailedAsync(int bookingGroupId, int businessId, HandoverBookingGroupRequestDto request);

    /// <summary>
    /// Business records return and inspection of equipment (HandedOver -> Completed). Releases inventory.
    /// </summary>
    Task<BookingGroupOperationResult> CompleteReturnBookingGroupAsync(int bookingGroupId, int businessId, string? inspectionNotes);

    /// <summary>
    /// Business records detailed return inspection with damage evaluation, deposit assessment, and automatic maintenance blocking.
    /// </summary>
    Task<ReturnInspectionResultDto> CompleteReturnWithInspectionAsync(int bookingGroupId, int businessId, CompleteReturnBookingGroupRequestDto request);

    /// <summary>
    /// Calculates authoritative date-range availability for an equipment item considering all active reservations and maintenance downtime.
    /// </summary>
    Task<int> GetAuthoritativeAvailableQuantityAsync(int equipmentId, DateOnly startDate, DateOnly endDate);
}

public class HandoverItemCheckDto
{
    public int BookingItemId { get; set; }
    public int QuantityHandedOver { get; set; }
    public string? ConditionNotes { get; set; }
    public bool AccessoriesConfirmed { get; set; } = true;
}

public class HandoverBookingGroupRequestDto
{
    public string VerificationCode { get; set; } = string.Empty;
    public string? GeneralHandoverNotes { get; set; }
    public List<HandoverItemCheckDto> Items { get; set; } = new();
}

public class ReturnItemInspectionDto
{
    public int BookingItemId { get; set; }
    public int ReturnedQuantity { get; set; }
    public int MissingQuantity { get; set; }
    public string Condition { get; set; } = "Good";
    public bool HasDamage { get; set; }
    public string? DamageDescription { get; set; }
    public decimal? EstimatedRepairCost { get; set; }
    public string? PhotoEvidenceUrl { get; set; }
    public bool CreateMaintenanceBlock { get; set; } = true;
    public int? EstimatedMaintenanceDays { get; set; } = 7;
}

public class CompleteReturnBookingGroupRequestDto
{
    public string? GeneralInspectionNotes { get; set; }
    public decimal? AssessedDamageFee { get; set; }
    public List<ReturnItemInspectionDto> Items { get; set; } = new();
}

public class ReturnInspectionResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public BookingGroupStatus NewStatus { get; set; }
    public decimal TotalDepositHeld { get; set; }
    public decimal TotalDamageFeeAssessed { get; set; }
    public decimal DepositRefundAmount { get; set; }
    public decimal ForfeitedDepositAmount { get; set; }
    public int MaintenanceRecordsCreatedCount { get; set; }
    public List<int> CreatedMaintenanceRecordIds { get; set; } = new();
    public List<string> InspectionSummaryLines { get; set; } = new();

    public static ReturnInspectionResultDto Ok(
        BookingGroupStatus status,
        decimal depositHeld,
        decimal damageFee,
        decimal refundAmount,
        decimal forfeitedAmount,
        List<int> maintenanceIds,
        List<string> summaries) =>
        new()
        {
            Success = true,
            NewStatus = status,
            TotalDepositHeld = depositHeld,
            TotalDamageFeeAssessed = damageFee,
            DepositRefundAmount = refundAmount,
            ForfeitedDepositAmount = forfeitedAmount,
            MaintenanceRecordsCreatedCount = maintenanceIds.Count,
            CreatedMaintenanceRecordIds = maintenanceIds,
            InspectionSummaryLines = summaries
        };

    public static ReturnInspectionResultDto Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
