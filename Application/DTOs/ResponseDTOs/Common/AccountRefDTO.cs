namespace Application.DTOs.ResponseDTOs.Common
{
    /// <summary>Tham chieu gon toi 1 tai khoan (id + ten hien thi) — dung trong cac danh sach nhu
    /// participants/mentions de FE khong phai tu resolve GUID sang ten.</summary>
    public class AccountRefDTO
    {
        public Guid AccountId { get; set; }
        public string? Name { get; set; }
    }
}
