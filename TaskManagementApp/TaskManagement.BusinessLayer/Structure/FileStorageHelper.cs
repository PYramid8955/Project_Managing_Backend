using Microsoft.AspNetCore.Http;

namespace TaskManagement.BusinessLayer.Structure;

public class FileStorageHelper
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".zip", ".docx", ".txt"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public async Task<string> SaveFileAsync(IFormFile file, Guid taskId, string wwwrootPath)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file provided.");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File size exceeds the 10MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed.");

        var uploadDir = Path.Combine(wwwrootPath, "uploads", taskId.ToString());
        Directory.CreateDirectory(uploadDir);

        // Generate a safe, unique filename
        var fileName = Path.GetRandomFileName().Replace(".", "") + ext;
        var fullPath = Path.Combine(uploadDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{taskId}/{fileName}";
    }
}
