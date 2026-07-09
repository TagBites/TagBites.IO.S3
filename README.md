# TagBites.IO.S3

Amazon S3 and S3-compatible storage (e.g. Cloudflare R2) file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `AWSSDK.S3`. Browse, read and write an S3 bucket through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.S3
```

## Usage

```csharp
using TagBites.IO.S3;

var fs = S3FileSystem.Create(serviceUrl, accessKey, secretKey, bucketName);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText();
```

### Cloudflare R2

```csharp
using TagBites.IO.S3;

var fs = S3FileSystem.CreateCloudflareR2FileSystem(accountId, accessKey, secretKey, bucketName);
```

S3 has no real directory concept, so directories are represented as object key prefixes.

## License

See [https://www.tagbites.com/io](https://www.tagbites.com/io) for licensing terms.
