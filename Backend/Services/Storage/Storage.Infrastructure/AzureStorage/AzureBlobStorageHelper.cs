namespace Storage.Infrastructure.AzureStorage {
    public static class AzureBlobStorageHelper {
        public static string GetDirectory(string? contentType) {
            if (string.IsNullOrEmpty(contentType)) {
                return "other";
            }

            return contentType.Split('/')[0] switch {
                "video" => "videos",
                "image" => "images",
                "audio" => "audios",
                _ => "other"
            };
        }

        public static string GetBlobName(string category, string? contentType, string fileName, string originalFileName) {
            var directory = GetDirectory(contentType);
            var fullFileName = fileName + Path.GetExtension(originalFileName);
            return $"{category}/{directory}/{fullFileName}";
        }
    }
}