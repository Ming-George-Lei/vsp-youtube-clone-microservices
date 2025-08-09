using Users.Domain.Models;

namespace Users.Infrastructure.Contracts {
    public interface ICachedUserChannelRepository {
        Task<UserChannel?> GetUserChannelByIdAsync(string userId, int? maxSectionItemsCount, CancellationToken cancellationToken = default);
        Task<UserChannel?> GetUserChannelByHandleAsync(string handle, int? maxSectionItemsCount, CancellationToken cancellationToken = default);
        Task CacheUserChannelsAsync(IEnumerable<UserChannel> userChannels, CancellationToken cancellationToken = default);
        Task RemoveUserChannelCachesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    }
}