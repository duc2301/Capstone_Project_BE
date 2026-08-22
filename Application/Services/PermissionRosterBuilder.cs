using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Services
{
    /// <summary>
    /// Dựng roster cho hộp thoại "Phân quyền thành viên": gộp các dòng (nhóm cấp quyền) đã resolve
    /// thành danh sách PHẲNG theo từng thành viên. Dùng chung cho cả file và folder — điểm khác chỉ
    /// nằm ở cách hai service tính ra tập grant view-granting (file có present-wins + fallback folder).
    /// </summary>
    public static class PermissionRosterBuilder
    {
        public static List<MemberPermissionItemDTO> Build(
            Dictionary<Guid, GroupGrantDTO> viewGrantByParticipant,
            List<MemberOfParticipantDTO> members,
            Dictionary<Guid, IPermissionAcl> overrideByAccount,
            Guid callerAccountId)
        {
            return members
                .Where(m => m.AccountId != callerAccountId)
                .GroupBy(m => m.AccountId)
                .Select(g =>
                {
                    var memberGrants = g
                        .Where(x => viewGrantByParticipant.ContainsKey(x.ParticipantId))
                        .Select(x => viewGrantByParticipant[x.ParticipantId])
                        .ToList();
                    var first = g.First();

                    var overrideLevel = ResolveOverrideLevel(overrideByAccount, first.AccountId);

                    return new MemberPermissionItemDTO
                    {
                        AccountId = first.AccountId,
                        UserName = first.UserName,
                        Email = first.Email,
                        Groups = memberGrants.Select(x => x.GroupName).Distinct().ToList(),
                        InheritedCanView = true,                          // roster = chỉ người có View
                        InheritedCanEdit = memberGrants.Any(x => x.CanEdit),
                        OverrideLevel = overrideLevel,
                        IsBlacklisted = overrideLevel == "Blocked"
                    };
                })
                .OrderBy(x => x.UserName)
                .ToList();
        }

        // Dịch dòng override (nếu có) sang mức hiển thị trên UI. Không có dòng = "None" (kế thừa nhóm).
        private static string ResolveOverrideLevel(Dictionary<Guid, IPermissionAcl> overrideByAccount, Guid accountId)
        {
            if (!overrideByAccount.TryGetValue(accountId, out var acl))
                return "None";
            if (!acl.CanView)
                return "Blocked";
            return acl.CanEdit ? "Edit" : "View";
        }
    }
}
