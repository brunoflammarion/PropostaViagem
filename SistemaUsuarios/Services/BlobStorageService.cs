using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SistemaUsuarios.Services;

public class BlobStorageService
{
    private readonly BlobContainerClient _container;
    private static readonly HashSet<string> _extensoesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public BlobStorageService(IConfiguration config)
    {
        var connStr    = config["AzureBlobConnectionString"]
            ?? throw new InvalidOperationException("AzureBlobConnectionString não configurada.");
        var container  = config["AzureBlobContainer"] ?? "uploads";
        _container = new BlobContainerClient(connStr, container);
    }

    /// <summary>
    /// Faz upload de uma imagem para o blob e retorna a URL pública.
    /// </summary>
    public async Task<string> SalvarAsync(IFormFile arquivo, string pasta)
    {
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!_extensoesPermitidas.Contains(ext))
            throw new InvalidOperationException("Apenas imagens são permitidas (JPG, PNG, GIF, WebP).");
        if (arquivo.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Arquivo excede 10 MB.");

        var nome      = $"{Guid.NewGuid()}{ext}";
        var blobName  = $"{pasta.Trim('/')}/{nome}";
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = arquivo.ContentType
        };

        using var stream = arquivo.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Faz upload de um arquivo genérico (PDF, etc.) para o blob e retorna a URL pública.
    /// </summary>
    public async Task<string> SalvarArquivoAsync(IFormFile arquivo, string pasta)
    {
        if (arquivo.Length > 50 * 1024 * 1024)
            throw new InvalidOperationException("Arquivo excede 50 MB.");

        var ext       = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var nome      = $"{Guid.NewGuid()}{ext}";
        var blobName  = $"{pasta.Trim('/')}/{nome}";
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = arquivo.ContentType };
        using var stream = arquivo.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Deleta um blob pela URL pública. Ignora se não existir.
    /// </summary>
    public async Task DeletarAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var uri      = new Uri(url);
            var blobName = uri.AbsolutePath.TrimStart('/');
            // Remove o prefixo do container ("/uploads/...") → "uploads/hospedagem/..."
            var containerPrefix = _container.Name + "/";
            if (blobName.StartsWith(containerPrefix))
                blobName = blobName[containerPrefix.Length..];

            var blobClient = _container.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
        catch { /* URL local legada — ignora */ }
    }
}
