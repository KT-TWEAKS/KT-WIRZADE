using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KTWirzade.Shared.Helpers
{
    public static class AtomicDownload
    {
        public static void ValidateHash(string hash)
        {
            if (hash != null && (hash.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(hash, "\\A[0-9a-fA-F]{64}\\z")))
                throw new ArgumentException("Expected SHA-256 must contain 64 hexadecimal characters.");
        }

        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<string> SaveAsync(Stream source, string destination, long? expectedSize,
            string expectedHash, Action<long> progress, CancellationToken cancellationToken)
        {
            ValidateHash(expectedHash);
            var fullPath = Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                long received = 0;
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        received += read;
                        if (expectedSize.HasValue && received > expectedSize.Value)
                            throw new InvalidDataException("Download exceeds the expected length.");
                        progress?.Invoke(received);
                    }
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                if (expectedSize.HasValue && received != expectedSize.Value)
                    throw new InvalidDataException("Incomplete download. Please try again.");
                var actualHash = HashFile(temp);
                if (expectedHash != null && !actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Download SHA-256 mismatch.");
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(fullPath)) File.Replace(temp, fullPath, null);
                else File.Move(temp, fullPath);
                return actualHash;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
