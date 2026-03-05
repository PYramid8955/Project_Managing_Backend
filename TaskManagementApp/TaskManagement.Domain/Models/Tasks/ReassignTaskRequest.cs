namespace TaskManagement.Domain.Models.Tasks;

public class ReassignTaskRequest
{
    /// <summary>Null means unassign (task becomes Open).</summary>
    public Guid? AssignedToId { get; set; }
}
