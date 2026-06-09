using System.Text;
#if NET8_0
using System.Buffers;
#else
using System.Buffers.Text;
#endif

namespace MnestixCore.Shared;

public static class Base64StringDeAndEncoder
{
    public static string DecodeFrom64(string encodedData)
    {
        var encodedDataAsBytes = Base64UrlDecode(encodedData);
        return Encoding.ASCII.GetString(encodedDataAsBytes);
    }

    public static string EncodeTo64(string toEncode)
    {
        var toEncodeAsBytes = Encoding.ASCII.GetBytes(toEncode);
        return Base64UrlEncode(toEncodeAsBytes);
    }

#if NET8_0
    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }

        return Convert.FromBase64String(output);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
#else
    private static byte[] Base64UrlDecode(string input)
    {
        return Base64Url.DecodeFromChars(input);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Base64Url.EncodeToString(input);
    }
#endif
}
