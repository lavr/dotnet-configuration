using System.Collections.Generic;
using System.Linq;

namespace Lavr.Configuration
{
    public static class Helpers
    {

        public static object GetByPath(object root, string path)
        {
            if (root is not IDictionary<object, object> currentDict || string.IsNullOrWhiteSpace(path))
                return null;

            var segments = path.Split('.');
            object current = currentDict;

            foreach (var seg in segments)
            {
                if (current is IDictionary<object, object> dict && dict.TryGetValue(seg, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        public static T GetByPath<T>(object root, string path)
        {
            var val = GetByPath(root, path);
            return val is T casted ? casted : default!;
        }
    }
}
