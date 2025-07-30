namespace Storage.Infrastructure.AzureStorage
{
    public class AzureBlobStorageConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = "videos";
        public string BaseUrl { get; set; } = string.Empty;
        public bool CreateContainerIfNotExists { get; set; } = true;
        public int UploadTimeoutMinutes { get; set; } = 60;
    }
}