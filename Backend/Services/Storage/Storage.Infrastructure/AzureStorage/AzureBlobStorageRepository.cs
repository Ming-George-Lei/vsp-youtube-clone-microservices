using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Exceptions;
using Storage.Domain.Contracts;
using Storage.Domain.Models;
using Storage.Infrastructure.Scanners;

namespace Storage.Infrastructure.AzureStorage {
    public class AzureBlobStorageRepository : IStorageRepository {

        private readonly IServiceProvider _serviceProvider;
        private readonly AzureBlobStorageConfiguration _config;
        private readonly ILogger<AzureBlobStorageRepository> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;

        public AzureBlobStorageRepository(IServiceProvider serviceProvider, IOptions<AzureBlobStorageConfiguration> config, ILogger<AzureBlobStorageRepository> logger) {
            _serviceProvider = serviceProvider;
            _config = config.Value;
            _logger = logger;

            _blobServiceClient = new BlobServiceClient(_config.ConnectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_config.ContainerName);

            InitializeContainerAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeContainerAsync() {
            if (_config.CreateContainerIfNotExists) {
                try {
                    await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                    _logger.LogInformation("Azure Blob container '{ContainerName}' initialized", _config.ContainerName);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to initialize Azure Blob container '{ContainerName}'", _config.ContainerName);
                    throw;
                }
            }
        }

        public async Task<StoredFile> StoreFileAsync(string? userId, Guid fileId, Guid trackingId, Guid groupId, string category, string? contentType, string fileName, string originalFileName, Stream stream, long? maxFileSize, int? bufferSize, Func<FileStream, Task>? fileCheck = null, CancellationToken cancellationToken = default) {
            var blobName = AzureBlobStorageHelper.GetBlobName(category, contentType, fileName, originalFileName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("File {OriginalFileName} ({ContentType}) is being uploaded to Azure Blob '{BlobName}'", originalFileName, contentType, blobName);

            try {
                // Validate file size before upload
                if (maxFileSize.HasValue && stream.Length > maxFileSize * 1024) {
                    throw new AppException("File too large", null, StatusCodes.Status400BadRequest);
                }

                // If file validation is required, we need to download to temp file first
                if (fileCheck != null) {
                    await ValidateFileWithTempFile(stream, fileCheck, trackingId);
                }

                // Anti-virus scan if available
                await AntiVirusScanAsync(trackingId, stream);

                // Upload to Azure Blob Storage
                var uploadOptions = new BlobUploadOptions {
                    HttpHeaders = new BlobHttpHeaders {
                        ContentType = contentType
                    },
                    Metadata = new Dictionary<string, string> {
                        ["userId"] = userId ?? string.Empty,
                        ["fileId"] = fileId.ToString(),
                        ["trackingId"] = trackingId.ToString(),
                        ["groupId"] = groupId.ToString(),
                        ["category"] = category,
                        ["originalFileName"] = originalFileName
                    }
                };

                await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

                var uri = GetPublicUrl(blobName);
                var properties = new List<FileProperty> {
                    new("StorageType", "AzureBlobStorage"),
                    new("BlobName", blobName),
                    new("ContainerName", _config.ContainerName)
                };

                _logger.LogInformation("File {OriginalFileName} ({ContentType}) uploaded successfully to Azure Blob '{BlobName}'", originalFileName, contentType, blobName);

                return StoredFile.Create(fileId, trackingId, groupId, userId, category, contentType, fileName, originalFileName, stream.Length, uri, properties, DateTimeOffset.UtcNow);

            } catch (Exception ex) {
                _logger.LogError(ex, "An error occurred when uploading file {OriginalFileName} ({ContentType}) to Azure Blob '{BlobName}'", originalFileName, contentType, blobName);
                throw;
            }
        }

        private async Task ValidateFileWithTempFile(Stream stream, Func<FileStream, Task> fileValidator, Guid trackingId) {
            var tempPath = Path.GetTempFileName();
            try {
                using (var tempFileStream = File.Create(tempPath)) {
                    stream.Position = 0;
                    await stream.CopyToAsync(tempFileStream);
                }

                using var fileStream = File.OpenRead(tempPath);
                await fileValidator.Invoke(fileStream);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to validate the file ({TrackingId})", trackingId);
                throw;
            } finally {
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }
            }
        }

        private async Task AntiVirusScanAsync(Guid trackingId, Stream stream) {
            var antiVirusScanner = _serviceProvider.GetService<IAntiVirusScanner>();

            if (antiVirusScanner != null) {
                var tempPath = Path.GetTempFileName();
                try {
                    // Read stream into memory first to avoid affecting original stream
                    stream.Position = 0;
                    var streamBytes = new byte[stream.Length];
                    await stream.ReadExactlyAsync(streamBytes);
                    
                    // Reset stream position for subsequent operations
                    stream.Position = 0;
                    
                    // Write bytes to temp file for scanning
                    await File.WriteAllBytesAsync(tempPath, streamBytes);

                    bool result = await antiVirusScanner.ScanAndClean(tempPath);

                    if (!result) {
                        _logger.LogError("The file ({TrackingId}) cannot pass the anti-virus scanner", trackingId);
                        throw new Exception("Virus detected");
                    }
                } finally {
                    if (File.Exists(tempPath)) {
                        File.Delete(tempPath);
                    }
                }
            }
        }

        public async Task DeleteFileAsync(StoredFile file) {
            var blobName = GetBlobNameFromStoredFile(file);
            await DeleteFileAsync(blobName);
        }

        public async Task<bool> HasFileAsync(string category, string? contentType, string fileName, string originalFileName) {
            var blobName = AzureBlobStorageHelper.GetBlobName(category, contentType, fileName, originalFileName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            try {
                var response = await blobClient.ExistsAsync();
                return response.Value;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error checking if blob exists: {BlobName}", blobName);
                return false;
            }
        }

        public async Task DeleteFileAsync(string category, string? contentType, string fileName, string originalFileName) {
            var blobName = AzureBlobStorageHelper.GetBlobName(category, contentType, fileName, originalFileName);
            await DeleteFileAsync(blobName);
        }

        private async Task DeleteFileAsync(string blobName) {
            var blobClient = _containerClient.GetBlobClient(blobName);

            try {
                await blobClient.DeleteIfExistsAsync();
                _logger.LogInformation("Blob {BlobName} deleted successfully", blobName);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error deleting blob: {BlobName}", blobName);
                throw;
            }
        }

        private static string GetBlobNameFromStoredFile(StoredFile file) {
            var blobNameProperty = file.Properties.FirstOrDefault(p => p.Name == "BlobName");
            if (blobNameProperty != null) {
                return blobNameProperty.Value;
            }

            return AzureBlobStorageHelper.GetBlobName(file.Category, file.ContentType, file.FileName, file.OriginalFileName);
        }

        private string GetPublicUrl(string blobName) {
            if (!string.IsNullOrEmpty(_config.BaseUrl)) {
                return $"{_config.BaseUrl.TrimEnd('/')}/{_config.ContainerName}/{blobName}";
            }

            return _containerClient.GetBlobClient(blobName).Uri.ToString();
        }
    }
}