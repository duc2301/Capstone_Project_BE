using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    // Lưu file lên Viettel Cloud Object Storage (S3-compatible, nền Cloudian HyperStore).
    // Cấu hình ở "FileStorage:S3:*". StoragePath = object key dạng {projectId}/{folderId}/{guid}{ext}.
    public class ViettelS3FileStorageService : IFileStorageService
    {
        private readonly string _endpoint;
        private readonly string _region;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _bucket;

        private AmazonS3Client? _client;

        public ViettelS3FileStorageService(IConfiguration config)
        {
            _endpoint = config["FileStorage:S3:Endpoint"] ?? "https://vcos.cloudstorage.com.vn";
            _region = config["FileStorage:S3:Region"] ?? "us-east-1";
            _accessKey = config["FileStorage:S3:AccessKey"] ?? string.Empty;
            _secretKey = config["FileStorage:S3:SecretKey"] ?? string.Empty;
            _bucket = config["FileStorage:S3:Bucket"] ?? string.Empty;
        }

        // Client dùng lại (service là Singleton). Tạo lazy để báo lỗi cấu hình rõ ràng lúc dùng.
        private AmazonS3Client Client
        {
            get
            {
                if (_client != null) return _client;
                EnsureConfigured();
                var cfg = new AmazonS3Config
                {
                    ServiceURL = _endpoint,
                    ForcePathStyle = true,           // Cloudian: dùng path-style, không virtual-host
                    AuthenticationRegion = _region,
                };
                _client = new AmazonS3Client(_accessKey, _secretKey, cfg);
                return _client;
            }
        }

        public async Task<StoredFile> SaveAsync(
            Stream content, Guid projectId, Guid folderId, string extension, CancellationToken ct = default)
        {
            var ext = NormalizeExt(extension);
            var key = string.Join('/', projectId.ToString(), folderId.ToString(), $"{Guid.NewGuid():N}{ext}");

            // Đệm vào bộ nhớ để tính SHA-256 + độ dài (S3 cần ContentLength).
            // TODO: file lớn (IFC/CAD) nên chuyển sang multipart/TransferUtility để khỏi nạp hết vào RAM.
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var size = buffer.Length;
            buffer.Position = 0;
            string checksum;
            using (var sha = SHA256.Create())
                checksum = Convert.ToHexString(sha.ComputeHash(buffer)).ToLowerInvariant();
            buffer.Position = 0;

            var req = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = buffer,
                ContentType = GetContentType(ext),
                DisablePayloadSigning = true,        // UNSIGNED-PAYLOAD: tránh lỗi chữ ký với Cloudian
                AutoCloseStream = false,
            };

            var res = await Client.PutObjectAsync(req, ct);
            if ((int)res.HttpStatusCode >= 300)
                throw new ApiExceptionResponse($"Upload to Object Storage failed (HTTP {(int)res.HttpStatusCode}).", 502);

            return new StoredFile(key, size, checksum);
        }

        public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
        {
            try
            {
                var res = await Client.GetObjectAsync(_bucket, storagePath, ct);
                return res.ResponseStream;   // controller đọc & dispose stream này
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ApiExceptionResponse("Stored object not found.", 404);
            }
        }

        public string GetContentType(string fileNameOrExt)
        {
            var ext = NormalizeExt(Path.GetExtension(fileNameOrExt) is { Length: > 0 } e ? e : fileNameOrExt);
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".csv" => "text/csv",
                ".txt" => "text/plain",
                ".ifc" => "application/x-step",
                ".dwg" => "image/vnd.dwg",
                ".dxf" => "image/vnd.dxf",
                _ => "application/octet-stream",
            };
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_accessKey) || string.IsNullOrWhiteSpace(_secretKey) || string.IsNullOrWhiteSpace(_bucket))
                throw new ApiExceptionResponse(
                    "Object Storage chưa được cấu hình: thiếu FileStorage:S3:AccessKey/SecretKey/Bucket.", 500);
        }

        private static string NormalizeExt(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            ext = ext.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }
    }
}
