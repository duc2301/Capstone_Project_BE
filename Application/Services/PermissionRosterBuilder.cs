using Application.DTOs.ResponseDTOs.Permission;

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
            HashSet<Guid> blacklistedAccountIds,
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

                    return new MemberPermissionItemDTO
                    {
                        AccountId = first.AccountId,
                        UserName = first.UserName,
                        Email = first.Email,
                        Groups = memberGrants.Select(x => x.GroupName).Distinct().ToList(),
                        InheritedCanView = true,                          // roster = chỉ người có View
                        InheritedCanEdit = memberGrants.Any(x => x.CanEdit),
                        IsBlacklisted = blacklistedAccountIds.Contains(first.AccountId)
                    };
                })
                .OrderBy(x => x.UserName)
                .ToList();
        }
    }
}
