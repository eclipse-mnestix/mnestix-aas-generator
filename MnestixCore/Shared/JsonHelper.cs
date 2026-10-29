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

        /// <summary>
        /// Parses JSON without upgrading ISO date strings to .NET DateTime objects.
        /// Use this at every boundary that parses AAS JSON and may re-serialize it,
        /// otherwise date/dateTime values get silently reformatted and can fail
        /// BaSyx's strict xs:date/xs:dateTime schema validation.
        /// </summary>
        public static JObject ParseWithoutDateUpgrade(string json)
        {
            using var sreader = new System.IO.StringReader(json);
            using var jreader = new Newtonsoft.Json.JsonTextReader(sreader)
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None
            };
            return JObject.Load(jreader);
        }
    }
}
