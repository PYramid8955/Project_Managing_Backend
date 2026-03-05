namespace TaskManagement.Domain.Models.Projects;

public class AssignManagerRequest
{
    /// <summary>UserId of the Manager to assign this developer to. Null = remove from group.</summary>
    public Guid? ManagerUserId { get; set; }
}
