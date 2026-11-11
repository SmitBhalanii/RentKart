namespace RentKart.Core.Interfaces;

public class ImageUploadResult
{
    public bool Success { get; set; }
    public string? ImageUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public static ImageUploadResult Ok(string url) => new() { Success = true, ImageUrl = url };
    public static ImageUploadResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

public interface IImageUploadService
{
    /// <summary>
    /// Validates file type and size, then securely saves equipment image to web storage.
    /// </summary>
    Task<ImageUploadResult> UploadEquipmentImageAsync(Stream stream, string fileName, string contentType, long fileLength);

    /// <summary>
    /// Safely deletes an existing uploaded image from web storage.
    /// </summary>
    bool DeleteImage(string imageUrl);
}

