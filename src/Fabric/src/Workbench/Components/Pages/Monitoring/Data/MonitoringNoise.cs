using System.Security.Cryptography;
using System.Text;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// Numbers and ids that a key always gives the same, so the sample data of the monitoring pages comes out the same on
/// every run, wherever it is rendered.
/// </summary>
public static class MonitoringNoise
{
    /// <summary>A number from 0 to 1 for a key and an index.</summary>
    public static double Of(string key, long index)
    {
        unchecked
        {
            var hash = 14695981039346656037UL;

            foreach (var c in key)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            hash ^= (ulong)index;
            hash *= 1099511628211UL;

            // SplitMix64's finalizer, so neighboring indexes don't give neighboring numbers.
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;

            return (hash >> 11) / (double)(1UL << 53);
        }
    }

    /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    public static int Between(string key, long index, int min, int max) => min + (int)Math.Floor(Of(key, index) * (max - min + 1));

    /// <summary>The same id for the same key on every run.</summary>
    public static Guid IdOf(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));
}
