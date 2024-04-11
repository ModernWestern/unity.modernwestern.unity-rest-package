using System.Text;
using System.Collections.Generic;

namespace UnityREST.Util
{
    public static class URLBuilder
    {
        /// <summary>
        /// Build url with given parameters as GET request.
        /// </summary>
        /// <example>
        /// <code>
        /// url: www.site.com/indexwww.site.com/index
        /// key: id, value: 3;
        /// key: style, value: 2;
        /// result: www.site.com/index?id=3&amp;style=2
        /// </code>
        /// </example>
        public static string Parameters(string url, Dictionary<string, string> parameters)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append(url);
            stringBuilder.Append("?");

            var count = 0;

            foreach (var values in parameters)
            {
                stringBuilder.Append(values.Key);
                stringBuilder.Append("=");
                stringBuilder.Append(values.Value);

                if (count < parameters.Count - 1)
                {
                    stringBuilder.Append("&");
                }

                count++;
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Build a list of values as a GET array.
        /// </summary>
        /// <example>
        /// <code>
        /// url: www.site.com/index?id=
        /// array: [1,2,3,4,5]
        /// result: www.site.com/index?id=1,2,3,4,5
        /// </code>
        /// </example>
        public static string ParameterArray(params string[] array)
        {
            var parameterValue = string.Empty;

            for (int i = 0, l = array.Length; i < l; i++)
            {
                parameterValue += array[i];

                if (i < array.Length - 1)
                {
                    parameterValue += ",";
                }
            }

            return parameterValue;
        }
    }
}