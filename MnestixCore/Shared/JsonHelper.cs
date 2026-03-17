using Newtonsoft.Json.Linq;

namespace MnestixCore.Shared
{
    public static class JsonHelper
    {
        public static bool IsValidJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();
            if (!(input.StartsWith("{") && input.EndsWith("}")) &&
                !(input.StartsWith("[") && input.EndsWith("]")))
            {
                return false;
            }

            try
            {
                JToken.Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
