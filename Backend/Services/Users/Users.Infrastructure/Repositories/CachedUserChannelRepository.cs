using Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Users.Domain.Contracts;
using Users.Domain.Models;
using Users.Infrastructure.Configurations;
using Users.Infrastructure.Contracts;

namespace Users.Infrastructure.Repositories {
    public class CachedUserChannelRepository : ICachedUserChannelRepository {

        private readonly IUserChannelRepository _userChannelRepository;
        private readonly ICacheContext _cacheContext;
        private readonly ICacheKeyProvider _cacheKeyProvider;
        private readonly CachingConfigurations _cachingConfigurations;
        private readonly ILogger<CachedUserChannelRepository> _logger;

        public CachedUserChannelRepository(
            IUserChannelRepository userChannelRepository,
            ICacheContext cacheContext,
            ICacheKeyProvider cacheKeyProvider,
            IOptions<CachingConfigurations> cachingConfigurations,
            ILogger<CachedUserChannelRepository> logger) {
            _userChannelRepository = userChannelRepository;
            _cacheContext = cacheContext;
            _cacheKeyProvider = cacheKeyProvider;
            _cachingConfigurations = cachingConfigurations.Value;
            _logger = logger;
        }

        public async Task<UserChannel?> GetUserChannelByIdAsync(string userId, int? maxSectionItemsCount, CancellationToken cancellationToken = default) {
            if (!maxSectionItemsCount.HasValue) {
                try {
                    var cachedUserChannel = await _cacheContext.GetCacheAsync<UserChannel>(
                        _cacheKeyProvider.GetUserChannelCacheKey(userId),
                        cancellationToken);

                    if (cachedUserChannel != null)
                    {
                        return cachedUserChannel;
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Failed to retrieve user channel cache ({UserId})", userId);
                }
                var userChannel = await _userChannelRepository.GetUserChannelByIdAsync(userId, maxSectionItemsCount, cancellationToken);
                if (userChannel != null) {
                    await CacheUserChannelsAsync(new[] { userChannel }, cancellationToken);
                }

                return userChannel;
            }

            return await _userChannelRepository.GetUserChannelByIdAsync(userId, maxSectionItemsCount, cancellationToken);
        }

        public async Task<UserChannel?> GetUserChannelByHandleAsync(string handle, int? maxSectionItemsCount, CancellationToken cancellationToken = default) {
            if (!maxSectionItemsCount.HasValue) {
                try {
                    var cachedUserChannel = await _cacheContext.GetCacheAsync<UserChannel>(
                        _cacheKeyProvider.GetUserChannelByHandleCacheKey(handle),
                        cancellationToken);

                    if (cachedUserChannel != null) {
                        return cachedUserChannel;
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to retrieve user channel cache by handle ({Handle})", handle);
                }

                var userChannel = await _userChannelRepository.GetUserChannelByHandleAsync(handle, null, cancellationToken);
                if (userChannel != null) {
                    await CacheUserChannelsAsync(new[] { userChannel }, cancellationToken);
                }
                return userChannel;
            }
            // For queries with maxSectionItemsCount, bypass cache and query database directly
            return await _userChannelRepository.GetUserChannelByHandleAsync(handle, maxSectionItemsCount, cancellationToken);
        }

        public async Task CacheUserChannelsAsync(IEnumerable<UserChannel> userChannels, CancellationToken cancellationToken = default) {
            try {
                var cacheDuration = TimeSpan.FromSeconds(_cachingConfigurations.UserChannelCacheDurationInSeconds);

                foreach (var userChannel in userChannels) {
                    // Cache by ID
                    _cacheContext.AddCache(
                        _cacheKeyProvider.GetUserChannelCacheKey(userChannel.Id),
                        userChannel,
                        cacheDuration);

                    // Also cache by handle if available
                    if (!string.IsNullOrEmpty(userChannel.Handle)) {
                        _cacheContext.AddCache(
                            _cacheKeyProvider.GetUserChannelByHandleCacheKey(userChannel.Handle),
                            userChannel,
                            cacheDuration);
                    }
                }

                await _cacheContext.CommitAsync(cancellationToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to add user channel caches");
            }
        }

        public async Task RemoveUserChannelCachesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default) {
            try {
                foreach (var userId in userIds) {
                    _cacheContext.RemoveCache(_cacheKeyProvider.GetUserChannelCacheKey(userId));
                }

                await _cacheContext.CommitAsync(cancellationToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to remove user channel caches");
            }
        }
    }
}