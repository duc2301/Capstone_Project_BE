using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;

namespace Application.Services
{
    /// <summary>
    /// Dựng phần mô tả cho audit log của các thao tác phân quyền. Tách riêng vì bốn đường ghi quyền
    /// (phân quyền thư mục, phân quyền tệp, ma trận, thu hồi chia sẻ) đều cần trả lời đúng một câu hỏi
    /// — "ai được đổi thành mức nào" — mà trước đây mỗi nơi chỉ ghi được con số ("cho 2 bên tham gia",
    /// "1 ô"), đọc log xong vẫn không biết đã đụng vào bên nào.
    /// Tên mức quyền lấy theo cùng hợp đồng N/R/W của PermissionLevelMapper để log không mô tả một
    /// đằng, dữ liệu lưu một nẻo.
    /// </summary>
    public static class PermissionAuditDescriber
    {
        /// <summary>Nhãn khi một chủ thể bị gỡ khỏi danh sách phân quyền (trả về kế thừa/không quyền).</summary>
        public const string RemovedLabel = "gỡ quyền";

        /// <summary>Tên tiếng Việt của mức quyền, dùng chung cho mọi dòng log.</summary>
        public static string LevelName(PermissionLevel level) => level switch
        {
            PermissionLevel.Write => "xem và sửa",
            PermissionLevel.Read => "chỉ xem",
            PermissionLevel.NoAccess => "chặn",
            _ => "kế thừa theo thư mục"
        };

        /// <summary>Mức quyền suy từ cặp cờ của payload — cùng quy đổi với PermissionLevelMapper.</summary>
        public static string LevelName(bool canView, bool canEdit)
            => LevelName(PermissionLevelMapper.FromFlags(canView, canEdit));

        /// <summary>Một mục "chủ thể → mức quyền" trong câu mô tả.</summary>
        public static string Entry(string subject, string level) => $"{subject} → {level}";

        /// <summary>
        /// Nối các mục thành một câu, cắt bớt khi quá dài để dòng log không tràn: liệt kê tối đa
        /// maxListed mục rồi tóm phần còn lại thành "và N mục khác".
        /// </summary>
        public static string Join(IReadOnlyList<string> entries, int maxListed = 4)
        {
            if (entries.Count == 0)
                return "không có thay đổi";
            if (entries.Count <= maxListed)
                return string.Join("; ", entries);

            return string.Join("; ", entries.Take(maxListed))
                   + $" và {entries.Count - maxListed} mục khác";
        }

        /// <summary>Tên nhóm (bên tham gia) theo ProjectParticipantId.</summary>
        public static async Task<Dictionary<Guid, string>> ResolveGroupNamesAsync(
            IUnitOfWork unitOfWork, IReadOnlyCollection<Guid> participantIds)
        {
            var ids = participantIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<Guid, string>();

            var participants = await unitOfWork.Repository<ProjectParticipant>()
                .FindAsync(pp => ids.Contains(pp.Id), nameof(ProjectParticipant.Group));

            return participants.ToDictionary(
                pp => pp.Id,
                pp => string.IsNullOrWhiteSpace(pp.Group?.Name) ? UnknownGroup : pp.Group!.Name);
        }

        /// <summary>Tên nhóm của một participant, an toàn khi bên đó đã rời dự án.</summary>
        public static string GroupNameOf(IReadOnlyDictionary<Guid, string> names, Guid participantId)
            => names.TryGetValue(participantId, out var name) ? name : UnknownGroup;

        /// <summary>
        /// Nhãn người dùng dạng "Tên (Nhóm A)" — tên tài khoản kèm (các) nhóm họ đang tham gia dự án.
        /// Chỉ mỗi tên là chưa đủ để truy vết: cùng một người có thể thuộc bên khác nhau, và khi đọc
        /// log điều cần biết là quyền vừa đổi thuộc về bên nào.
        /// projectId null (chưa xác định được dự án) thì trả về tên trần thay vì bỏ trống cả dòng.
        /// </summary>
        public static async Task<Dictionary<Guid, string>> ResolveAccountLabelsAsync(
            IUnitOfWork unitOfWork, Guid? projectId, IReadOnlyCollection<Guid> accountIds)
        {
            var ids = accountIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<Guid, string>();

            var accounts = await unitOfWork.Repository<Account>().FindAsync(a => ids.Contains(a.Id));
            var nameById = accounts.ToDictionary(
                a => a.Id,
                a => string.IsNullOrWhiteSpace(a.UserName) ? (a.Email ?? UnknownAccount) : a.UserName);

            var groupsByAccount = await ResolveProjectGroupsByAccountAsync(unitOfWork, projectId, ids);

            var result = new Dictionary<Guid, string>();
            foreach (var id in ids)
            {
                var name = nameById.TryGetValue(id, out var found) ? found : UnknownAccount;
                result[id] = groupsByAccount.TryGetValue(id, out var groups) && groups.Count > 0
                    ? $"{name} ({string.Join(", ", groups)})"
                    : name;
            }
            return result;
        }

        /// <summary>Nhãn của một tài khoản, an toàn khi tài khoản đã bị xoá khỏi dự án.</summary>
        public static string AccountLabelOf(IReadOnlyDictionary<Guid, string> labels, Guid accountId)
            => labels.TryGetValue(accountId, out var label) ? label : UnknownAccount;

        // ---------- nội bộ ----------

        private const string UnknownGroup = "bên đã rời dự án";
        private const string UnknownAccount = "tài khoản không xác định";

        /// <summary>
        /// Các nhóm ACTIVE trong dự án mà mỗi tài khoản đang là thành viên active. Lọc theo nhóm của
        /// dự án chứ không lấy mọi nhóm của tài khoản: nhóm ở dự án khác không nói lên điều gì ở đây.
        /// </summary>
        private static async Task<Dictionary<Guid, List<string>>> ResolveProjectGroupsByAccountAsync(
            IUnitOfWork unitOfWork, Guid? projectId, IReadOnlyCollection<Guid> accountIds)
        {
            var byAccount = new Dictionary<Guid, List<string>>();
            if (!projectId.HasValue)
                return byAccount;

            var projectGroupIds = (await unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(pp => pp.ProjectId == projectId.Value
                                  && pp.Status == ProjectParticipantStatus.Active))
                .Select(pp => pp.GroupId)
                .Distinct()
                .ToList();
            if (projectGroupIds.Count == 0)
                return byAccount;

            var members = await unitOfWork.Repository<GroupMember>()
                .FindAsync(m => accountIds.Contains(m.AccountId)
                             && projectGroupIds.Contains(m.GroupId)
                             && m.Status == GroupMemberStatus.Active,
                           nameof(GroupMember.Group));

            foreach (var member in members)
            {
                var groupName = member.Group?.Name;
                if (string.IsNullOrWhiteSpace(groupName))
                    continue;

                if (!byAccount.TryGetValue(member.AccountId, out var names))
                    byAccount[member.AccountId] = names = new List<string>();
                if (!names.Contains(groupName))
                    names.Add(groupName);
            }
            return byAccount;
        }
    }
}
