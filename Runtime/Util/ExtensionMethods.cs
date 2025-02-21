using System;
using System.Linq;

namespace UnityREST.Util
{
    public static class ExtensionMethods
    {
        public static bool IsBase64Encoded(this string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length % 4 != 0)
            {
                return false;
            }

            if (!input.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '='))
            {
                return false;
            }

            try
            {
                var decodedBytes = Convert.FromBase64String(input);

                var reEncoded = Convert.ToBase64String(decodedBytes);

                return input.TrimEnd('=') == reEncoded.TrimEnd('=');
            }
            catch
            {
                return false;
            }
        }
    }
}