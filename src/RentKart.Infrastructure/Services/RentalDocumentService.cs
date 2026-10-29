using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class RentalDocumentService : IRentalDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RentalDocumentService> _logger;

    public RentalDocumentService(ApplicationDbContext dbContext, ILogger<RentalDocumentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DocumentOperationResult> GenerateRentalAgreementAsync(int bookingGroupId)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.Customer)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
                    .ThenInclude(e => e.Category)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null)
        {
            return DocumentOperationResult.Fail($"Booking group with ID {bookingGroupId} was not found.");
        }

        try
        {
            string docNumber = $"AGR-{group.GroupNumber}";
            string title = $"Rental Agreement - {group.Business.ShopName} (#{group.GroupNumber})";
            string fileName = $"RentalAgreement_{group.GroupNumber}.html";

            string html = BuildAgreementHtml(group, docNumber);

            var existingDoc = await _dbContext.RentalDocuments
                .FirstOrDefaultAsync(d => d.BookingGroupId == bookingGroupId && d.DocumentType == RentalDocumentType.RentalAgreement);

            if (existingDoc != null)
            {
                existingDoc.Title = title;
                existingDoc.Content = html;
                existingDoc.FileName = fileName;
                existingDoc.RentalSubtotal = group.SubtotalRentalFee;
                existingDoc.SecurityDeposit = group.SubtotalSecurityDeposit;
                existingDoc.GrandTotal = group.SubtotalRentalFee + group.SubtotalSecurityDeposit;
                existingDoc.IsGenerated = true;
                existingDoc.FailureReason = null;
                existingDoc.GeneratedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return DocumentOperationResult.Ok(existingDoc.Id, existingDoc.DocumentNumber);
            }

            var doc = new RentalDocument
            {
                DocumentNumber = docNumber,
                DocumentType = RentalDocumentType.RentalAgreement,
                BookingId = group.ParentBookingId,
                BookingGroupId = group.Id,
                BusinessId = group.BusinessId,
                CustomerUserId = group.ParentBooking.CustomerId,
                Title = title,
                FileName = fileName,
                ContentType = "text/html",
                Content = html,
                RentalSubtotal = group.SubtotalRentalFee,
                SecurityDeposit = group.SubtotalSecurityDeposit,
                GrandTotal = group.SubtotalRentalFee + group.SubtotalSecurityDeposit,
                IsGenerated = true,
                FailureReason = null,
                GeneratedAtUtc = DateTime.UtcNow
            };

            _dbContext.RentalDocuments.Add(doc);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Generated Rental Agreement {DocNumber} for Group {GroupId}.", docNumber, bookingGroupId);
            return DocumentOperationResult.Ok(doc.Id, doc.DocumentNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Rental Agreement for Group {GroupId}.", bookingGroupId);
            await RecordFailedDocumentGenerationAsync(group.ParentBookingId, group.Id, group.BusinessId, group.ParentBooking.CustomerId,
                RentalDocumentType.RentalAgreement, $"AGR-{group.GroupNumber}", ex.Message);

            return DocumentOperationResult.Fail("Document generation failed: " + ex.Message);
        }
    }

    public async Task<DocumentOperationResult> GenerateStoreInvoiceAsync(int bookingGroupId)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.Customer)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
                    .ThenInclude(e => e.Category)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null)
        {
            return DocumentOperationResult.Fail($"Booking group with ID {bookingGroupId} was not found.");
        }

        try
        {
            string docNumber = $"INV-{group.GroupNumber}";
            string title = $"Store Invoice - {group.Business.ShopName} (#{group.GroupNumber})";
            string fileName = $"StoreInvoice_{group.GroupNumber}.html";

            string html = BuildStoreInvoiceHtml(group, docNumber);

            var existingDoc = await _dbContext.RentalDocuments
                .FirstOrDefaultAsync(d => d.BookingGroupId == bookingGroupId && d.DocumentType == RentalDocumentType.StoreInvoice);

            if (existingDoc != null)
            {
                existingDoc.Title = title;
                existingDoc.Content = html;
                existingDoc.FileName = fileName;
                existingDoc.RentalSubtotal = group.SubtotalRentalFee;
                existingDoc.SecurityDeposit = group.SubtotalSecurityDeposit;
                existingDoc.GrandTotal = group.SubtotalRentalFee + group.SubtotalSecurityDeposit;
                existingDoc.IsGenerated = true;
                existingDoc.FailureReason = null;
                existingDoc.GeneratedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return DocumentOperationResult.Ok(existingDoc.Id, existingDoc.DocumentNumber);
            }

            var doc = new RentalDocument
            {
                DocumentNumber = docNumber,
                DocumentType = RentalDocumentType.StoreInvoice,
                BookingId = group.ParentBookingId,
                BookingGroupId = group.Id,
                BusinessId = group.BusinessId,
                CustomerUserId = group.ParentBooking.CustomerId,
                Title = title,
                FileName = fileName,
                ContentType = "text/html",
                Content = html,
                RentalSubtotal = group.SubtotalRentalFee,
                SecurityDeposit = group.SubtotalSecurityDeposit,
                GrandTotal = group.SubtotalRentalFee + group.SubtotalSecurityDeposit,
                IsGenerated = true,
                FailureReason = null,
                GeneratedAtUtc = DateTime.UtcNow
            };

            _dbContext.RentalDocuments.Add(doc);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Generated Store Invoice {DocNumber} for Group {GroupId}.", docNumber, bookingGroupId);
            return DocumentOperationResult.Ok(doc.Id, doc.DocumentNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Store Invoice for Group {GroupId}.", bookingGroupId);
            await RecordFailedDocumentGenerationAsync(group.ParentBookingId, group.Id, group.BusinessId, group.ParentBooking.CustomerId,
                RentalDocumentType.StoreInvoice, $"INV-{group.GroupNumber}", ex.Message);

            return DocumentOperationResult.Fail("Invoice generation failed: " + ex.Message);
        }
    }

    public async Task<DocumentOperationResult> GenerateParentBookingInvoiceAsync(int bookingId)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.Business)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
                    .ThenInclude(bi => bi.Equipment)
                        .ThenInclude(e => e.Category)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return DocumentOperationResult.Fail($"Booking with ID {bookingId} was not found.");
        }

        try
        {
            string docNumber = $"INV-{booking.BookingNumber}";
            string title = $"Consolidated Booking Invoice (#{booking.BookingNumber})";
            string fileName = $"BookingInvoice_{booking.BookingNumber}.html";

            string html = BuildParentBookingInvoiceHtml(booking, docNumber);

            var existingDoc = await _dbContext.RentalDocuments
                .FirstOrDefaultAsync(d => d.BookingId == bookingId && d.DocumentType == RentalDocumentType.ParentBookingInvoice);

            if (existingDoc != null)
            {
                existingDoc.Title = title;
                existingDoc.Content = html;
                existingDoc.FileName = fileName;
                existingDoc.RentalSubtotal = booking.TotalRentalFee;
                existingDoc.SecurityDeposit = booking.TotalSecurityDeposit;
                existingDoc.GrandTotal = booking.GrandTotal;
                existingDoc.IsGenerated = true;
                existingDoc.FailureReason = null;
                existingDoc.GeneratedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return DocumentOperationResult.Ok(existingDoc.Id, existingDoc.DocumentNumber);
            }

            var doc = new RentalDocument
            {
                DocumentNumber = docNumber,
                DocumentType = RentalDocumentType.ParentBookingInvoice,
                BookingId = booking.Id,
                BookingGroupId = null,
                BusinessId = null, // Parent-level consolidated
                CustomerUserId = booking.CustomerId,
                Title = title,
                FileName = fileName,
                ContentType = "text/html",
                Content = html,
                RentalSubtotal = booking.TotalRentalFee,
                SecurityDeposit = booking.TotalSecurityDeposit,
                GrandTotal = booking.GrandTotal,
                IsGenerated = true,
                FailureReason = null,
                GeneratedAtUtc = DateTime.UtcNow
            };

            _dbContext.RentalDocuments.Add(doc);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Generated Parent Booking Invoice {DocNumber} for Booking {BookingId}.", docNumber, bookingId);
            return DocumentOperationResult.Ok(doc.Id, doc.DocumentNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Parent Booking Invoice for Booking {BookingId}.", bookingId);
            await RecordFailedDocumentGenerationAsync(booking.Id, null, null, booking.CustomerId,
                RentalDocumentType.ParentBookingInvoice, $"INV-{booking.BookingNumber}", ex.Message);

            return DocumentOperationResult.Fail("Consolidated invoice generation failed: " + ex.Message);
        }
    }

    public async Task<RentalDocumentDto?> GetDocumentDetailsAsync(int documentId, string currentUserId, bool isAdmin = false)
    {
        var doc = await _dbContext.RentalDocuments
            .Include(d => d.Business)
            .Include(d => d.CustomerUser)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (doc == null) return null;

        // Authorization validation
        if (!isAdmin)
        {
            bool isCustomer = string.Equals(doc.CustomerUserId, currentUserId, StringComparison.OrdinalIgnoreCase);
            if (!isCustomer)
            {
                // Check if user is the business owner
                if (doc.BusinessId.HasValue && doc.DocumentType != RentalDocumentType.ParentBookingInvoice)
                {
                    var business = await _dbContext.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == currentUserId);
                    if (business == null || business.Id != doc.BusinessId.Value)
                    {
                        return null; // Forbidden: Business cannot access another business's document
                    }
                }
                else
                {
                    return null; // Forbidden: Business cannot access parent consolidated invoices
                }
            }
        }

        return new RentalDocumentDto
        {
            Id = doc.Id,
            DocumentNumber = doc.DocumentNumber,
            DocumentType = doc.DocumentType,
            BookingId = doc.BookingId,
            BookingGroupId = doc.BookingGroupId,
            BusinessId = doc.BusinessId,
            BusinessName = doc.Business?.ShopName,
            CustomerUserId = doc.CustomerUserId,
            CustomerName = doc.CustomerUser.FullName,
            CustomerEmail = doc.CustomerUser.Email ?? string.Empty,
            Title = doc.Title,
            FileName = doc.FileName,
            ContentType = doc.ContentType,
            Content = doc.Content,
            RentalSubtotal = doc.RentalSubtotal,
            SecurityDeposit = doc.SecurityDeposit,
            GrandTotal = doc.GrandTotal,
            IsGenerated = doc.IsGenerated,
            FailureReason = doc.FailureReason,
            GeneratedAtUtc = doc.GeneratedAtUtc
        };
    }

    public async Task<(List<RentalDocumentSummaryDto> Items, int TotalCount)> GetCustomerDocumentsAsync(string customerUserId, int page = 1, int pageSize = 15)
    {
        var query = _dbContext.RentalDocuments
            .Include(d => d.Business)
            .Where(d => d.CustomerUserId == customerUserId);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.GeneratedAtUtc)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new RentalDocumentSummaryDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                DocumentType = d.DocumentType,
                BookingId = d.BookingId,
                BookingGroupId = d.BookingGroupId,
                BusinessName = d.Business != null ? d.Business.ShopName : "RentKart Consolidated",
                Title = d.Title,
                RentalSubtotal = d.RentalSubtotal,
                SecurityDeposit = d.SecurityDeposit,
                GrandTotal = d.GrandTotal,
                IsGenerated = d.IsGenerated,
                FailureReason = d.FailureReason,
                GeneratedAtUtc = d.GeneratedAtUtc
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<RentalDocumentSummaryDto> Items, int TotalCount)> GetBusinessDocumentsAsync(int businessId, int page = 1, int pageSize = 15)
    {
        // Strictly isolated: only documents explicitly belonging to this business, excluding parent consolidated documents
        var query = _dbContext.RentalDocuments
            .Where(d => d.BusinessId == businessId && d.DocumentType != RentalDocumentType.ParentBookingInvoice);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.GeneratedAtUtc)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new RentalDocumentSummaryDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                DocumentType = d.DocumentType,
                BookingId = d.BookingId,
                BookingGroupId = d.BookingGroupId,
                BusinessName = null,
                Title = d.Title,
                RentalSubtotal = d.RentalSubtotal,
                SecurityDeposit = d.SecurityDeposit,
                GrandTotal = d.GrandTotal,
                IsGenerated = d.IsGenerated,
                FailureReason = d.FailureReason,
                GeneratedAtUtc = d.GeneratedAtUtc
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<DocumentDownloadResult> GetDocumentForDownloadAsync(int documentId, string currentUserId, bool isAdmin = false)
    {
        var doc = await GetDocumentDetailsAsync(documentId, currentUserId, isAdmin);
        if (doc == null)
        {
            return DocumentDownloadResult.Fail("Document not found or access denied.");
        }

        if (!doc.IsGenerated || string.IsNullOrWhiteSpace(doc.Content))
        {
            return DocumentDownloadResult.Fail($"Document generation was not completed: {doc.FailureReason ?? "Unknown generation failure"}.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(doc.Content);
        return DocumentDownloadResult.Ok(doc.FileName, doc.ContentType, bytes);
    }

    public async Task<DocumentOperationResult> RetryDocumentGenerationAsync(int documentId, string currentUserId, bool isAdmin = false)
    {
        var doc = await _dbContext.RentalDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc == null)
        {
            return DocumentOperationResult.Fail("Document not found.");
        }

        // Authorization check
        if (!isAdmin && doc.CustomerUserId != currentUserId)
        {
            var biz = await _dbContext.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == currentUserId);
            if (biz == null || doc.BusinessId != biz.Id)
            {
                return DocumentOperationResult.Fail("Access denied to regenerate this document.");
            }
        }

        switch (doc.DocumentType)
        {
            case RentalDocumentType.RentalAgreement when doc.BookingGroupId.HasValue:
                return await GenerateRentalAgreementAsync(doc.BookingGroupId.Value);

            case RentalDocumentType.StoreInvoice when doc.BookingGroupId.HasValue:
                return await GenerateStoreInvoiceAsync(doc.BookingGroupId.Value);

            case RentalDocumentType.ParentBookingInvoice when doc.BookingId.HasValue:
                return await GenerateParentBookingInvoiceAsync(doc.BookingId.Value);

            default:
                return DocumentOperationResult.Fail("Invalid document configuration for retry.");
        }
    }

    private async Task RecordFailedDocumentGenerationAsync(int? bookingId, int? bookingGroupId, int? businessId, string customerUserId,
        RentalDocumentType type, string docNumber, string failureReason)
    {
        try
        {
            var existing = await _dbContext.RentalDocuments
                .FirstOrDefaultAsync(d => d.DocumentNumber == docNumber);

            if (existing != null)
            {
                existing.IsGenerated = false;
                existing.FailureReason = failureReason;
                existing.GeneratedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _dbContext.RentalDocuments.Add(new RentalDocument
                {
                    DocumentNumber = docNumber,
                    DocumentType = type,
                    BookingId = bookingId,
                    BookingGroupId = bookingGroupId,
                    BusinessId = businessId,
                    CustomerUserId = customerUserId,
                    Title = $"Failed Document ({docNumber})",
                    FileName = $"{docNumber}.html",
                    ContentType = "text/html",
                    Content = string.Empty,
                    IsGenerated = false,
                    FailureReason = failureReason,
                    GeneratedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record failed document state for {DocNumber}.", docNumber);
        }
    }

    // =========================================================================
    // AUTHORITATIVE HTML DOCUMENT BUILDERS
    // =========================================================================

    private static string BuildAgreementHtml(BookingGroup group, string docNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"<title>{docNumber} - Equipment Rental Agreement</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #1e293b; margin: 0; padding: 40px; background: #f8fafc; }");
        sb.AppendLine(".doc-container { max-width: 820px; margin: 0 auto; background: #ffffff; padding: 48px; border-radius: 8px; border: 1px solid #e2e8f0; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }");
        sb.AppendLine(".doc-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #0f172a; padding-bottom: 24px; margin-bottom: 24px; }");
        sb.AppendLine(".brand-name { font-size: 26px; font-weight: 900; color: #0f172a; margin: 0; }");
        sb.AppendLine(".doc-title { font-size: 18px; font-weight: 700; color: #0d9488; text-transform: uppercase; margin: 4px 0 0; }");
        sb.AppendLine(".doc-meta { font-size: 12px; color: #64748b; text-align: right; }");
        sb.AppendLine(".parties-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-bottom: 32px; background: #f8fafc; padding: 20px; border-radius: 6px; border: 1px solid #e2e8f0; }");
        sb.AppendLine(".party-title { font-size: 11px; text-transform: uppercase; font-weight: 700; color: #64748b; margin-bottom: 6px; }");
        sb.AppendLine(".party-name { font-size: 16px; font-weight: 700; color: #0f172a; margin-bottom: 4px; }");
        sb.AppendLine(".party-info { font-size: 13px; color: #334155; margin: 2px 0; }");
        sb.AppendLine(".items-table { width: 100%; border-collapse: collapse; margin-bottom: 24px; font-size: 13px; }");
        sb.AppendLine(".items-table th { background: #f1f5f9; padding: 10px 12px; text-align: left; font-weight: 700; color: #334155; border-bottom: 2px solid #cbd5e1; }");
        sb.AppendLine(".items-table td { padding: 12px; border-bottom: 1px solid #e2e8f0; }");
        sb.AppendLine(".totals-box { margin-left: auto; width: 320px; margin-bottom: 32px; font-size: 14px; }");
        sb.AppendLine(".totals-row { display: flex; justify-content: space-between; padding: 6px 0; }");
        sb.AppendLine(".totals-row.grand { border-top: 2px solid #0f172a; margin-top: 6px; padding-top: 10px; font-weight: 800; font-size: 16px; color: #0f172a; }");
        sb.AppendLine(".terms-section { margin-top: 32px; border-top: 1px solid #e2e8f0; padding-top: 24px; font-size: 11px; color: #475569; }");
        sb.AppendLine(".terms-title { font-size: 13px; font-weight: 700; color: #0f172a; margin-bottom: 12px; text-transform: uppercase; }");
        sb.AppendLine(".terms-clause { margin-bottom: 10px; }");
        sb.AppendLine(".disclaimer-box { background: #fefce8; border: 1px solid #fef08a; padding: 12px; border-radius: 6px; font-size: 12px; color: #854d0e; margin: 20px 0; font-weight: 600; }");
        sb.AppendLine(".signatures { display: grid; grid-template-columns: 1fr 1fr; gap: 48px; margin-top: 48px; padding-top: 24px; border-top: 1px dashed #cbd5e1; }");
        sb.AppendLine(".sig-line { border-top: 1px solid #94a3b8; margin-top: 40px; padding-top: 6px; font-size: 11px; color: #64748b; text-align: center; }");
        sb.AppendLine("@media print { body { background: #fff; padding: 0; } .doc-container { box-shadow: none; border: none; padding: 0; } .no-print { display: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"doc-container\">");

        // Action Toolbar
        sb.AppendLine("<div class=\"no-print\" style=\"display: flex; justify-content: flex-end; gap: 8px; margin-bottom: 20px;\">");
        sb.AppendLine("<button onclick=\"window.print()\" style=\"background: #0d9488; color: #fff; border: none; padding: 8px 16px; border-radius: 4px; font-weight: 600; cursor: pointer;\">Print / Save as PDF</button>");
        sb.AppendLine("</div>");

        // Header
        sb.AppendLine("<div class=\"doc-header\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<h1 class=\"brand-name\">RentKart</h1>");
        sb.AppendLine("<div class=\"doc-title\">Equipment Rental Agreement</div>");
        sb.AppendLine($"<div style=\"font-size: 12px; color: #64748b; margin-top: 4px;\">Store Voucher Group: <strong>#{group.GroupNumber}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div class=\"doc-meta\">");
        sb.AppendLine($"<div>Document Ref: <strong>{docNumber}</strong></div>");
        sb.AppendLine($"<div>Generated: <strong>{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</strong></div>");
        sb.AppendLine($"<div>Parent Order: #{group.ParentBooking.BookingNumber}</div>");
        sb.AppendLine($"<div>Pickup Code: <strong style=\"font-family: monospace; color: #0d9488;\">{group.PickupVerificationCode}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Disclaimer
        sb.AppendLine("<div class=\"disclaimer-box\">");
        sb.AppendLine("NOTICE: This document is an authoritative rental agreement snapshot based on confirmed booking parameters. Counter inspection and handover occur directly at the store location. No online financial settlement or payment gateway processing is simulated.");
        sb.AppendLine("</div>");

        // Parties
        sb.AppendLine("<div class=\"parties-grid\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"party-title\">Lessor (Equipment Store)</div>");
        sb.AppendLine($"<div class=\"party-name\">{group.Business.ShopName}</div>");
        sb.AppendLine($"<div class=\"party-info\">{group.Business.AddressLine}, {group.Business.City}, {group.Business.PostalCode}</div>");
        sb.AppendLine($"<div class=\"party-info\">Phone: {group.Business.ContactPhone}</div>");
        sb.AppendLine($"<div class=\"party-info\">Hours: {group.Business.OperatingHours}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"party-title\">Lessee (Customer)</div>");
        sb.AppendLine($"<div class=\"party-name\">{group.ParentBooking.Customer.FullName}</div>");
        sb.AppendLine($"<div class=\"party-info\">Email: {group.ParentBooking.Customer.Email}</div>");
        sb.AppendLine($"<div class=\"party-info\">Phone: {group.ParentBooking.Customer.PhoneNumber ?? "Not provided"}</div>");
        sb.AppendLine($"<div class=\"party-info\">Customer Ref: {group.ParentBooking.CustomerId}</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Items Table
        sb.AppendLine("<table class=\"items-table\">");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr>");
        sb.AppendLine("<th>Equipment Description</th>");
        sb.AppendLine("<th style=\"text-align: center;\">Qty</th>");
        sb.AppendLine("<th>Rental Period</th>");
        sb.AppendLine("<th style=\"text-align: right;\">Daily Rate</th>");
        sb.AppendLine("<th style=\"text-align: right;\">Deposit</th>");
        sb.AppendLine("<th style=\"text-align: right;\">Total Rental</th>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");

        foreach (var item in group.BookingItems)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><strong>{item.Equipment.Title}</strong><div style=\"font-size: 11px; color: #64748b;\">{item.Equipment.Category?.Name}</div></td>");
            sb.AppendLine($"<td style=\"text-align: center;\">{item.Quantity}</td>");
            sb.AppendLine($"<td>{item.StartDate:MMM dd, yyyy} &rarr; {item.EndDate:MMM dd, yyyy}<div style=\"font-size: 11px; color: #64748b;\">({item.DaysCount} days)</div></td>");
            sb.AppendLine($"<td style=\"text-align: right;\">${item.DailyRentalRate:F2}</td>");
            sb.AppendLine($"<td style=\"text-align: right;\">${item.ItemSecurityDeposit:F2}</td>");
            sb.AppendLine($"<td style=\"text-align: right; font-weight: 700;\">${item.TotalItemPrice:F2}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        // Financial Totals
        sb.AppendLine("<div class=\"totals-box\">");
        sb.AppendLine("<div class=\"totals-row\"><span>Rental Charges Subtotal:</span><span>$" + group.SubtotalRentalFee.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row\"><span>Refundable Security Deposit:</span><span>$" + group.SubtotalSecurityDeposit.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row grand\"><span>Agreed Total Payable:</span><span>$" + (group.SubtotalRentalFee + group.SubtotalSecurityDeposit).ToString("F2") + "</span></div>");
        sb.AppendLine("</div>");

        // Legal Clauses
        sb.AppendLine("<div class=\"terms-section\">");
        sb.AppendLine("<div class=\"terms-title\">Standard Terms & Conditions of Rental</div>");
        sb.AppendLine("<div class=\"terms-clause\"><strong>1. Counter Collection & Inspection:</strong> Lessee agrees to collect equipment in person at the Lessor's physical counter using Pickup Verification Code " + group.PickupVerificationCode + ". Equipment condition and complete accessories must be verified at handover.</div>");
        sb.AppendLine("<div class=\"terms-clause\"><strong>2. Return Schedule & Late Returns:</strong> All equipment must be returned to the Lessor's store by the agreed end date during normal operating hours. Unreturned equipment is subject to daily renewal rates.</div>");
        sb.AppendLine("<div class=\"terms-clause\"><strong>3. Security Deposit & Damage Liability:</strong> The refundable security deposit is held to ensure safe return. In the event of physical damage, broken components, or missing accessories, repair and replacement costs will be assessed and deducted from the deposit.</div>");
        sb.AppendLine("<div class=\"terms-clause\"><strong>4. Maintenance Quarantine:</strong> Equipment returned with damage or missing components will be placed into immediate maintenance downtime, blocking further reservation availability until recertified.</div>");
        sb.AppendLine("<div class=\"terms-clause\"><strong>5. No Delivery Fleet:</strong> RentKart provides discovery, scheduling, and contract generation. Collection and returns occur exclusively at store premises; no home delivery fleet is engaged.</div>");
        sb.AppendLine("</div>");

        // Signature Lines
        sb.AppendLine("<div class=\"signatures\">");
        sb.AppendLine("<div><div class=\"sig-line\">Authorized Store Representative (" + group.Business.ShopName + ")</div></div>");
        sb.AppendLine("<div><div class=\"sig-line\">Lessee Signature (" + group.ParentBooking.Customer.FullName + ")</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string BuildStoreInvoiceHtml(BookingGroup group, string docNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"<title>{docNumber} - Store Rental Invoice</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #1e293b; margin: 0; padding: 40px; background: #f8fafc; }");
        sb.AppendLine(".doc-container { max-width: 800px; margin: 0 auto; background: #ffffff; padding: 48px; border-radius: 8px; border: 1px solid #e2e8f0; }");
        sb.AppendLine(".doc-header { display: flex; justify-content: space-between; border-bottom: 2px solid #0f172a; padding-bottom: 20px; margin-bottom: 24px; }");
        sb.AppendLine(".invoice-badge { display: inline-block; background: #f1f5f9; padding: 4px 10px; border-radius: 4px; font-size: 11px; font-weight: 700; color: #475569; text-transform: uppercase; margin-bottom: 6px; }");
        sb.AppendLine(".items-table { width: 100%; border-collapse: collapse; margin-top: 24px; font-size: 13px; }");
        sb.AppendLine(".items-table th { background: #f8fafc; padding: 10px; text-align: left; border-bottom: 2px solid #e2e8f0; font-weight: 700; color: #475569; }");
        sb.AppendLine(".items-table td { padding: 12px 10px; border-bottom: 1px solid #f1f5f9; }");
        sb.AppendLine(".totals-box { margin-left: auto; width: 300px; margin-top: 24px; font-size: 14px; }");
        sb.AppendLine(".totals-row { display: flex; justify-content: space-between; padding: 6px 0; }");
        sb.AppendLine(".totals-row.grand { border-top: 2px solid #0f172a; margin-top: 6px; padding-top: 8px; font-weight: 800; font-size: 16px; }");
        sb.AppendLine(".notice { background: #f8fafc; border: 1px dashed #cbd5e1; padding: 14px; border-radius: 6px; font-size: 12px; color: #64748b; margin-top: 32px; text-align: center; }");
        sb.AppendLine("@media print { body { background: #fff; padding: 0; } .doc-container { border: none; padding: 0; } .no-print { display: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"doc-container\">");

        // Action Toolbar
        sb.AppendLine("<div class=\"no-print\" style=\"display: flex; justify-content: flex-end; margin-bottom: 16px;\">");
        sb.AppendLine("<button onclick=\"window.print()\" style=\"background: #0f172a; color: #fff; border: none; padding: 8px 16px; border-radius: 4px; font-weight: 600; cursor: pointer;\">Print / Save Invoice</button>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"doc-header\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<div class=\"invoice-badge\">Pro-Forma Quotation</div>");
        sb.AppendLine("<h1 style=\"font-size: 24px; font-weight: 800; color: #0f172a; margin: 0;\">Store Rental Invoice</h1>");
        sb.AppendLine($"<div style=\"font-size: 13px; color: #64748b; margin-top: 4px;\">Issued by: <strong>{group.Business.ShopName}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div style=\"text-align: right; font-size: 12px;\">");
        sb.AppendLine($"<div>Invoice No: <strong>{docNumber}</strong></div>");
        sb.AppendLine($"<div>Issue Date: <strong>{DateTime.UtcNow:yyyy-MM-dd}</strong></div>");
        sb.AppendLine($"<div>Group Ref: <strong>#{group.GroupNumber}</strong></div>");
        sb.AppendLine($"<div>Pickup Voucher: <strong style=\"color: #0d9488;\">{group.PickupVerificationCode}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Billing info
        sb.AppendLine("<div style=\"display: grid; grid-template-columns: 1fr 1fr; gap: 20px; font-size: 13px; margin-bottom: 24px;\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<div style=\"font-size: 11px; text-transform: uppercase; font-weight: 700; color: #94a3b8;\">Store Location</div>");
        sb.AppendLine($"<div style=\"font-weight: 700;\">{group.Business.ShopName}</div>");
        sb.AppendLine($"<div>{group.Business.AddressLine}, {group.Business.City}</div>");
        sb.AppendLine($"<div>Phone: {group.Business.ContactPhone}</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div>");
        sb.AppendLine("<div style=\"font-size: 11px; text-transform: uppercase; font-weight: 700; color: #94a3b8;\">Billed To Customer</div>");
        sb.AppendLine($"<div style=\"font-weight: 700;\">{group.ParentBooking.Customer.FullName}</div>");
        sb.AppendLine($"<div>Email: {group.ParentBooking.Customer.Email}</div>");
        sb.AppendLine($"<div>Phone: {group.ParentBooking.Customer.PhoneNumber ?? "Not provided"}</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Table
        sb.AppendLine("<table class=\"items-table\">");
        sb.AppendLine("<thead>");
        sb.AppendLine("<tr><th>Item</th><th style=\"text-align: center;\">Qty</th><th>Duration</th><th style=\"text-align: right;\">Daily Rate</th><th style=\"text-align: right;\">Deposit</th><th style=\"text-align: right;\">Amount</th></tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");
        foreach (var item in group.BookingItems)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td><strong>{item.Equipment.Title}</strong></td>");
            sb.AppendLine($"<td style=\"text-align: center;\">{item.Quantity}</td>");
            sb.AppendLine($"<td>{item.DaysCount} days</td>");
            sb.AppendLine($"<td style=\"text-align: right;\">${item.DailyRentalRate:F2}</td>");
            sb.AppendLine($"<td style=\"text-align: right;\">${item.ItemSecurityDeposit:F2}</td>");
            sb.AppendLine($"<td style=\"text-align: right; font-weight: 700;\">${item.TotalItemPrice:F2}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class=\"totals-box\">");
        sb.AppendLine("<div class=\"totals-row\"><span>Rental Fee:</span><span>$" + group.SubtotalRentalFee.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row\"><span>Refundable Deposit:</span><span>$" + group.SubtotalSecurityDeposit.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row grand\"><span>Agreed Payable:</span><span>$" + (group.SubtotalRentalFee + group.SubtotalSecurityDeposit).ToString("F2") + "</span></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"notice\">");
        sb.AppendLine("Pro-forma rental invoice for counter collection and return reconciliation. Security deposits are refundable subject to post-rental equipment inspection.");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string BuildParentBookingInvoiceHtml(Booking booking, string docNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"<title>{docNumber} - Consolidated Booking Invoice</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; color: #1e293b; margin: 0; padding: 40px; background: #f8fafc; }");
        sb.AppendLine(".doc-container { max-width: 820px; margin: 0 auto; background: #ffffff; padding: 48px; border-radius: 8px; border: 1px solid #e2e8f0; }");
        sb.AppendLine(".doc-header { display: flex; justify-content: space-between; border-bottom: 2px solid #0f172a; padding-bottom: 20px; margin-bottom: 24px; }");
        sb.AppendLine(".group-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin-bottom: 20px; }");
        sb.AppendLine(".group-header { display: flex; justify-content: space-between; font-weight: 700; border-bottom: 1px dashed #cbd5e1; padding-bottom: 8px; margin-bottom: 12px; font-size: 13px; }");
        sb.AppendLine(".items-table { width: 100%; border-collapse: collapse; font-size: 12px; }");
        sb.AppendLine(".items-table th { text-align: left; color: #64748b; padding: 6px 8px; font-size: 11px; text-transform: uppercase; }");
        sb.AppendLine(".items-table td { padding: 8px; border-bottom: 1px solid #e2e8f0; }");
        sb.AppendLine(".totals-box { margin-left: auto; width: 340px; margin-top: 24px; font-size: 14px; }");
        sb.AppendLine(".totals-row { display: flex; justify-content: space-between; padding: 6px 0; }");
        sb.AppendLine(".totals-row.grand { border-top: 2px solid #0f172a; margin-top: 6px; padding-top: 10px; font-weight: 900; font-size: 17px; color: #0d9488; }");
        sb.AppendLine("@media print { body { background: #fff; padding: 0; } .doc-container { border: none; padding: 0; } .no-print { display: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"doc-container\">");

        // Action Toolbar
        sb.AppendLine("<div class=\"no-print\" style=\"display: flex; justify-content: flex-end; margin-bottom: 16px;\">");
        sb.AppendLine("<button onclick=\"window.print()\" style=\"background: #0d9488; color: #fff; border: none; padding: 8px 16px; border-radius: 4px; font-weight: 600; cursor: pointer;\">Print / Save Invoice</button>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"doc-header\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<h1 style=\"font-size: 26px; font-weight: 900; color: #0f172a; margin: 0;\">RentKart</h1>");
        sb.AppendLine("<div style=\"font-size: 16px; font-weight: 700; color: #475569; text-transform: uppercase; margin-top: 2px;\">Consolidated Rental Invoice</div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<div style=\"text-align: right; font-size: 12px;\">");
        sb.AppendLine($"<div>Invoice No: <strong>{docNumber}</strong></div>");
        sb.AppendLine($"<div>Date: <strong>{DateTime.UtcNow:yyyy-MM-dd}</strong></div>");
        sb.AppendLine($"<div>Parent Booking: <strong>#{booking.BookingNumber}</strong></div>");
        sb.AppendLine($"<div>Stores Count: <strong>{booking.BookingGroups.Count}</strong></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Customer Details
        sb.AppendLine("<div style=\"margin-bottom: 24px; font-size: 13px; background: #f8fafc; padding: 14px; border-radius: 6px; border: 1px solid #e2e8f0;\">");
        sb.AppendLine("<div style=\"font-size: 11px; text-transform: uppercase; font-weight: 700; color: #64748b; margin-bottom: 4px;\">Customer Information</div>");
        sb.AppendLine($"<div style=\"font-weight: 700; font-size: 14px;\">{booking.Customer.FullName}</div>");
        sb.AppendLine($"<div>Email: {booking.Customer.Email} &bull; Phone: {booking.Customer.PhoneNumber ?? "Not provided"}</div>");
        sb.AppendLine("</div>");

        // Group cards
        foreach (var group in booking.BookingGroups)
        {
            sb.AppendLine("<div class=\"group-card\">");
            sb.AppendLine("<div class=\"group-header\">");
            sb.AppendLine($"<div>Store: <span style=\"color: #0f172a;\">{group.Business.ShopName}</span> (#{group.GroupNumber})</div>");
            sb.AppendLine($"<div>Voucher: <span style=\"color: #0d9488; font-family: monospace;\">{group.PickupVerificationCode}</span></div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<table class=\"items-table\">");
            sb.AppendLine("<thead><tr><th>Item</th><th style=\"text-align: center;\">Qty</th><th>Period</th><th style=\"text-align: right;\">Daily</th><th style=\"text-align: right;\">Subtotal</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var item in group.BookingItems)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td><strong>{item.Equipment.Title}</strong></td>");
                sb.AppendLine($"<td style=\"text-align: center;\">{item.Quantity}</td>");
                sb.AppendLine($"<td>{item.StartDate:MMM dd} - {item.EndDate:MMM dd}</td>");
                sb.AppendLine($"<td style=\"text-align: right;\">${item.DailyRentalRate:F2}</td>");
                sb.AppendLine($"<td style=\"text-align: right; font-weight: 700;\">${item.TotalItemPrice:F2}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            sb.AppendLine($"<div style=\"text-align: right; font-size: 12px; margin-top: 10px; color: #475569;\">Store Subtotal: Rental <strong>${group.SubtotalRentalFee:F2}</strong> + Deposit <strong>${group.SubtotalSecurityDeposit:F2}</strong> = <strong>${(group.SubtotalRentalFee + group.SubtotalSecurityDeposit):F2}</strong></div>");
            sb.AppendLine("</div>");
        }

        // Consolidated Totals
        sb.AppendLine("<div class=\"totals-box\">");
        sb.AppendLine("<div class=\"totals-row\"><span>Total Rental Fees:</span><span>$" + booking.TotalRentalFee.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row\"><span>Total Security Deposits:</span><span>$" + booking.TotalSecurityDeposit.ToString("F2") + "</span></div>");
        sb.AppendLine("<div class=\"totals-row grand\"><span>Grand Total Agreed:</span><span>$" + booking.GrandTotal.ToString("F2") + "</span></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div style=\"margin-top: 32px; font-size: 11px; color: #64748b; border-top: 1px solid #e2e8f0; padding-top: 16px; text-align: center;\">");
        sb.AppendLine("This consolidated invoice compiles agreed rates across all participating stores for Booking #" + booking.BookingNumber + ". Each store manages item collection, handover, and returns independently.");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
