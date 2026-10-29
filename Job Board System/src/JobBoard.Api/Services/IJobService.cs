using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;

namespace JobBoard.Api.Services;

public interface IJobService
{
    Task<PagedResultDto<JobResponseDto>> GetPublishedJobsAsync(JobFilterDto filter, CancellationToken cancellationToken = default);
    Task<JobResponseDto> GetJobByIdAsync(int id, bool publicOnly = true, CancellationToken cancellationToken = default);
    Task<JobResponseDto> CreateJobAsync(JobCreateDto dto, CancellationToken cancellationToken = default);
    Task<JobResponseDto> UpdateJobAsync(int id, JobUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteJobAsync(int id, CancellationToken cancellationToken = default);
    Task<JobResponseDto> PublishJobAsync(int id, CancellationToken cancellationToken = default);
    Task<JobResponseDto> CloseJobAsync(int id, CancellationToken cancellationToken = default);
}
