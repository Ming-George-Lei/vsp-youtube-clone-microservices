using Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Users.Domain.Contracts;
using Users.Domain.Models;
using Users.Infrastructure.Configurations;
using Users.Infrastructure.Contracts;

namespace Users.Infrastructure.Repositories {
    public class CachedUserProfileRepository : ICachedUserProfileRepository {

        private readonly IUserProfileRepository _userProfileRepository;
        private readonly ICacheContext _cacheContext;
        private readonly ICacheKeyProvider _cacheKeyProvider;
        private readonly CachingConfigurations _cachingConfigurations;
        private readonly ILogger<CachedUserProfileRepository> _logger;

        public CachedUserProfileRepository(
            IUserProfileRepository userProfileRepository,
            ICacheContext cacheContext,
            ICacheKeyProvider cacheKeyProvider,
            IOptions<CachingConfigurations> cachingConfigurations,
            ILogger<CachedUserProfileRepository> logger) {
            _userProfileRepository = userProfileRepository;
            _cacheContext = cacheContext;
            _cacheKeyProvider = cacheKeyProvider;
            _cachingConfigurations = cachingConfigurations.Value;
            _logger = logger;
        }

        public async Task<UserProfile?> GetUserProfileByIdAsync(string userId, CancellationToken cancellationToken = default) {
            try {
                var cachedUserProfile = await _cacheContext.GetCacheAsync<UserProfile>(
                    _cacheKeyProvider.GetUserProfileCacheKey(userId),
                    cancellationToken);

                if (cachedUserProfile != null) {
                    return cachedUserProfile;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to retrieve user profile cache ({UserId})", userId);
            }

            var userProfile = await _userProfileRepository.GetUserProfileByIdAsync(userId, false, cancellationToken);

            if (userProfile != null) {
                await CacheUserProfilesAsync(new[] { userProfile }, cancellationToken);
            }

            return userProfile;
        }

        public async Task<UserProfile?> GetUserProfileByHandleAsync(string handle, CancellationToken cancellationToken = default) {
            try {
                var cachedUserProfile = await _cacheContext.GetCacheAsync<UserProfile>(
                    _cacheKeyProvider.GetUserProfileByHandleCacheKey(handle),
                    cancellationToken);

                if (cachedUserProfile != null) {
                    return cachedUserProfile;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to retrieve user profile cache by handle ({Handle})", handle);
            }

            var userProfile = await _userProfileRepository.GetUserProfileByHandleAsync(handle, false, cancellationToken);

            if (userProfile != null) {
                await CacheUserProfilesAsync(new[] { userProfile }, cancellationToken);
            }

            return userProfile;
        }

        public async Task CacheUserProfilesAsync(IEnumerable<UserProfile> userProfiles, CancellationToken cancellationToken = default) {
            try {
                var cacheDuration = TimeSpan.FromSeconds(_cachingConfigurations.UserProfileCacheDurationInSeconds);

                foreach (var userProfile in userProfiles) {
                    _cacheContext.AddCache(
                        _cacheKeyProvider.GetUserProfileCacheKey(userProfile.Id),
                        userProfile,
                        cacheDuration);

                    if (!string.IsNullOrEmpty(userProfile.Handle)) {
                        _cacheContext.AddCache(
                            _cacheKeyProvider.GetUserProfileByHandleCacheKey(userProfile.Handle),
                            userProfile,
                            cacheDuration);
                    }
                }

                await _cacheContext.CommitAsync(cancellationToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to add user profile caches");
            }
        }

        public async Task RemoveUserProfileCachesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default) {
            try {
                foreach (var userId in userIds) {
                    _cacheContext.RemoveCache(_cacheKeyProvider.GetUserProfileCacheKey(userId));
                }

                await _cacheContext.CommitAsync(cancellationToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to remove user profile caches");
            }
        }

        public async Task RemoveUserProfileCacheByHandleAsync(string handle, CancellationToken cancellationToken = default) {
            try {
                _cacheContext.RemoveCache(_cacheKeyProvider.GetUserProfileByHandleCacheKey(handle));
                await _cacheContext.CommitAsync(cancellationToken);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to remove user profile cache by handle ({Handle})", handle);
            }
        }
    }
}