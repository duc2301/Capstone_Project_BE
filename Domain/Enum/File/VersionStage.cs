namespace Domain.Enum.File
{
    // Giai đoạn của tài liệu trong vòng đời version:
    // Working (P) = đang làm việc (WIP/SHARED), Published (C) = đã phát hành.
    public enum VersionStage
    {
        Working = 0,    // "P" — hiển thị dạng P{Revision}.{Version}, vd: P01.02
        Published = 1   // "C" — hiển thị dạng C{PublishedRevision}, vd: C01
    }
}
