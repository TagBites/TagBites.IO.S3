using System;
using Xunit;

namespace TagBites.IO.S3.Tests;

public class S3FileSystemTests
{
    private const string ServiceUrl = "https://s3.eu-central-1.amazonaws.com";
    private const string AccessKey = "accessKey";
    private const string SecretKey = "secretKey";

    [Fact]
    public void Create_ValidArguments_KindIsS3()
    {
        var fileSystem = S3FileSystem.Create(ServiceUrl, AccessKey, SecretKey, "my-bucket");

        Assert.Equal("s3", fileSystem.Kind);
    }

    [Fact]
    public void Create_ValidArguments_NameIsBucketName()
    {
        var fileSystem = S3FileSystem.Create(ServiceUrl, AccessKey, SecretKey, "my-bucket");

        Assert.Equal("my-bucket", fileSystem.Name);
    }

    [Fact]
    public void Create_NullBucketName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => S3FileSystem.Create(ServiceUrl, AccessKey, SecretKey, null));
    }

    [Fact]
    public void CreateCloudflareR2FileSystem_ValidArguments_KindIsS3()
    {
        var fileSystem = S3FileSystem.CreateCloudflareR2FileSystem("account-id", AccessKey, SecretKey, "my-bucket");

        Assert.Equal("s3", fileSystem.Kind);
    }

    [Fact]
    public void CreateCloudflareR2FileSystem_ValidArguments_NameIsBucketName()
    {
        var fileSystem = S3FileSystem.CreateCloudflareR2FileSystem("account-id", AccessKey, SecretKey, "my-bucket");

        Assert.Equal("my-bucket", fileSystem.Name);
    }

    [Fact]
    public void CreateCloudflareR2FileSystem_NullBucketName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => S3FileSystem.CreateCloudflareR2FileSystem("account-id", AccessKey, SecretKey, null));
    }
}
