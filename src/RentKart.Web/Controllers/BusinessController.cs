using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Business;

namespace RentKart.Web.Controllers;

[Authorize(Roles = $"{IdentityDataSeeder.RoleBusiness},{IdentityDataSeeder.RoleAdmin}")]
public class BusinessController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBusinessManagementService _businessService;
    private readonly IBusinessEquipmentService _equipmentService;
    private readonly IEquipmentCatalogService _catalogService;
    private readonly IBookingService _bookingService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BusinessController> _logger;

    public BusinessController(
        UserManager<ApplicationUser> userManager,
        IBusinessManagementService businessService,
        IBusinessEquipmentService equipmentService,
        IEquipmentCatalogService catalogService,
        IBookingService bookingService,
        ApplicationDbContext context,
        ILogger<BusinessController> logger)
    {
        _userManager = userManager;
        _businessService = businessService;
        _equipmentService = equipmentService;
        _catalogService = catalogService;
        _bookingService = bookingService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            TempData["ErrorMessage"] = "No registered business profile was found for your account.";
            return RedirectToAction("RegisterBusiness", "Account");
        }

        var equipmentList = await _equipmentService.GetBusinessEquipmentAsync(business.Id);

        var model = new BusinessDashboardViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            OwnerName = user.FullName,
            Email = user.Email ?? string.Empty,
            ContactPhone = business.ContactPhone,
            Address = $"{business.AddressLine}, {business.City}, {business.State} {business.PostalCode}",
            OperatingHours = business.OperatingHours,
            Status = business.Status,
            CreatedAtUtc = business.CreatedAtUtc,
            ApprovedAtUtc = business.ApprovedAtUtc,
            SuspendedAtUtc = business.SuspendedAtUtc
        };

        ViewBag.TotalListings = equipmentList.Count;
        ViewBag.ActiveListings = equipmentList.Count(e => e.IsActive);
        ViewBag.TotalUnits = equipmentList.Sum(e => e.TotalQuantity);
        ViewBag.UnderMaintenance = equipmentList.Sum(e => e.ActiveMaintenanceBlockedQuantity);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var model = new BusinessProfileViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            RegistrationNumber = business.RegistrationNumber,
            ContactPhone = business.ContactPhone,
            AddressLine = business.AddressLine,
            City = business.City,
            State = business.State,
            PostalCode = business.PostalCode,
            OperatingHours = business.OperatingHours,
            PickupInstructions = business.PickupInstructions,
            Status = business.Status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(BusinessProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // Server-side ownership security check
        if (business.UserId != user.Id && !User.IsInRole(IdentityDataSeeder.RoleAdmin))
        {
            _logger.LogWarning("Unauthorized attempt to update business profile {BusinessId} by user {UserId}.", model.BusinessId, user.Id);
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.Status = business.Status;
            return View(model);
        }

        // Update permitted business profile fields (business owner cannot change Status!)
        business.ShopName = model.ShopName;
        business.RegistrationNumber = model.RegistrationNumber;
        business.ContactPhone = model.ContactPhone;
        business.AddressLine = model.AddressLine;
        business.City = model.City;
        business.State = model.State;
        business.PostalCode = model.PostalCode;
        business.OperatingHours = model.OperatingHours;
        business.PickupInstructions = model.PickupInstructions;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business profile {BusinessId} updated by user {UserId}.", business.Id, user.Id);

        TempData["SuccessMessage"] = "Your business profile has been updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    // =========================================================================
    // PHASE 6: EQUIPMENT MANAGEMENT & INVENTORY CONTROLS
    // =========================================================================

    [HttpGet]
    [Route("Business/Equipment")]
    public async Task<IActionResult> Equipment()
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var items = await _equipmentService.GetBusinessEquipmentAsync(business.Id);

        var model = new BusinessEquipmentIndexViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            BusinessStatus = business.Status,
            Equipments = items
        };

        return View(model);
    }

    [HttpGet]
    [Route("Business/Equipment/Create")]
    public async Task<IActionResult> CreateEquipment()
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        if (business.Status != BusinessStatus.Approved)
        {
            TempData["ErrorMessage"] = "Only approved businesses are permitted to create or publish equipment listings.";
            return RedirectToAction(nameof(Equipment));
        }

        var categories = await GetActiveCategoryOptionsAsync();

        var model = new CreateEquipmentViewModel
        {
            AvailableCategories = categories,
            TotalQuantity = 1,
            IsActive = true
        };

        return View(model);
    }

    [HttpPost]
    [Route("Business/Equipment/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipment(CreateEquipmentViewModel model)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        if (business.Status != BusinessStatus.Approved)
        {
            TempData["ErrorMessage"] = "Only approved businesses are permitted to publish equipment listings.";
            return RedirectToAction(nameof(Equipment));
        }

        if (!ModelState.IsValid)
        {
            model.AvailableCategories = await GetActiveCategoryOptionsAsync();
            return View(model);
        }

        // Parse images
        var imageUrls = new List<string>();
        if (!string.IsNullOrWhiteSpace(model.PrimaryImageUrl))
        {
            imageUrls.Add(model.PrimaryImageUrl.Trim());
        }

        if (!string.IsNullOrWhiteSpace(model.AdditionalImageUrls))
        {
            var extraLines = model.AdditionalImageUrls.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in extraLines)
            {
                var clean = line.Trim();
                if (!string.IsNullOrWhiteSpace(clean) && !imageUrls.Contains(clean))
                {
                    imageUrls.Add(clean);
                }
            }
        }

        var dto = new CreateEquipmentDto
        {
            Title = model.Title,
            Description = model.Description,
            CategoryId = model.CategoryId,
            DailyRentalRate = model.DailyRentalRate,
            SecurityDeposit = model.SecurityDeposit,
            TotalQuantity = model.TotalQuantity,
            Condition = model.Condition,
            IsActive = model.IsActive,
            ImageUrls = imageUrls
        };

        var result = await _equipmentService.CreateEquipmentAsync(business.Id, dto);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create equipment listing.");
            model.AvailableCategories = await GetActiveCategoryOptionsAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Equipment listing '{model.Title}' has been successfully created.";
        return RedirectToAction(nameof(Equipment));
    }

    [HttpGet]
    [Route("Business/Equipment/Edit/{id:int}")]
    public async Task<IActionResult> EditEquipment(int id)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var equipment = await _equipmentService.GetEquipmentForBusinessAsync(id, business.Id);
        if (equipment == null)
        {
            return NotFound();
        }

        var categories = await GetActiveCategoryOptionsAsync();
        var maxReserved = await _equipmentService.GetMaxActiveReservedQuantityAsync(id);
        var activeMaintenance = equipment.MaintenanceRecords
            .Where(m => !m.IsResolved && m.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && m.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .Sum(m => m.BlockedQuantity);

        var model = new EditEquipmentViewModel
        {
            EquipmentId = equipment.Id,
            BusinessId = business.Id,
            Title = equipment.Title,
            Description = equipment.Description,
            CategoryId = equipment.CategoryId,
            DailyRentalRate = equipment.DailyRentalRate,
            SecurityDeposit = equipment.SecurityDeposit,
            TotalQuantity = equipment.TotalQuantity,
            Condition = equipment.Condition,
            IsActive = equipment.IsActive,
            RowVersion = equipment.RowVersion,
            ActiveReservationsCount = maxReserved,
            ActiveMaintenanceBlockedCount = activeMaintenance,
            AvailableCategories = categories,
            ExistingImages = equipment.Images.Select(i => new EquipmentImageViewModel
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                DisplayOrder = i.DisplayOrder
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Route("Business/Equipment/Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEquipment(int id, EditEquipmentViewModel model)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
        {
            await RepopulateEditViewModelAsync(model, id, business.Id);
            return View(model);
        }

        var dto = new UpdateEquipmentDto
        {
            Title = model.Title,
            Description = model.Description,
            CategoryId = model.CategoryId,
            DailyRentalRate = model.DailyRentalRate,
            SecurityDeposit = model.SecurityDeposit,
            TotalQuantity = model.TotalQuantity,
            Condition = model.Condition,
            IsActive = model.IsActive,
            RowVersion = model.RowVersion
        };

        var result = await _equipmentService.UpdateEquipmentAsync(id, business.Id, dto);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update equipment listing.");
            await RepopulateEditViewModelAsync(model, id, business.Id);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Listing '{model.Title}' updated successfully.";
        return RedirectToAction(nameof(Equipment));
    }

    [HttpPost]
    [Route("Business/Equipment/ToggleStatus/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEquipmentStatus(int id)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.ToggleEquipmentStatusAsync(id, business.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment listing status updated successfully.";
        }

        return RedirectToAction(nameof(Equipment));
    }

    [HttpGet]
    [Route("Business/Equipment/Maintenance/{id:int}")]
    public async Task<IActionResult> Maintenance(int id)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var equipment = await _equipmentService.GetEquipmentForBusinessAsync(id, business.Id);
        if (equipment == null)
        {
            return NotFound();
        }

        var records = await _equipmentService.GetMaintenanceRecordsAsync(id, business.Id);
        var activeReserved = await _equipmentService.GetMaxActiveReservedQuantityAsync(id);

        var model = new EquipmentMaintenanceViewModel
        {
            EquipmentId = equipment.Id,
            EquipmentTitle = equipment.Title,
            TotalQuantity = equipment.TotalQuantity,
            ActiveReservedCount = activeReserved,
            Records = records.Select(m => new MaintenanceRecordViewModel
            {
                Id = m.Id,
                BlockedQuantity = m.BlockedQuantity,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                Reason = m.Reason,
                EstimatedCost = m.EstimatedCost,
                IsResolved = m.IsResolved,
                CreatedAtUtc = m.CreatedAtUtc,
                ResolvedAtUtc = m.ResolvedAtUtc
            }).ToList(),
            NewRecord = new AddMaintenanceRecordInput
            {
                BlockedQuantity = 1,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
            }
        };

        return View(model);
    }

    [HttpPost]
    [Route("Business/Equipment/Maintenance/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMaintenance(int id, EquipmentMaintenanceViewModel model)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
        {
            var equipment = await _equipmentService.GetEquipmentForBusinessAsync(id, business.Id);
            if (equipment == null) return NotFound();

            model.EquipmentId = equipment.Id;
            model.EquipmentTitle = equipment.Title;
            model.TotalQuantity = equipment.TotalQuantity;
            model.Records = (await _equipmentService.GetMaintenanceRecordsAsync(id, business.Id))
                .Select(m => new MaintenanceRecordViewModel
                {
                    Id = m.Id,
                    BlockedQuantity = m.BlockedQuantity,
                    StartDate = m.StartDate,
                    EndDate = m.EndDate,
                    Reason = m.Reason,
                    EstimatedCost = m.EstimatedCost,
                    IsResolved = m.IsResolved,
                    CreatedAtUtc = m.CreatedAtUtc,
                    ResolvedAtUtc = m.ResolvedAtUtc
                }).ToList();

            return View("Maintenance", model);
        }

        var dto = new AddMaintenanceRecordDto
        {
            BlockedQuantity = model.NewRecord.BlockedQuantity,
            StartDate = model.NewRecord.StartDate,
            EndDate = model.NewRecord.EndDate,
            Reason = model.NewRecord.Reason,
            EstimatedCost = model.NewRecord.EstimatedCost
        };

        var result = await _equipmentService.AddMaintenanceRecordAsync(id, business.Id, dto);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Maintenance downtime record successfully logged.";
        }

        return RedirectToAction(nameof(Maintenance), new { id });
    }

    [HttpPost]
    [Route("Business/Equipment/Maintenance/{equipmentId:int}/Resolve/{recordId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveMaintenance(int equipmentId, int recordId)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.ResolveMaintenanceRecordAsync(recordId, equipmentId, business.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Maintenance record marked as resolved. Inventory has returned to active availability.";
        }

        return RedirectToAction(nameof(Maintenance), new { id = equipmentId });
    }

    [HttpPost]
    [Route("Business/Equipment/Maintenance/{equipmentId:int}/Delete/{recordId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMaintenance(int equipmentId, int recordId)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.DeleteMaintenanceRecordAsync(recordId, equipmentId, business.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Maintenance record deleted successfully.";
        }

        return RedirectToAction(nameof(Maintenance), new { id = equipmentId });
    }

    [HttpPost]
    [Route("Business/Equipment/Images/Add/{equipmentId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImage(int equipmentId, string imageUrl, bool isPrimary = false)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.AddEquipmentImageAsync(equipmentId, business.Id, imageUrl, isPrimary);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Image added successfully.";
        }

        return RedirectToAction(nameof(EditEquipment), new { id = equipmentId });
    }

    [HttpPost]
    [Route("Business/Equipment/Images/Delete/{equipmentId:int}/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int equipmentId, int imageId)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.DeleteEquipmentImageAsync(imageId, equipmentId, business.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Image removed successfully.";
        }

        return RedirectToAction(nameof(EditEquipment), new { id = equipmentId });
    }

    [HttpPost]
    [Route("Business/Equipment/Images/SetPrimary/{equipmentId:int}/{imageId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int equipmentId, int imageId)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _equipmentService.SetPrimaryImageAsync(imageId, equipmentId, business.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Primary photo updated.";
        }

        return RedirectToAction(nameof(EditEquipment), new { id = equipmentId });
    }

    // =========================================================================
    // BOOKING LIFECYCLE MANAGEMENT (PHASE 7)
    // =========================================================================

    [HttpGet]
    [Route("Business/Bookings")]
    public async Task<IActionResult> Bookings(BookingGroupStatus? status = null, int page = 1)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var (items, totalCount) = await _bookingService.GetBusinessBookingGroupsAsync(business.Id, status, page, pageSize: 15);
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 15.0);
        ViewBag.TotalCount = totalCount;
        ViewBag.Business = business;

        return View(items);
    }

    [HttpGet]
    [Route("Business/Bookings/Details/{id:int}")]
    public async Task<IActionResult> BookingGroupDetails(int id)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var groupDetails = await _bookingService.GetBusinessBookingGroupDetailsAsync(id, business.Id);
        if (groupDetails == null)
        {
            return NotFound();
        }

        ViewBag.Business = business;
        return View(groupDetails);
    }

    [HttpPost]
    [Route("Business/Bookings/Accept/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptBooking(int id)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _bookingService.AcceptBookingGroupAsync(id, business.Id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not accept booking.";
        }
        else
        {
            TempData["SuccessMessage"] = "Reservation accepted and confirmed. Customer notified for pickup.";
        }

        return RedirectToAction(nameof(BookingGroupDetails), new { id });
    }

    [HttpPost]
    [Route("Business/Bookings/Reject/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id, string? rejectionReason)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _bookingService.RejectBookingGroupAsync(id, business.Id, rejectionReason);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not decline booking.";
        }
        else
        {
            TempData["SuccessMessage"] = "Reservation declined. Held inventory units have been released.";
        }

        return RedirectToAction(nameof(BookingGroupDetails), new { id });
    }

    [HttpPost]
    [Route("Business/Bookings/Handover/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HandoverBooking(int id, string verificationCode, string? handoverNotes)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _bookingService.HandoverBookingGroupAsync(id, business.Id, verificationCode, handoverNotes);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Handover verification failed.";
        }
        else
        {
            TempData["SuccessMessage"] = "Equipment pickup verified and handed over to customer successfully!";
        }

        return RedirectToAction(nameof(BookingGroupDetails), new { id });
    }

    [HttpPost]
    [Route("Business/Bookings/Complete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteBooking(int id, string? inspectionNotes)
    {
        var business = await GetCurrentBusinessAsync();
        if (business == null) return RedirectToAction(nameof(Dashboard));

        var result = await _bookingService.CompleteReturnBookingGroupAsync(id, business.Id, inspectionNotes);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not complete return.";
        }
        else
        {
            TempData["SuccessMessage"] = "Return inspection recorded and booking completed! Units restored to active inventory.";
        }

        return RedirectToAction(nameof(BookingGroupDetails), new { id });
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private async Task<BusinessProfile?> GetCurrentBusinessAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;

        return await _businessService.GetBusinessByUserIdAsync(user.Id);
    }

    private async Task<List<CategorySelectOption>> GetActiveCategoryOptionsAsync()
    {
        var categories = await _catalogService.GetCategoriesWithCountsAsync();
        return categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategorySelectOption
            {
                Id = c.Id,
                Name = c.Name
            }).ToList();
    }

    private async Task RepopulateEditViewModelAsync(EditEquipmentViewModel model, int equipmentId, int businessId)
    {
        var equipment = await _equipmentService.GetEquipmentForBusinessAsync(equipmentId, businessId);
        if (equipment != null)
        {
            model.RowVersion = equipment.RowVersion;
            model.ActiveReservationsCount = await _equipmentService.GetMaxActiveReservedQuantityAsync(equipmentId);
            model.ActiveMaintenanceBlockedCount = equipment.MaintenanceRecords
                .Where(m => !m.IsResolved && m.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && m.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .Sum(m => m.BlockedQuantity);

            model.ExistingImages = equipment.Images.Select(i => new EquipmentImageViewModel
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                DisplayOrder = i.DisplayOrder
            }).ToList();
        }

        model.AvailableCategories = await GetActiveCategoryOptionsAsync();
    }
}
