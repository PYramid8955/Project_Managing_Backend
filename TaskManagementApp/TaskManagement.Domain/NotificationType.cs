namespace TaskManagement.Domain;

public static class NotificationType
{
    public const string RoleAssigned        = "RoleAssigned";
    public const string RoleChanged         = "RoleChanged";
    public const string RemovedFromProject  = "RemovedFromProject";
    public const string TaskAssigned        = "TaskAssigned";
    public const string TaskApproved        = "TaskApproved";
    public const string TaskRejected        = "TaskRejected";
    public const string TaskSubmitted       = "TaskSubmitted";
    public const string TaskDelegated       = "TaskDelegated";
    public const string CommentAdded        = "CommentAdded";
    public const string MemberLeft          = "MemberLeft";
}
