namespace Users.Infrastructure.Configurations {
    public class CachingConfigurations
    {
        public int UserProfileCacheDurationInSeconds { get; set; } = 1800;
        public int UserChannelCacheDurationInSeconds { get; set; } = 1800;
    }
}