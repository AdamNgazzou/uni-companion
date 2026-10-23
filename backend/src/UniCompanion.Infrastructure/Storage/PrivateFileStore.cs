using System.Security.Cryptography;

namespace UniCompanion.Infrastructure.Storage;

public sealed class PrivateFileStore(string root)
{
    public string PathFor(string sha256)
    {
        if (sha256.Length != 64 || sha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Invalid content hash.", nameof(sha256));
        return Path.Combine(root, sha256[..2], sha256);
    }

    public async Task PutVerifiedAsync(string source, string sha256, CancellationToken cancellationToken)
    {
        var destination = PathFor(sha256);
        if (File.Exists(destination))
        {
            await VerifyAsync(destination, sha256, cancellationToken);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid() + ".tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew))
                await input.CopyToAsync(output, cancellationToken);
            await VerifyAsync(temporary, sha256, cancellationToken);
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task VerifyAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
        if (actual != expected) throw new InvalidDataException("Material checksum mismatch.");
    }
}
