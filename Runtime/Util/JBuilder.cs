using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace UnityREST.Util
{
    public static class JBuilder
    {
        /// <summary>
        /// Serializes the specified object to a JSON string using formatting.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>A JSON string representation of the object.</returns>
        public static string Object(object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }
        
        /// <returns>
        /// String
        /// <code>
        /// {
        ///     "key0": "value 0",
        ///     "key1": "value 1",
        ///     "key2": "value 2",
        ///     ...
        /// }
        /// </code>
        /// </returns>
        public static string Object<T>(params (string key, T value)[] pairs)
        {
            var payload = new JObject();

            foreach (var pair in pairs)
            {
                var token = new TokenizedObject<T>(pair.value);

                payload.Add(pair.key, token);
            }

            return Object(payload);
        }

        /// <param name="key">Key name</param>
        /// <param name="collection">Array of objects of type T</param>
        /// <returns>
        /// String
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
        public static string ArrayObject<T>(string key, IEnumerable<T> collection)
        {
            var payload = new JObject
            {
                { key, Array(collection) }
            };

            return Object(payload);
        }

        /// <param name="collection">Array of objects of type T</param>
        /// <returns>
        /// String
        /// <code>
        /// [
        ///     {   "element": "0"  },
        ///     {   "element": "1"  },
        ///     ...
        /// ]
        /// </code>
        /// </returns>
        public static JToken Array<T>(IEnumerable<T> collection)
        {
            return JToken.FromObject(collection);
        }

        /// <summary>
        /// Returns a JToken from an Object or ArrayObject
        /// </summary>
        /// <example>
        /// <code>
        /// string array = JBuilder.Array(collection);
        /// string arrayObject = JBuilder.ArrayObject("names", collection);
        /// Debug.Log(JBuilder.Object(("names", array), ("arrayObject", arrayObject.ParseToJToken())));
        /// </code>
        /// </example>
        /// <returns>
        /// JToken
        /// <code>
        /// {
        ///     "names": [
        ///         {
        ///             "Item1": "name1",
        ///             "Item2": "surname1"
        ///         },
        ///         {
        ///             "Item1": "name2",
        ///             "Item2": "surname2"
        ///         }
        ///     ],
        ///     "arrayObject": {
        ///         "names": [
        ///             {
        ///                 "Item1": "name1",
        ///                 "Item2": "surname1"
        ///             },
        ///             {
        ///                 "Item1": "name2",
        ///                 "Item2": "surname2"
        ///             }
        ///         ]
        ///     }
        /// }
        /// </code>
        /// </returns>
        public static JToken ParseToJToken(this string value)
        {
            return JToken.Parse(value);
        }
    }

    public class TokenizedObject<T>
    {
        protected JToken token { get; }

        public TokenizedObject(T type)
        {
            token = JToken.FromObject(type);
        }

        public static implicit operator JToken(TokenizedObject<T> tokenizedObject)
        {
            return tokenizedObject.token;
        }

        public static implicit operator string(TokenizedObject<T> tokenizedObject)
        {
            return tokenizedObject.token.ToString(Formatting.None);
        }
    }
}