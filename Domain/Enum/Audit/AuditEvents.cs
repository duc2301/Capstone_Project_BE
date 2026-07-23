using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enum.Audit
{
    public static class AuditEvents
    {
        // System
        public const string ProjectCreate = "Project.Create";
        public const string ProjectUpdate = "Project.Update";
        public const string ProjectDelete = "Project.Delete";
        public const string AccountCreate = "Account.Create";
        // Project
        public const string ManagerAssign = "Project.AssignManager";
        public const string ParticipantAdd = "Project.AddParticipant";
        public const string InvitationInvite = "Invitation.Invite";
        public const string ApprovalSubmit = "Approval.Submit";
        public const string ApprovalApprove = "Approval.Approve";
        public const string ApprovalReject = "Approval.Reject";
        public const string FileSign = "File.Sign";
        public const string ZoneTransfer = "File.ZoneTransfer";
        public const string PermissionChange = "Permission.Change";
        // Group
        public const string FileUpload = "File.Upload";
        public const string FileNewVersion = "File.NewVersion";
        public const string FileDownload = "File.Download";
        public const string FolderCreate = "Folder.Create";
        public const string GroupMemberRole = "GroupMember.RoleChange";
    }
}
