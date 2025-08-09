namespace Users.Infrastructure.Contracts {
    public interface ICacheKeyProvider
    {
        string GetUserProfileCacheKey(string userId);
        string GetUserProfileByHandleCacheKey(string handle);
        string GetUserChannelCacheKey(string userId);
        string GetUserChannelByHandleCacheKey(string handle);
    }
}