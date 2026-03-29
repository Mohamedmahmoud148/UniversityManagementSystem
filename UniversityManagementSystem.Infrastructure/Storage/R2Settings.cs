namespace UniversityManagementSystem.Infrastructure.Storage
{
    public class R2Settings
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string ServiceUrl { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// The public base URL for the R2 bucket (e.g. https://pub-xxxx.r2.dev or custom domain).
        /// If using the S3-compatible endpoint directly, set to ServiceUrl/BucketName.
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
