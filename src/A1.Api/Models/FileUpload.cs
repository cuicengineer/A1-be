using System;

namespace A1.Api.Models
{
    public class FileUpload : BaseEntity
    {
        public int FormId { get; set; }
        public DateTime? UploadedDate { get; set; }
        public string? Path { get; set; }
        public string? FormName { get; set; }
    }
}