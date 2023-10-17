using System;
using System.Collections.Generic;
using System.Text;

namespace YaR.Clouds.Base
{
    public static class WebDavPath
    {
        public static bool IsFullPath(string path)
        {
            return path.StartsWith("/");
        }


        public static string Combine(string a, string b)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            if (b == null)
                return Clean(a, false);

            StringBuilder right = CleanSb(b, false);

            if (a == "/disk")
            {
                right.Insert(0, a);
                if (right[right.Length - 1] == '/')
                    right.Remove(right.Length - 1, 1);
                return right.ToString();
            }

            StringBuilder left = CleanSb(a, false);

#if NET48
            left.Append(right.ToString());
#else
            left.Append(right);
#endif

            return left.ToString();

        }

        public static StringBuilder CleanSb(string path, bool doAddFinalSeparator = false)
        {
            try
            {
                if (path == null)
                    throw new ArgumentNullException(nameof(path));

                StringBuilder text = new StringBuilder(path);
                text.Replace("\\", "/").Replace("//", "/");
                if (doAddFinalSeparator)
                {
                    if (text.Length == 0 || text[^1] != '/')
                        text.Append('/');
                }
                else
                {
                    if (text.Length > 1 && text[^1] == '/')
                        text.Remove(text.Length - 1, 1);
                }

                if (text.Length == 0 || text[0] != '/')
                    text.Insert(0, '/');

                return text;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static string Clean(string path, bool doAddFinalSeparator = false)
        {
            return CleanSb(path, doAddFinalSeparator).ToString();
        }

        public static string Parent(string path, string cmdPrefix = ">>")
        {
            // cause we use >> as a sign of special command
            int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);
            int len = path.EndsWith("/") ? path.Length - 1 : path.Length;
            if (len == 0)
                return Root;
            int slash = path.LastIndexOf("/", len - 1, cmdPos < 0 ? len : len - cmdPos, StringComparison.Ordinal);

            return slash > 0
                ? path.Substring(0, slash)
                : Root;
        }

        public static string Name(string path, string cmdPrefix = ">>")
        {
            // cause we use >> as a sign of special command
            int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);
            int len = path.EndsWith("/") ? path.Length - 1 : path.Length;
            if (len == 0)
                return "";
            int slash = path.LastIndexOf("/", len - 1, cmdPos < 0 ? len : len - cmdPos, StringComparison.Ordinal);

            if (slash < 0 && len == path.Length)
                return path;

            return path.Substring(slash + 1, len - slash - 1);
        }

        public static string Root => "/";
        public static string Separator => "/";

        public static bool IsParentOrSame(string parent, string child)
        {
            return IsParent(parent, child, true);
        }

        public static bool IsParent(string parent, string child, bool selfTrue = false)
        {
            parent = Clean(parent, true);
            child = Clean(child, true);
            return child.StartsWith(parent) && (selfTrue || parent.Length != child.Length);
        }

        public static WebDavPathParts Parts(string path)
        {
            //TODO: refact
            var res = new WebDavPathParts
            {
                Parent = Parent(path),
                Name = Name(path)
            };

            return res;
        }

        public static List<string> GetParents(string path, bool includeSelf = true)
        {
            List<string> result = new List<string>();

            path = Clean(path);
            if (includeSelf)
                result.Add(path);

            while (path != Root)
            {
                path = Parent(path);
                result.Add(path);
            }

            return result;
        }

        public static string ModifyParent(string path, string oldParent, string newParent)
        {
            if (!IsParentOrSame(oldParent, path))
                return path;

            if (path is null)
                throw new ArgumentNullException(nameof(path));
            if (oldParent is null)
                throw new ArgumentNullException(nameof(oldParent));
            if (newParent is null)
                throw new ArgumentNullException(nameof(newParent));
            if (path.Length < oldParent.Length)
                throw new ArgumentException($"Value of {nameof(oldParent)} is longer then length of {nameof(path)}");

            StringBuilder pathTmp = CleanSb(path, true);
            StringBuilder oldParentTmp = CleanSb(oldParent, true);
            path = pathTmp.Remove(0, oldParentTmp.Length).ToString();

            return Combine(newParent, path);
        }

        public static bool PathEquals(string path1, string path2)
        {
            return Clean(path1).Equals(Clean(path2), StringComparison.InvariantCultureIgnoreCase);
        }

        public static string EscapeDataString(string path)
        {
            return Uri
                .EscapeDataString(path ?? string.Empty)
                .Replace("#", "%23");
        }
    }

    public struct WebDavPathParts
    {
        public string Parent { get; set; }
        public string Name { get; set; }
    }
}
