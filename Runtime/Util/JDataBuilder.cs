using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace UnityREST.Util
{
    public static class JDataBuilder
    {
        /// <returns>
        /// <code>
        /// {
        ///     "key0": "value 0",
        ///     "key1": "value 1",
        ///     "key2": "value 2",
        ///     ...
        /// }
        /// </code>
        /// </returns>
        public static string JObject(params (string key, string value)[] pairs)
        {
            var payload = new JObject();

            foreach (var pair in pairs)
            {
                payload.Add(pair.key, pair.value);
            }

            return JsonConvert.SerializeObject(payload);
        }

        /// <param name="name">Key name</param>
        /// <param name="collection">Array of objects of type T</param>
        /// <returns>
        /// <code>
        /// {
        ///     "key": [
        ///       
        ///         {   "element": "0"  },
        ///         {   "element": "1"  },
        ///         ...
        ///     ]
        /// }
        /// </code>
        /// </returns>
        public static string JArrayObject<T>(string name, IEnumerable<T> collection)
        {
            var payload = new JObject
            {
                { name, JArray(collection) }
            };

            return JsonConvert.SerializeObject(payload);
        }

        /// <param name="collection">Array of objects of type T</param>
        /// <returns>
        /// <code>
        /// [
        ///     {   "element": "0"  },
        ///     {   "element": "1"  },
        ///     ...
        /// ]
        /// </code>
        /// </returns>
        public static string JArray<T>(IEnumerable<T> collection)
        {
            return JToken.FromObject(collection.ToArray()).ToString();
        }
    }
}