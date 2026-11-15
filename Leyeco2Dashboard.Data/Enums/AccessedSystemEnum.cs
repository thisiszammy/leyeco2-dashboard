namespace Leyeco2Dashboard.Data.Enums
{
    // The systems whose staff sign in with these accounts, each honouring the grants of one
    // PermissionEnum range (PermissionEnumExtensions.GetAccessedSystem). The numbering continues
    // the HRIS's (HRIS = 2), so a shared credential manager could hold both. Append new members,
    // never renumber or rename one.
    public enum AccessedSystemEnum
    {
        TransactionMonitor = 3
    }
}
