using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.DTOs.Common;

namespace JobBoard.Api.Services;

public interface IApplicationService
{
    Task<ApplicationResponseDto> ApplyForJobAsync(int jobId, ApplicationCreateDto dto, CancellationToken cancellationToken = default);
    Task<PagedResultDto<ApplicationResponseDto>> GetApplicationsForJobAsync(int jobId, PaginationQueryDto pagination, CancellationToken cancellationToken = default);
    Task<ApplicationResponseDto> GetApplicationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApplicationResponseDto> UpdateApplicationStatusAsync(int id, ApplicationStatusUpdateDto dto, CancellationToken cancellationToken = default);
    Task<ApplicationResponseDto> WithdrawApplicationAsync(int id, CancellationToken cancellationToken = default);
}
