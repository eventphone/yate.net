using System;

namespace eventphone.yate
{
    public struct InstallResult
    {
        public InstallResult(string priority, string success)
        {
            if (Int32.TryParse(priority, out var prio))
            {
                Priority = prio;
            }
            else
            {
                Priority = -1;
            }
            Success = "true".Equals(success, StringComparison.OrdinalIgnoreCase);
        }

        public readonly int Priority;

        public readonly bool Success;
    }
}
