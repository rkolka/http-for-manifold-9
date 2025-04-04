using Manifold;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer; // For S3Download
using System.Drawing;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;

public class Script
{
    private static readonly string AddinName = "Http4Manifold";
    private static readonly string AddinCodeFolder = "Code\\Http4Manifold";

    private static readonly string[] CodeFiles = { "Http4Manifold.sql", "Http4Manifold-examples.sql" };


    // The current application context provided by Manifold at run time
    private static Context Manifold;

    // static constructor. 
    private static readonly HttpClient _httpClient;
    private static readonly IAmazonS3 _s3Client;
    private static string _bearer_token;


    static Script()
    {
        System.Diagnostics.Debug.WriteLine("Dll loading");

        System.Diagnostics.Debug.WriteLine(Manifold is null);

        // Force TLS 1.2, if needed:
        System.Net.ServicePointManager.SecurityProtocol =
            System.Net.SecurityProtocolType.Tls12
            | System.Net.SecurityProtocolType.Tls11
            | System.Net.SecurityProtocolType.Tls;

        // Create the shared HttpClient instance
        _httpClient = new HttpClient();
        // Optionally set defaults, e.g.:
        // _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Assumes credentials are configured via standard AWS SDK methods
        // (e.g., environment variables, instance profile, shared credential file)
        // You might need to specify a region, e.g., _s3Client = new AmazonS3Client(RegionEndpoint.USEast1);
        // For public buckets like 'eodata', explicit credentials might not be needed,
        // but the SDK still needs to be configured minimally (e.g., region).
        // For Sentinel-2 bucket 'eodata', it's typically in 'eu-central-1'
        _s3Client = new AmazonS3Client(RegionEndpoint.EUCentral1); 
    }

    public static void Main()
    {
        Application app = Manifold.Application;

        using (Database db = app.GetDatabaseRoot())
        {
            CreateQueries(app, db);
        }

        app.Log(DisplayHelp());
        app.OpenLog();

    }



    private static string DisplayHelp()
    {
        return "Use include directive:\r\n-- $include$ [Http4Manifold.sql]";
    }


    public static string HttpGetString(string url)
    {
        try
        {
            // Wrap the async call in a sync call
            return HttpGetInternal(url).GetAwaiter().GetResult();
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (Exception ex)
        {
            return "ERROR (General Exception): " + ex.Message;
        }
    }

    private static async Task<string> HttpGetInternal(string url)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(url);
        // Throw if not success
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static string HttpPost(string uri, string postData, string contentType = "application/x-www-form-urlencoded")
    {
        try
        {
            // Sync-over-async
            return HttpPostInternal(uri, postData, contentType).GetAwaiter().GetResult();
        }
        catch (UriFormatException ex)
        {
            return "ERROR (UriFormatException): " + ex.Message;
        }
        catch (Exception ex)
        {
            return "ERROR (General Exception): " + ex.Message;
        }
    }

    private static async Task<string> HttpPostInternal(string url, string postData, string contentType)
    {
        // Prepare content
        var dataBytes = new StringContent(postData, Encoding.UTF8, contentType);
        HttpResponseMessage response = await _httpClient.PostAsync(url, dataBytes);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Gets an OAuth token from the given tokenUrl using client credentials flow.
    /// This returns the raw access_token string (Bearer token) to the caller.
    /// </summary>
    public static string HttpGetToken(string tokenUrl, string clientId, string clientSecret)
    {
        try
        {
            // Synchronously wait for the async method
            _bearer_token = HttpGetTokenInternal(tokenUrl, clientId, clientSecret).GetAwaiter().GetResult();
            return _bearer_token;
        }
        catch (Exception ex)
        {
            // Return errors as a string so Manifold won't see null
            return "ERROR: " + ex.Message;
        }
    }
    public static string SetToken(string token)
    {
        _bearer_token = token;
        return _bearer_token;
    }


    // Private async method that uses HttpClient to POST form-encoded data,
    // then parses the JSON response to extract "access_token".
    private static async Task<string> HttpGetTokenInternal(string tokenUrl, string clientId, string clientSecret)
    {
        // The standard client-credentials body:
        //   grant_type=client_credentials&client_id=...&client_secret=...
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        using (var response = await _httpClient.PostAsync(tokenUrl, formData))
        {
            response.EnsureSuccessStatusCode(); // Throws if not successful (4xx/5xx)
            string json = await response.Content.ReadAsStringAsync();

            // Typical OAuth JSON structure: {"access_token":"...", "token_type":"bearer", "expires_in":3600, ...}
            var serializer = new JavaScriptSerializer();
            var tokenData = serializer.Deserialize<dynamic>(json);

            if (tokenData.ContainsKey("access_token"))
            {
                return tokenData["access_token"];
            }

            throw new Exception("No access_token field found in response: " + json);
        }
    }

    /// <summary>
    /// Performs an HTTP GET using an OAuth bearer token.
    /// The token is passed in as a parameter.
    /// </summary>
    public static string HttpGetWithToken(string url, string token)
    {
        try
        {
            return HttpGetWithTokenInternal(url, token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }
    }

    private static async Task<string> HttpGetWithTokenInternal(string url, string token)
    {
        // Build an HTTP request with the Bearer token
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Perform the request
        using (var response = await _httpClient.SendAsync(request))
        {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    // NEW Function: Downloads content from URL as byte array
    public static byte[] HttpGetBinary(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            System.Diagnostics.Debug.WriteLine("HttpGetBinary ERROR: URL is empty.");
            // Return null or empty array on error? Throwing exception might be better for SQL.
            // SQL typically expects NULL on function error if return type allows it.
            // However, VARBINARY might not be nullable in all contexts. Let's throw.
            throw new ArgumentNullException(nameof(url));
        }

        try
        {
            // Use sync-over-async approach
            return HttpGetBinaryInternal(url).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            // Log the error and re-throw or wrap it
            System.Diagnostics.Debug.WriteLine($"HttpGetBinary ERROR (HttpRequestException): {ex.Message} (URL: {url})");
            // Rethrowing allows SQL to handle the error (often results in NULL)
            throw new HttpRequestException($"Failed to download binary data from {url}. Error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HttpGetBinary ERROR (General Exception): {ex.ToString()} (URL: {url})");
            throw new Exception($"An unexpected error occurred downloading binary data from {url}. Error: {ex.Message}", ex);
        }
    }

    private static async Task<byte[]> HttpGetBinaryInternal(string url)
    {
        // Download data into memory
        byte[] data = await _httpClient.GetByteArrayAsync(url);
        if (data == null || data.Length == 0)
        {
            // Consider throwing an exception if empty data is an error condition
            System.Diagnostics.Debug.WriteLine($"HttpGetBinary WARNING: Downloaded data is empty for URL: {url}");
            // Depending on requirements, you might return empty array or throw:
            // throw new Exception("Downloaded data is empty.");
        }
        return data;
    }


    public static string HttpDownload(string url, string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "ERROR: URL cannot be empty.";
        }
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            return "ERROR: Local file path cannot be empty.";
        }

        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use sync-over-async for simplicity
            HttpDownloadInternal(url, localFilePath).GetAwaiter().GetResult();
            return $"Success: Downloaded '{url}' to '{localFilePath}'";
        }
        catch (HttpRequestException ex)
        {
            return $"ERROR (HttpRequestException): {ex.Message} (URL: {url})";
        }
        catch (Exception ex)
        {
            // Catch specific exceptions like DirectoryNotFoundException, UnauthorizedAccessException etc. if needed
            return $"ERROR (General Exception): {ex.Message} (URL: {url})";
        }
    }

    private static async Task HttpDownloadInternal(string url, string localFilePath)
    {
        // Use GetAsync with HttpCompletionOption.ResponseHeadersRead for efficiency with large files
        using (HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode(); // Throw if download failed

            // Get the response stream
            using (Stream contentStream = await response.Content.ReadAsStreamAsync())
            {
                // Create a file stream to save the download
                using (FileStream fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Copy the content stream to the file stream
                    // Use a buffer for better performance
                    await contentStream.CopyToAsync(fileStream);
                }
            }
        }
    }


    // Parses S3 URL like 's3://bucket-name/key/path/object.ext'
    private static bool S3ParseUrl(string s3Url, out string bucketName, out string key)
    {
        bucketName = null;
        key = null;
        if (string.IsNullOrWhiteSpace(s3Url) || !s3Url.StartsWith("s3://"))
            return false;

        var parts = s3Url.Substring(5).Split(new[] { '/' }, 2);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return false;

        bucketName = parts[0];
        key = parts[1];
        return true;
    }

    // Gets S3 object content as string
    public static string S3GetString(string s3Url)
    {
        if (!S3ParseUrl(s3Url, out string bucketName, out string key))
        {
            return "ERROR: Invalid S3 URL format. Expected 's3://bucket-name/key'.";
        }

        try
        {
            // Use sync-over-async for simplicity in Manifold script context
            return S3GetInternal(bucketName, key).GetAwaiter().GetResult();
        }
        catch (AmazonS3Exception ex)
        {
            return $"ERROR (AmazonS3Exception): {ex.Message} (Request ID: {ex.RequestId}, HTTP Status: {ex.StatusCode})";
        }
        catch (Exception ex)
        {
            return $"ERROR (General Exception): {ex.Message}";
        }
    }

    private static async Task<string> S3GetInternal(string bucketName, string key)
    {
        GetObjectRequest request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };

        using (GetObjectResponse response = await _s3Client.GetObjectAsync(request))
        using (Stream responseStream = response.ResponseStream)
        using (StreamReader reader = new StreamReader(responseStream))
        {
            // Check content type if necessary before reading?
            // For XML like MTD_MSIL2A.xml, reading as string is fine.
            return await reader.ReadToEndAsync();
        }
    }


    // Downloads S3 object to a local file
    public static string S3Download(string s3Url, string localFilePath)
    {
        if (!S3ParseUrl(s3Url, out string bucketName, out string key))
        {
            return "ERROR: Invalid S3 URL format. Expected 's3://bucket-name/key'.";
        }
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            return "ERROR: Local file path cannot be empty.";
        }

        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use TransferUtility for potentially large files and easier download management
            TransferUtility transferUtility = new TransferUtility(_s3Client);

            // Use sync-over-async for simplicity in Manifold script context
            // Note: DownloadAsync doesn't return anything directly on success, throws on error.
            transferUtility.DownloadAsync(localFilePath, bucketName, key).GetAwaiter().GetResult();

            return $"Success: Downloaded '{s3Url}' to '{localFilePath}'";

        }
        catch (AmazonS3Exception ex)
        {
            return $"ERROR (AmazonS3Exception): {ex.Message} (Request ID: {ex.RequestId}, HTTP Status: {ex.StatusCode})";
        }
        catch (Exception ex)
        {
            // Catch specific exceptions like DirectoryNotFoundException, UnauthorizedAccessException etc. if needed
            return $"ERROR (General Exception): {ex.Message}";
        }
    }

    public static void CreateQuery(Application app, Database db, string name, string text, string folder = "")
    {
        PropertySet propertyset = app.CreatePropertySet();
        propertyset.SetProperty("Text", text);
        if (folder != "")
        {
            propertyset.SetProperty("Folder", folder);

        }
        db.Insert(name, "query", null, propertyset);

    }

    public static void CreateQueries(Application app, Database db)
    {
        string AddinDir = System.IO.Path.GetDirectoryName(new System.Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        foreach (string fname in CodeFiles)
        {
            bool rewrite = true;

            if (db.GetComponentType(fname) == "")
            {
                rewrite = true;
            }
            else
            {
                rewrite = false;

                string message = $"{db.GetComponentType(fname).ToUpper()} {fname} already exists. DROP?";

                System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.YesNo;
                System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show(message, AddinName, buttons);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    db.Delete(fname);
                    rewrite = true;
                }
            }

            if (rewrite)
            {
                string text = File.ReadAllText(AddinDir + "\\" + fname);

                // insert
                CreateQuery(app, db, fname, text, AddinCodeFolder);
            }
        }
    }


    // NEW Function: Creates a Manifold Tile from image byte array (using uint8x4)
    // REMINDER: Requires System.Drawing.Common NuGet package and Manifold Context
    public static Tile TileFromBinary(byte[] imageData)
    {
        // Ensure Manifold context is available
        if (Manifold == null || Manifold.Application == null)
        {
            System.Diagnostics.Debug.WriteLine("TileFromBinary ERROR: Manifold context not available.");
            throw new InvalidOperationException("Manifold context not available to create TileBuilder.");
        }
        Application app = Manifold.Application; // Get app context

        if (imageData == null || imageData.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine("TileFromBinary ERROR: Input imageData is null or empty.");
            // Return null or throw? Throwing is consistent.
            throw new ArgumentException("Input image data cannot be null or empty.", nameof(imageData));
        }

        // Use TileBuilder for creation
        TileBuilder builder = app.CreateTileBuilder();
        Tile resultTile = null;

        // Use System.Drawing to load the image and get properties
        try
        {
            using (MemoryStream ms = new MemoryStream(imageData))
            using (Bitmap bitmap = new Bitmap(ms)) // This can throw ArgumentException for invalid formats
            {
                if (bitmap == null)
                {
                    // Should not happen if Bitmap constructor succeeds, but check anyway
                    throw new Exception("Failed to load input data as Bitmap.");
                }

                int width = bitmap.Width;
                int height = bitmap.Height;

                // Always create a uint8x4 (RGBA) tile
                string manifoldPixelTypeString = "uint8x4";

                // Start building the tile
                builder.StartTile(width, height, manifoldPixelTypeString);

                // Get the PixelSet<Point4<byte>>
                var builderPixels = (TileBuilder.PixelSet<Manifold.Point4<byte>>)builder.Pixels;

                // --- Pixel Copying Logic (always to RGBA) ---
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        builderPixels[x, height - y - 1] = new Manifold.Point4<byte>(color.R, color.G, color.B, (byte)(255 - color.A));
                        // Optional: Missing mask based on Alpha
                        // if (color.A == 0) builder.PixelMissingMasks[x, y] = true;
                    }
                }

                // Finish composing the tile
                resultTile = builder.EndTile();

            } // end using Bitmap, MemoryStream
        }
        catch (ArgumentException ex) // Catch specific error from Bitmap constructor
        {
            builder.EndTile(); // Clear builder
            System.Diagnostics.Debug.WriteLine($"TileFromBinary ERROR: Invalid image data or format. {ex.Message}");
            throw new ArgumentException($"Input data is not a valid image format supported by System.Drawing. Error: {ex.Message}", nameof(imageData), ex);
        }
        catch (Exception ex)
        {
            // Clean up builder if pixel setting failed or other error occurred
            builder.EndTile(); // Clear the builder state
            System.Diagnostics.Debug.WriteLine($"TileFromBinary ERROR: {ex.ToString()}");
            throw; // Re-throw the original exception or a wrapped one
        }

        return resultTile;
    }

}
