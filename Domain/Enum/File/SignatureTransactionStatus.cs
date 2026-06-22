namespace Domain.Enum.File
{
    public enum SignatureTransactionStatus
    {
        Created = 0,
        WaitingConfirm = 1,
        Signed = 2,
        Failed = 3,
        Expired = 4
    }
}
