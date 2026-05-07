namespace TaskManagement.Domain;

public static class ProjectEventType
{
    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectUpdated = "ProjectUpdated";
    public const string MemberJoined = "MemberJoined";
    public const string MemberLeft = "MemberLeft";
    public const string MemberRemoved = "MemberRemoved";
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleChanged = "RoleChanged";
    public const string InviteLinkCreated = "InviteLinkCreated";
    public const string InviteLinkDeleted = "InviteLinkDeleted";
}
