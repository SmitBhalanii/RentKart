using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Services;

public class ImageUploadService : IImageUploadService
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(IWebHostEnvironment env, ILogger<ImageUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<ImageUploadResult> UploadEquipmentImageAsync(Stream stream, string fileName, string contentType, long fileLength)
    {
        if (stream == null || fileLength <= 0)
        {
            return ImageUploadResult.Fail("Please select an image file to upload.");
        }

        if (fileLength > MaxFileSizeInBytes)
        {
            return ImageUploadResult.Fail("Image size exceeds the maximum permitted limit of 5 MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return ImageUploadResult.Fail("Only JPG, JPEG, PNG, and WebP images are allowed.");
        }

        if (!string.IsNullOrEmpty(contentType) && !AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            return ImageUploadResult.Fail("Invalid image MIME content type.");
        }

        try
        {
            // Resolve wwwroot path
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsFolder = Path.Combine(webRoot, "uploads", "equipment");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"eq_{Guid.NewGuid():N}{extension}";
            var destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Equipment image uploaded successfully: {FileName} ({Bytes} bytes).", uniqueFileName, fileLength);
            var relativeUrl = $"/uploads/equipment/{uniqueFileName}";
            return ImageUploadResult.Ok(relativeUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded equipment image '{FileName}'.", fileName);
            return ImageUploadResult.Fail("An unexpected error occurred while saving the image. Please try again.");
        }
    }

    public bool DeleteImage(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/equipment/"))
        {
            return false;
        }

        try
        {
            var fileName = Path.GetFileName(imageUrl);
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, "uploads", "equipment", fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted equipment image from disk: {FilePath}", fullPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete equipment image file '{ImageUrl}'.", imageUrl);
        }

        return false;
    }
}

