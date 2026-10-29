using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private readonly IRentalDocumentService _documentService;
    private readonly IBusinessManagementService _businessService;

    public DocumentsController(
        IRentalDocumentService documentService,
        IBusinessManagementService businessService)
    {
        _documentService = documentService;
        _businessService = businessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        const int pageSize = 15;
        page = Math.Max(1, page);

        bool isBusinessUser = User.IsInRole("Business");
        List<RentalDocumentSummaryDto> items;
        int totalCount;

        if (isBusinessUser)
        {
            var business = await _businessService.GetBusinessByUserIdAsync(userId);
            if (business != null)
            {
                (items, totalCount) = await _documentService.GetBusinessDocumentsAsync(business.Id, page, pageSize);
                ViewBag.IsBusinessContext = true;
                ViewBag.ShopName = business.ShopName;
            }
            else
            {
                items = new List<RentalDocumentSummaryDto>();
                totalCount = 0;
            }
        }
        else
        {
            (items, totalCount) = await _documentService.GetCustomerDocumentsAsync(userId, page, pageSize);
            ViewBag.IsBusinessContext = false;
        }

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalCount = totalCount;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        bool isAdmin = User.IsInRole("Admin");
        var doc = await _documentService.GetDocumentDetailsAsync(id, userId, isAdmin);
        if (doc == null)
        {
            return NotFound("Document not found or access is restricted.");
        }

        if (!doc.IsGenerated || string.IsNullOrWhiteSpace(doc.Content))
        {
            return Content(
                $"<h3>Document Generation Pending or Failed</h3><p>Reason: {doc.FailureReason ?? "Generation error"}</p><p><a href='/Documents'>Back to Documents</a></p>",
                "text/html; charset=utf-8");
        }

        return Content(doc.Content, "text/html; charset=utf-8");
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        bool isAdmin = User.IsInRole("Admin");
        var result = await _documentService.GetDocumentForDownloadAsync(id, userId, isAdmin);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not download document.";
            return RedirectToAction(nameof(Index));
        }

        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        bool isAdmin = User.IsInRole("Admin");
        var result = await _documentService.RetryDocumentGenerationAsync(id, userId, isAdmin);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Document was regenerated successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Regeneration failed.";
        }

        return RedirectToAction(nameof(Index));
    }
}
