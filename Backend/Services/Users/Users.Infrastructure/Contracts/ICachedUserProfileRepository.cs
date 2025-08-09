using Users.Domain.Models;

namespace Users.Infrastructure.Contracts {
    public interface ICachedUserProfileRepository {
        Task<UserProfile?> GetUserProfileByIdAsync(string userId, CancellationToken cancellationToken = default);
        Task<UserProfile?> GetUserProfileByHandleAsync(string handle, CancellationToken cancellationToken = default);
        Task CacheUserProfilesAsync(IEnumerable<UserProfile> userProfiles, CancellationToken cancellationToken = default);
        Task RemoveUserProfileCachesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
        Task RemoveUserProfileCacheByHandleAsync(string handle, CancellationToken cancellationToken = default);
    }
}