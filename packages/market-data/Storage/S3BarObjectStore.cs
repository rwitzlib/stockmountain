using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace StockMountain.MarketData.Storage;

public sealed class S3BarObjectStore : IBarObjectStore, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;

    public S3BarObjectStore(S3BarObjectStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BucketName);

        _bucketName = options.BucketName;
        _s3 = CreateClient(options);
    }

    public S3BarObjectStore(IAmazonS3 s3, string bucketName)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
        _bucketName = string.IsNullOrWhiteSpace(bucketName)
            ? throw new ArgumentException("Bucket name is required.", nameof(bucketName))
            : bucketName;
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        try
        {
            await _s3.GetObjectMetadataAsync(_bucketName, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        using var response = await _s3.GetObjectAsync(_bucketName, objectKey, cancellationToken);
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(content);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,
        };

        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public void Dispose() => _s3.Dispose();

    private static IAmazonS3 CreateClient(S3BarObjectStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            return new AmazonS3Client();
        }

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
        };

        return new AmazonS3Client(new BasicAWSCredentials("test", "test"), config);
    }
}
