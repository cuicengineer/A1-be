using A1.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace A1.Api.Controllers
{
    /// <summary>
    /// Upload Controller for handling file uploads and downloads
    /// 
    /// POST /api/Upload - Upload files
    /// Content-Type: multipart/form-data
    /// Parameters:
    ///   - id: int (form data)
    ///   - tableName: string (form data)
    ///   - files: IFormFile[] (form data - multiple files)
    /// 
    /// Example using fetch:
    /// const formData = new FormData();
    /// formData.append('id', '123');
    /// formData.append('tableName', 'RevenueRates');
    /// formData.append('files', file1);
    /// formData.append('files', file2);
    /// 
    /// fetch('https://your-api-url/api/Upload', {
    ///   method: 'POST',
    ///   body: formData
    /// });
    /// 
    /// Example using axios:
    /// const formData = new FormData();
    /// formData.append('id', '123');
    /// formData.append('tableName', 'RevenueRates');
    /// files.forEach(file => formData.append('files', file));
    /// 
    /// axios.post('https://your-api-url/api/Upload', formData, {
    ///   headers: { 'Content-Type': 'multipart/form-data' }
    /// });
    /// 
    /// GET /api/Upload?id=123&formName=RevenueRates - Get list of uploaded files
    /// Returns: Array of file metadata (id, fileName, path, size, uploadedDate, downloadUrl)
    /// 
    /// Example using fetch:
    /// fetch('https://your-api-url/api/Upload?id=123&formName=RevenueRates')
    ///   .then(res => res.json())
    ///   .then(data => console.log(data));
    /// 
    /// Example using axios:
    /// axios.get('https://your-api-url/api/Upload', {
    ///   params: { id: 123, formName: 'RevenueRates' }
    /// });
    /// 
    /// GET /api/Upload/Download?fileId=456 - Download/view a specific file
    /// Returns: File stream for download or viewing
    /// 
    /// Example using fetch:
    /// fetch('https://your-api-url/api/Upload/Download?fileId=456')
    ///   .then(res => res.blob())
    ///   .then(blob => {
    ///     const url = window.URL.createObjectURL(blob);
    ///     const a = document.createElement('a');
    ///     a.href = url;
    ///     a.download = 'filename.ext';
    ///     a.click();
    ///   });
    /// 
    /// DELETE /api/Upload/{fileId} - Delete a file (soft delete - sets IsDeleted flag)
    /// Returns: Success message
    /// 
    /// Example using fetch:
    /// fetch('https://your-api-url/api/Upload/123', {
    ///   method: 'DELETE'
    /// });
    /// 
    /// Example using axios:
    /// axios.delete('https://your-api-url/api/Upload/123');
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private const int BufferSize = 8192; // 8KB buffer for memory efficiency

        public UploadController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles([FromForm] int id, [FromForm] string tableName, [FromForm] IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest("No files provided.");
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                return BadRequest("Table name is required.");
            }

            if (id <= 0)
            {
                return BadRequest("Valid ID is required.");
            }

            // Sanitize table name to prevent directory traversal
            var sanitizedTableName = SanitizeFileName(tableName);
            var uploadsBasePath = Path.Combine(_environment.ContentRootPath ?? _environment.WebRootPath ?? Directory.GetCurrentDirectory(), "Uploads");
            var tableFolderPath = Path.Combine(uploadsBasePath, sanitizedTableName);
            var idFolderPath = Path.Combine(tableFolderPath, id.ToString());

            try
            {
                // Create directory structure if it doesn't exist
                Directory.CreateDirectory(idFolderPath);

                var uploadedFiles = new List<FileUpload>();

                // Process files with memory-efficient streaming
                foreach (var file in files)
                {
                    if (file.Length == 0)
                        continue;

                    // Use original filename without GUID prefix
                    var fileName = SanitizeFileName(file.FileName);
                    var filePath = Path.Combine(idFolderPath, fileName);
                    var relativePath = Path.Combine("Uploads", sanitizedTableName, id.ToString(), fileName).Replace('\\', '/');

                    // Save file using streaming for memory efficiency
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    // Create FileUpload record
                    var fileUpload = new FileUpload
                    {
                        FormId = id,
                        FormName = tableName,
                        Path = relativePath,
                        UploadedDate = DateTime.UtcNow,
                        ActionDate = DateTime.UtcNow,
                        Action = "CREATE",
                        IsDeleted = false
                    };

                    uploadedFiles.Add(fileUpload);
                }

                // Save all records to database in a single transaction
                if (uploadedFiles.Count > 0)
                {
                    await _context.FileUploads.AddRangeAsync(uploadedFiles);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = $"{uploadedFiles.Count} file(s) uploaded successfully.",
                    uploadedFiles = uploadedFiles.Select(f => new
                    {
                        id = f.Id,
                        formId = f.FormId,
                        formName = f.FormName,
                        path = f.Path,
                        uploadedDate = f.UploadedDate
                    })
                });
            }
            catch (Exception ex)
            {
                // Clean up partially uploaded files on error
                try
                {
                    if (Directory.Exists(idFolderPath))
                    {
                        Directory.Delete(idFolderPath, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                // Get inner exception details for better error reporting
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }

                // If it's a DbUpdateException, get more details
                if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx && dbEx.InnerException != null)
                {
                    errorMessage += $" Database Error: {dbEx.InnerException.Message}";
                }

                return StatusCode(500, new { message = "An error occurred while uploading files.", error = errorMessage });
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            // Remove path separators and other dangerous characters
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Limit length to prevent issues
            if (sanitized.Length > 255)
            {
                var extension = Path.GetExtension(sanitized);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
                sanitized = nameWithoutExt.Substring(0, Math.Min(255 - extension.Length, nameWithoutExt.Length)) + extension;
            }

            return sanitized;
        }

        /// <summary>
        /// GET method to retrieve all uploaded files for a specific form
        /// Memory efficient - only reads file metadata, not file contents
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUploadedFiles([FromQuery] int id, [FromQuery] string formName)
        {
            if (id <= 0)
            {
                return BadRequest("Valid ID is required.");
            }

            if (string.IsNullOrWhiteSpace(formName))
            {
                return BadRequest("Form name is required.");
            }

            try
            {
                // Get files from database
                var fileUploads = await _context.FileUploads
                    .Where(f => f.FormId == id && f.FormName == formName && (f.IsDeleted == null || f.IsDeleted == false))
                    .ToListAsync();

                if (fileUploads.Count == 0)
                {
                    return Ok(new { files = new List<object>() });
                }

                // Sanitize form name for path construction
                var sanitizedFormName = SanitizeFileName(formName);
                var uploadsBasePath = Path.Combine(_environment.ContentRootPath ?? _environment.WebRootPath ?? Directory.GetCurrentDirectory(), "Uploads");
                var idFolderPath = Path.Combine(uploadsBasePath, sanitizedFormName, id.ToString());

                var fileList = new List<object>();

                // Process files without loading contents into memory
                foreach (var fileUpload in fileUploads)
                {
                    try
                    {
                        // Extract filename from stored path
                        var fileName = Path.GetFileName(fileUpload.Path ?? string.Empty);
                        
                        // Construct full path from known components (more reliable than parsing stored path)
                        // This matches how files are saved: Uploads/TableName/Id/filename
                        var fullPath = Path.Combine(idFolderPath, fileName);

                        // Check if file exists and get metadata (memory efficient - only reads file info)
                        if (System.IO.File.Exists(fullPath))
                        {
                            var fileInfo = new FileInfo(fullPath);

                            // Get MIME type based on extension
                            var mimeType = GetMimeType(Path.GetExtension(fileName));

                            // Build complete downloadable URL
                            var baseUrl = $"{Request.Scheme}://{Request.Host}";
                            var downloadUrl = $"{baseUrl}/api/Upload/Download?fileId={fileUpload.Id}";

                            fileList.Add(new
                            {
                                id = fileUpload.Id,
                                fileName = fileName,
                                path = downloadUrl,
                                size = fileInfo.Length,
                                sizeFormatted = FormatFileSize(fileInfo.Length),
                                uploadedDate = fileUpload.UploadedDate,
                                downloadUrl = downloadUrl,
                                mimeType = mimeType
                            });
                        }
                        else
                        {
                            // File doesn't exist on disk but record exists in DB
                            fileList.Add(new
                            {
                                id = fileUpload.Id,
                                fileName = Path.GetFileName(fileUpload.Path ?? string.Empty),
                                storedFileName = Path.GetFileName(fileUpload.Path ?? string.Empty),
                                path = fileUpload.Path,
                                size = 0L,
                                sizeFormatted = "0 B",
                                uploadedDate = fileUpload.UploadedDate,
                                downloadUrl = (string?)null,
                                mimeType = (string?)null,
                                error = "File not found on disk"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other files
                        fileList.Add(new
                        {
                            id = fileUpload.Id,
                            fileName = Path.GetFileName(fileUpload.Path ?? string.Empty),
                            error = $"Error reading file: {ex.Message}"
                        });
                    }
                }

                return Ok(new { files = fileList });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving files.", error = ex.Message });
            }
        }

        /// <summary>
        /// GET method to download/view a specific file by fileId
        /// Memory efficient - streams file directly to response without loading into memory
        /// </summary>
        [HttpGet("Download")]
        public async Task<IActionResult> DownloadFile([FromQuery] int fileId)
        {
            if (fileId <= 0)
            {
                return BadRequest("Valid file ID is required.");
            }

            try
            {
                var fileUpload = await _context.FileUploads
                    .FirstOrDefaultAsync(f => f.Id == fileId && (f.IsDeleted == null || f.IsDeleted == false));

                if (fileUpload == null)
                {
                    return NotFound("File not found.");
                }

                var uploadsBasePath = Path.Combine(_environment.ContentRootPath ?? _environment.WebRootPath ?? Directory.GetCurrentDirectory(), "Uploads");
                
                // Construct full path correctly - fileUpload.Path format: "Uploads/TableName/Id/filename"
                // Use FormId and FormName from database record (most reliable)
                var sanitizedFormName = SanitizeFileName(fileUpload.FormName ?? string.Empty);
                var fileName = Path.GetFileName(fileUpload.Path ?? string.Empty);
                var fullPath = Path.Combine(uploadsBasePath, sanitizedFormName, fileUpload.FormId.ToString(), fileName);

                if (!System.IO.File.Exists(fullPath))
                {
                    return NotFound("File not found on disk.");
                }

                var mimeType = GetMimeType(Path.GetExtension(fileName));

                // Stream file directly to response (memory efficient)
                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
                
                return File(fileStream, mimeType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while downloading the file.", error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE method to soft delete a file by setting IsDeleted flag to true
        /// </summary>
        [HttpDelete("{fileId}")]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            if (fileId <= 0)
            {
                return BadRequest("Valid file ID is required.");
            }

            try
            {
                var fileUpload = await _context.FileUploads
                    .FirstOrDefaultAsync(f => f.Id == fileId);

                if (fileUpload == null)
                {
                    return NotFound("File not found.");
                }

                // Soft delete - set IsDeleted flag
                fileUpload.IsDeleted = true;
                fileUpload.Action = "DELETE";
                fileUpload.ActionDate = DateTime.UtcNow;

                _context.FileUploads.Update(fileUpload);
                await _context.SaveChangesAsync();

                return Ok(new { message = "File deleted successfully.", id = fileUpload.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the file.", error = ex.Message });
            }
        }

        private static string GetMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

