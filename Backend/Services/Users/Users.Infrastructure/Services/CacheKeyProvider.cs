using Users.Infrastructure.Contracts;

namespace Users.Infrastructure.Services {
    public class CacheKeyProvider : ICacheKeyProvider
    {
        public string GetUserProfileCacheKey(string userId)
        {
            return $"user-profile-{userId}";
        }

        public string GetUserProfileByHandleCacheKey(string handle)
        {
            return $"user-profile-handle-{handle}";
        }

        public string GetUserChannelCacheKey(string userId)
        {
            return $"user-channel-{userId}";
        }

        public string GetUserChannelByHandleCacheKey(string handle)
        {
            return $"user-channel-handle-{handle}";
        }
    }
}