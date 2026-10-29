using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class DocumentOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DocumentId { get; set; }
    public string? DocumentNumber { get; set; }

    public static DocumentOperationResult Ok(int id, string number) =>
        new() { Success = true, DocumentId = id, DocumentNumber = number };

    public static DocumentOperationResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

public class RentalDocumentSummaryDto
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public RentalDocumentType DocumentType { get; set; }
    public int? BookingId { get; set; }
    public int? BookingGroupId { get; set; }
    public string? BusinessName { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal RentalSubtotal { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsGenerated { get; set; }
    public string? FailureReason { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class RentalDocumentDto
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public RentalDocumentType DocumentType { get; set; }
    public int? BookingId { get; set; }
    public int? BookingGroupId { get; set; }
    public int? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string CustomerUserId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal RentalSubtotal { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsGenerated { get; set; }
    public string? FailureReason { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class DocumentDownloadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/html";
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();

    public static DocumentDownloadResult Ok(string fileName, string contentType, byte[] bytes) =>
        new() { Success = true, FileName = fileName, ContentType = contentType, FileBytes = bytes };

    public static DocumentDownloadResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

public interface IRentalDocumentService
{
    /// <summary>
    /// Generates the store-specific equipment rental agreement for a confirmed booking group.
    /// Captures authoritative rate snapshots, duration, counter-pickup requirements, and liability terms.
    /// </summary>
    Task<DocumentOperationResult> GenerateRentalAgreementAsync(int bookingGroupId);

    /// <summary>
    /// Generates the store-specific pro-forma rental invoice/quotation for a booking group.
    /// </summary>
    Task<DocumentOperationResult> GenerateStoreInvoiceAsync(int bookingGroupId);

    /// <summary>
    /// Generates the consolidated parent-level pro-forma rental invoice for the customer's entire multi-store booking.
    /// </summary>
    Task<DocumentOperationResult> GenerateParentBookingInvoiceAsync(int bookingId);

    /// <summary>
    /// Securely retrieves full document details with tenant & user isolation.
    /// A customer can only access their own documents.
    /// A business can only access their store's agreements and invoices (cannot access other stores' or parent documents).
    /// </summary>
    Task<RentalDocumentDto?> GetDocumentDetailsAsync(int documentId, string currentUserId, bool isAdmin = false);

    /// <summary>
    /// Gets all authorized documents for a customer.
    /// </summary>
    Task<(List<RentalDocumentSummaryDto> Items, int TotalCount)> GetCustomerDocumentsAsync(string customerUserId, int page = 1, int pageSize = 15);

    /// <summary>
    /// Gets all authorized documents for an approved business.
    /// </summary>
    Task<(List<RentalDocumentSummaryDto> Items, int TotalCount)> GetBusinessDocumentsAsync(int businessId, int page = 1, int pageSize = 15);

    /// <summary>
    /// Gets document content formatted for browser view or file download with strict access control.
    /// </summary>
    Task<DocumentDownloadResult> GetDocumentForDownloadAsync(int documentId, string currentUserId, bool isAdmin = false);

    /// <summary>
    /// Retries generation for a document that previously failed generation.
    /// </summary>
    Task<DocumentOperationResult> RetryDocumentGenerationAsync(int documentId, string currentUserId, bool isAdmin = false);
}
