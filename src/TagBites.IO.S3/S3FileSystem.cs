namespace TagBites.IO.S3;
public class S3FileSystem
{
    public static FileSystem Create(string serviceUrl, string accessKey, string secretKey, string bucketName)
    {
        return new FileSystem(new S3FileSystemOperations(serviceUrl, accessKey, secretKey, bucketName), FileSystemFlags.IsDirectoryAsPrefix);
    }
    public static FileSystem CreateCloudflareR2FileSystem(string accountId, string accessKey, string secretKey, string bucketName)
    {
        return Create($"https://{accountId}.r2.cloudflarestorage.com", accessKey, secretKey, bucketName);
    }
}
