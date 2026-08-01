using Everdue.Server.Application.Common;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// The no-silent-truncation rule, in one place so every capped export refuses the same way.
///
/// A truncated file that looks complete is the worst possible input to a decision somebody is about to
/// make, so passing the limit is a 400 with an instruction rather than a shorter file.
/// </summary>
internal static class ExportGuard
{
    public static void EnsureWithinLimit(int total, int max)
    {
        if (total > max)
        {
            throw new ValidationException(
                $"This export would contain {total:N0} rows, over the {max:N0}-row limit. Narrow the filters.");
        }
    }
}
