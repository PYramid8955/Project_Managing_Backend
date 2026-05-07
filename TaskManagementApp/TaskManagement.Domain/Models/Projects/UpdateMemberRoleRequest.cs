using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Projects;

public class UpdateMemberRoleRequest
{
    [Required]
    public ProjectRole NewRole { get; set; }
}
