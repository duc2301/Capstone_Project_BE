namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Cascade cleanup of orphaned per-ACCOUNT override rows (Part 4 of the permission redesign).
    /// Under the mask model an account override only refines what a group grants, so once an account
    /// has no view-granting group left for a resource the override is meaningless — this service
    /// hard-deletes such rows. Orphans are INERT while they exist (the group ceiling denies first),
    /// so cleanup is hygiene, not security: every method is idempotent and safe to re-run.
    ///
    /// Call AFTER the triggering mutation has been committed (the recompute reads the DB), from:
    ///  - T1: file group-ACL save            -> CleanupFileOverridesAsync
    ///  - T2: folder group-ACL save / matrix -> CleanupFolderOverridesAsync (folder + its direct files)
    ///  - T3: member leaves group / group deleted -> CleanupAccountOverridesAsync
    /// Each method stages its deletes and commits once; returns the number of rows deleted.
    /// </summary>
    public interface IPermissionCleanupService
    {
        /// <summary>T1 — a file's group ACL changed: drop account overrides on that file for
        /// accounts no longer in its view pool (file grants present-wins, else parent folder).</summary>
        Task<int> CleanupFileOverridesAsync(Guid fileItemId);

        /// <summary>T2 — a folder's group ACL changed: drop out-of-pool account overrides on the
        /// folder itself AND on its DIRECT child files (only they fall back to this folder's ACL —
        /// group ACLs do not inherit deeper).</summary>
        Task<int> CleanupFolderOverridesAsync(Guid folderId);

        /// <summary>T3 — an account lost a group membership (status change, group deleted): drop
        /// every file/folder account override of that account whose resource pool it left.</summary>
        Task<int> CleanupAccountOverridesAsync(Guid accountId);
    }
}
