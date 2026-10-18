using System.ComponentModel.DataAnnotations;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Models.Business;

public class BusinessEquipmentIndexViewModel
{
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public BusinessStatus BusinessStatus { get; set; }
    public bool CanPublishListings => BusinessStatus == BusinessStatus.Approved;
    public List<EquipmentManagementSummaryDto> Equipments { get; set; } = new();

    public int TotalListingsCount => Equipments.Count;
    public int ActiveListingsCount => Equipments.Count(e => e.IsActive);
    public int TotalInventoryUnits => Equipments.Sum(e => e.TotalQuantity);
    public int UnitsUnderMaintenance => Equipments.Sum(e => e.ActiveMaintenanceBlockedQuantity);
}

public class CreateEquipmentViewModel
{
    [Required(ErrorMessage = "Equipment title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    [Display(Name = "Equipment Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Equipment description and technical specifications are required.")]
    [Display(Name = "Description & Specifications")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Daily rental rate is required.")]
    [Range(0.01, 100000.00, ErrorMessage = "Daily rental rate must be greater than $0.00.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Daily Rental Rate ($)")]
    public decimal DailyRentalRate { get; set; }

    [Required(ErrorMessage = "Security deposit amount is required.")]
    [Range(0.00, 100000.00, ErrorMessage = "Security deposit cannot be negative.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Refundable Security Deposit ($)")]
    public decimal SecurityDeposit { get; set; }

    [Required(ErrorMessage = "Total rentable quantity is required.")]
    [Range(1, 10000, ErrorMessage = "Total rentable inventory must be at least 1 unit.")]
    [Display(Name = "Total Rentable Inventory Units")]
    public int TotalQuantity { get; set; } = 1;

    [Required(ErrorMessage = "Condition is required.")]
    [Display(Name = "Equipment Condition")]
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

    [Display(Name = "Publish Immediately (Active)")]
    public bool IsActive { get; set; } = true;

    [Url(ErrorMessage = "Please enter a valid absolute image URL (http or https).")]
    [Display(Name = "Primary Photo URL")]
    public string? PrimaryImageUrl { get; set; }

    [Display(Name = "Additional Photo URLs (one per line)")]
    public string? AdditionalImageUrls { get; set; }

    public List<CategorySelectOption> AvailableCategories { get; set; } = new();
}

public class EditEquipmentViewModel
{
    public int EquipmentId { get; set; }
    public int BusinessId { get; set; }

    [Required(ErrorMessage = "Equipment title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    [Display(Name = "Equipment Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Equipment description and specifications are required.")]
    [Display(Name = "Description & Specifications")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Daily rental rate is required.")]
    [Range(0.01, 100000.00, ErrorMessage = "Daily rental rate must be greater than $0.00.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Daily Rental Rate ($)")]
    public decimal DailyRentalRate { get; set; }

    [Required(ErrorMessage = "Security deposit amount is required.")]
    [Range(0.00, 100000.00, ErrorMessage = "Security deposit cannot be negative.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Refundable Security Deposit ($)")]
    public decimal SecurityDeposit { get; set; }

    [Required(ErrorMessage = "Total rentable quantity is required.")]
    [Range(1, 10000, ErrorMessage = "Total rentable inventory must be at least 1 unit.")]
    [Display(Name = "Total Rentable Inventory Units")]
    public int TotalQuantity { get; set; }

    [Required(ErrorMessage = "Condition is required.")]
    [Display(Name = "Equipment Condition")]
    public EquipmentCondition Condition { get; set; }

    [Display(Name = "Listing Status (Active)")]
    public bool IsActive { get; set; }

    public byte[]? RowVersion { get; set; }

    public int ActiveReservationsCount { get; set; }
    public int ActiveMaintenanceBlockedCount { get; set; }

    public List<CategorySelectOption> AvailableCategories { get; set; } = new();
    public List<EquipmentImageViewModel> ExistingImages { get; set; } = new();
}

public class CategorySelectOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EquipmentImageViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class EquipmentMaintenanceViewModel
{
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int ActiveReservedCount { get; set; }

    public List<MaintenanceRecordViewModel> Records { get; set; } = new();

    public AddMaintenanceRecordInput NewRecord { get; set; } = new();
}

public class AddMaintenanceRecordInput
{
    [Required(ErrorMessage = "Blocked quantity is required.")]
    [Range(1, 10000, ErrorMessage = "Quantity must be at least 1.")]
    [Display(Name = "Quantity Under Maintenance")]
    public int BlockedQuantity { get; set; } = 1;

    [Required(ErrorMessage = "Start date is required.")]
    [Display(Name = "Maintenance Start Date")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required(ErrorMessage = "End date is required.")]
    [Display(Name = "Expected Completion Date")]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

    [Required(ErrorMessage = "Maintenance reason or inspection notes required.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    [Display(Name = "Reason / Maintenance Notes")]
    public string Reason { get; set; } = string.Empty;

    [Range(0.00, 100000.00, ErrorMessage = "Estimated cost cannot be negative.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Estimated Repair / Service Cost ($)")]
    public decimal? EstimatedCost { get; set; }
}

public class MaintenanceRecordViewModel
{
    public int Id { get; set; }
    public int BlockedQuantity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public bool IsActiveNow => !IsResolved && StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && EndDate >= DateOnly.FromDateTime(DateTime.UtcNow);
}
