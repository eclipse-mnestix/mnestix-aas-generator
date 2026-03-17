using Microsoft.AspNetCore.WebUtilities;

namespace MnestixCore.Shared;

public static class Base64StringDeAndEncoder
{
    public static string DecodeFrom64(string encodedData)
    {
        var encodedDataAsBytes = WebEncoders.Base64UrlDecode(encodedData);
        var returnValue = System.Text.Encoding.ASCII.GetString(encodedDataAsBytes);
        return returnValue;
    }

    public static string EncodeTo64(string toEncode)
    {
        var toEncodeAsBytes = System.Text.Encoding.ASCII.GetBytes(toEncode);
        var returnValue = WebEncoders.Base64UrlEncode(toEncodeAsBytes);
        return returnValue;
    }
}