using FluentValidation;

namespace Crm.Application.Common;

/// <summary>Converts row versions to and from the opaque <c>version</c> string used in the API.</summary>
public static class Versioning
{
    public static string ToVersion(this byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] FromVersion(string version)
    {
        try
        {
            return Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("version", "Invalid version.")]);
        }
    }
}
