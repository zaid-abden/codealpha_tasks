using System.ComponentModel.DataAnnotations;
using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.DTOs.Applications;

public class ApplicationStatusUpdateDto
{
    [Required(ErrorMessage = "Status is required.")]
    [EnumDataType(typeof(ApplicationStatus), ErrorMessage = "Invalid application status.")]
    public ApplicationStatus Status { get; set; }
}
