using System;

namespace MDUA.Web.UI.Extensions
{
    public static class DateExtensions
    {
        //This handles database columns that allow NULLs:UpdatedAt or DeletedAt.
        public static string ToUtcString(this DateTime? date)
        {
            if (!date.HasValue) return "";
            return date.Value.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
        }
        //This handles database columns that are Mandatory (NOT NULL):CreatedAt
        public static string ToUtcString(this DateTime date)
        {
            return date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
        }
    }
}