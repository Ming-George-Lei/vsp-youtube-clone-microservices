
using Application.Handlers;
using SharedKernel.Exceptions;
using Users.Domain.Contracts;
using Users.Domain.Models;
using Users.Infrastructure.Contracts;

namespace Users.API.Application.Queries.Handlers {
    public class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfile> {

        private readonly ICachedUserProfileRepository _cachedRepository;

        public GetUserProfileQueryHandler (ICachedUserProfileRepository cachedRepository) {
            _cachedRepository = cachedRepository;
        }

        public async Task<UserProfile> Handle (GetUserProfileQuery request, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(request.UserId) && string.IsNullOrEmpty(request.Handle)) {
                throw new AppException(null, null, StatusCodes.Status400BadRequest);
            }

            var userProfile = await
                (!string.IsNullOrEmpty(request.UserId) ?
                _cachedRepository.GetUserProfileByIdAsync(request.UserId, cancellationToken) :
                _cachedRepository.GetUserProfileByHandleAsync(request.Handle!, cancellationToken));

            if (request.ThrowIfNotFound && userProfile == null) {
                throw new AppException("User profile not found", null, StatusCodes.Status404NotFound);
            }

            return userProfile!;
        }

    }
}
