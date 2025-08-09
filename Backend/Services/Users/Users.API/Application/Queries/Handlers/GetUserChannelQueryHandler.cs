
using Application.Handlers;
using SharedKernel.Exceptions;
using Users.Domain.Models;
using Users.Infrastructure.Contracts;

namespace Users.API.Application.Queries.Handlers {
    public class GetUserChannelQueryHandler : IQueryHandler<GetUserChannelQuery, UserChannel> {

        private readonly ICachedUserChannelRepository _cachedRepository;

        public GetUserChannelQueryHandler (ICachedUserChannelRepository cachedRepository) {
            _cachedRepository = cachedRepository;
        }

        public async Task<UserChannel> Handle (GetUserChannelQuery request, CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(request.UserId) && string.IsNullOrEmpty(request.Handle)) {
                throw new AppException(null, null, StatusCodes.Status400BadRequest);
            }

            var userChannel = await
                (!string.IsNullOrEmpty(request.UserId) ?
                _cachedRepository.GetUserChannelByIdAsync(
                    request.UserId,
                    request.MaxSectionItemsCount,
                    cancellationToken) :
                _cachedRepository.GetUserChannelByHandleAsync(
                    request.Handle!,
                    request.MaxSectionItemsCount,
                    cancellationToken));

            if (userChannel == null) {
                throw new AppException("User channel not found", null, StatusCodes.Status404NotFound);
            }

            return userChannel;
        }

    }
}
