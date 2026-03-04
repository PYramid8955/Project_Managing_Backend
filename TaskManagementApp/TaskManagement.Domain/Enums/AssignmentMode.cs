namespace TaskManagement.Domain.Enums;

public enum AssignmentMode
{
    Open = 0,    // Any developer in the project can accept
    Invited = 1, // Only specified developers can accept
    Direct = 2   // Auto-assigned, task starts as InProgress immediately
}
