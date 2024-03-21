using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using TagBites.IO.Operations;

namespace TagBites.IO.S3;
internal class S3FileSystemOperations : IFileSystemAsyncWriteOperations, IFileSystemMetadataSupport, IDisposable
{
    private readonly string _bucketName;
    private readonly IAmazonS3 _storageClient;

    public char DirectorySeparator => '/';
    public string DirectorySeparatorString => "/";

    public string Kind => "s3";
    public string Name => _bucketName;

    #region IFileSystemOperationsMetadataSupport

    bool IFileSystemMetadataSupport.SupportsIsHiddenMetadata => false;
    bool IFileSystemMetadataSupport.SupportsIsReadOnlyMetadata => false;
    bool IFileSystemMetadataSupport.SupportsLastWriteTimeMetadata => false;

    #endregion

    public S3FileSystemOperations(string serviceUrl, string accessKey, string secretKey, string bucketName)
    {
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _storageClient = new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = serviceUrl,
        });

        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<IFileSystemStructureLinkInfo> GetLinkInfoAsync(string fullName)
    {

        try
        {
            return await GetLinkInfoCoreAsync(fullName);
        }
        catch
        {
            if (Path.HasExtension(fullName))
                return null;

            try
            {
                var correctFullName = GetCorrectDirectoryFullName(fullName);
                return await GetLinkInfoCoreAsync(correctFullName);
            }
            catch
            {
                return null;
            }
        }
    }
    private async Task<IFileSystemStructureLinkInfo> GetLinkInfoCoreAsync(string fullName)
    {
        var response = await _storageClient.GetObjectMetadataAsync(_bucketName, fullName);
        return GetInfo(fullName, response);
    }

    public async Task ReadFileAsync(FileLink file, Stream stream)
    {
        var response = await _storageClient.GetObjectAsync(_bucketName, file.FullName);
        await using var responseStream = response.ResponseStream;
        await responseStream.CopyToAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);
    }
    public async Task<IFileLinkInfo> WriteFileAsync(FileLink file, Stream stream, bool overwrite)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = file.FullName,
            InputStream = stream,
            DisablePayloadSigning = true
        };
        var result = await _storageClient.PutObjectAsync(request);

        var response = await _storageClient.GetObjectMetadataAsync(_bucketName, file.FullName);
        return new FileInfo(file.FullName, response);
    }
    public Task<IFileLinkInfo> MoveFileAsync(FileLink source, FileLink destination, bool overwrite)
    {
        throw new NotImplementedException();
        //var result = await _storageClient.CopyObjectAsync(_bucketName, source.FullName, _bucketName, destination.FullName);
        //await _storageClient.DeleteObjectAsync(_bucketName, source.FullName);

        //var response = await _storageClient.GetObjectMetadataAsync(_bucketName, destination.FullName);
        //return new FileInfo(destination.FullName, response);
    }
    public async Task DeleteFileAsync(FileLink file) => await _storageClient.DeleteObjectAsync(_bucketName, file.FullName);

    public async Task<IFileSystemStructureLinkInfo> CreateDirectoryAsync(DirectoryLink directory)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = directoryFullName,
            InputStream = new MemoryStream(Array.Empty<byte>()), // Provide an empty stream
            DisablePayloadSigning = true
        };

        var result = await _storageClient.PutObjectAsync(request);

        var metadataRequest = new GetObjectMetadataRequest
        {
            BucketName = _bucketName,
            Key = directoryFullName
        };
        var metadataResponse = await _storageClient.GetObjectMetadataAsync(metadataRequest);
        return new DirectoryInfo(directory.FullName, metadataResponse);
    }
    public Task<IFileSystemStructureLinkInfo> MoveDirectoryAsync(DirectoryLink source, DirectoryLink destination)
    {
        throw new NotImplementedException();
        //var sourceFullName = GetCorrectDirectoryFullName(source.FullName);
        //var destinationFullName = GetCorrectDirectoryFullName(destination.FullName);

        //var copyRequest = new CopyObjectRequest
        //{
        //    SourceBucket = _bucketName,
        //    DestinationBucket = _bucketName,
        //    SourceKey = sourceFullName,
        //    DestinationKey = destinationFullName,
        //};

        //var result = await _storageClient.CopyObjectAsync(copyRequest);
        //await _storageClient.DeleteObjectAsync(_bucketName, sourceFullName);
        //var metadataRequest = new GetObjectMetadataRequest
        //{
        //    BucketName = _bucketName,
        //    Key = destinationFullName
        //};
        //var metadataResponse = await _storageClient.GetObjectMetadataAsync(metadataRequest);
        //return new DirectoryInfo(destinationFullName, metadataResponse);
    }
    public async Task DeleteDirectoryAsync(DirectoryLink directory, bool recursive)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);

        if (recursive)
        {
            var isTruncated = true;
            string continuationToken = null;
            while (isTruncated)
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = directoryFullName,
                    ContinuationToken = continuationToken
                };
                var response = await _storageClient.ListObjectsV2Async(request);


                var deleteRequest = new DeleteObjectsRequest()
                {
                    BucketName = _bucketName,
                    Objects = response.S3Objects.Select(x => new KeyVersion() { Key = x.Key }).ToList()
                };
                var deleteResponse = await _storageClient.DeleteObjectsAsync(deleteRequest);

                continuationToken = response.NextContinuationToken;
                isTruncated = response.IsTruncated;
            }
        }
        else
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = directoryFullName,
            };
            var response = await _storageClient.ListObjectsV2Async(request);

            if (response.S3Objects.Any(x => x.Key != directoryFullName) || response.CommonPrefixes.Any())
                throw new IOException("Folder is not empty.");

            var _ = await _storageClient.DeleteObjectAsync(_bucketName, directoryFullName);
        }
    }

    public async Task<IList<IFileSystemStructureLinkInfo>> GetLinksAsync(DirectoryLink directory, FileSystem.ListingOptions options)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        options.RecursiveHandled = true;

        var isTruncated = true;
        string continuationToken = null;
        var result = new List<IFileSystemStructureLinkInfo>();

        var delimiter = !options.Recursive ? DirectorySeparatorString : null;
        while (isTruncated)
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = directoryFullName,
                Delimiter = delimiter,
                ContinuationToken = continuationToken
            };
            var response = await _storageClient.ListObjectsV2Async(request);

            result.AddRange(response.S3Objects.Where(x => x.Key != directoryFullName).Select(GetInfo));

            if (!options.Recursive && options.SearchForDirectories && response.CommonPrefixes.Count > 0)
            {
                foreach (var linkFullName in response.CommonPrefixes)
                {
                    var link = await GetLinkInfoAsync(linkFullName);
                    if (link != null)
                        result.Add(link);
                }
            }

            continuationToken = response.NextContinuationToken;
            isTruncated = response.IsTruncated;
        }

        return result;
    }
    public async Task<IFileSystemStructureLinkInfo> UpdateMetadataAsync(FileSystemStructureLink link, IFileSystemLinkMetadata metadata)
    {
        var obj = await _storageClient.GetObjectAsync(_bucketName, link.FullName);
        return GetInfo(obj);
    }

    private static IFileSystemStructureLinkInfo GetInfo(string fullName, GetObjectMetadataResponse response)
    {
        if (response == null)
            return null;

        if (fullName.EndsWith("/"))
            return new DirectoryInfo(fullName, response);

        return new FileInfo(fullName, response);
    }
    private static IFileSystemStructureLinkInfo GetInfo(GetObjectResponse response)
    {
        if (response == null)
            return null;

        if (response.Key.EndsWith("/"))
            return new DirectoryInfo(response);

        return new FileInfo(response);
    }
    private static IFileSystemStructureLinkInfo GetInfo(S3Object response)
    {
        if (response.Key.EndsWith("/"))
            return new DirectoryInfo(response);

        return new FileInfo(response);
    }

    private string GetCorrectDirectoryFullName(string directoryFullName) => directoryFullName?.TrimEnd(DirectorySeparator) + DirectorySeparator;

    public void Dispose() => _storageClient?.Dispose();

    private class FileInfo : IFileLinkInfo
    {
        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => false;
        public DateTime? CreationTime { get; }
        public DateTime? LastWriteTime { get; }
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public string ContentPath => FullName;
        public FileHash Hash { get; }
        public long Length { get; }

        public FileInfo(GetObjectResponse response)
        {
            FullName = response.Key;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
            Length = response.ContentLength;
            Hash = new FileHash(FileHashAlgorithm.Md5, response.ETag);
        }
        public FileInfo(S3Object response)
        {
            FullName = response.Key;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
            Length = response.Size;
            Hash = new FileHash(FileHashAlgorithm.Md5, response.ETag);
        }
        public FileInfo(string fullName, GetObjectMetadataResponse response)
        {
            FullName = fullName;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
            Length = response.ContentLength;
            Hash = new FileHash(FileHashAlgorithm.Md5, response.ETag);
        }

    }
    private class DirectoryInfo : IFileSystemStructureLinkInfo
    {
        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => true;
        public DateTime? CreationTime { get; }
        public DateTime? LastWriteTime { get; }
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public DirectoryInfo(GetObjectResponse response)
        {
            FullName = response.Key;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
        }
        public DirectoryInfo(S3Object response)
        {
            FullName = response.Key;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
        }
        public DirectoryInfo(string fullName, GetObjectMetadataResponse response)
        {
            FullName = fullName;
            CreationTime = response.LastModified;
            LastWriteTime = response.LastModified;
        }
    }
}

