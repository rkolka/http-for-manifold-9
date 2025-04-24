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
    private static readonly string AddinName = "http-for-manifold-9";
    private static readonly string AddinCodeFolder = "Code\\http-for-manifold-9";

    private static readonly string[] CodeFiles = { "http-for-manifold-9.sql", "http-for-manifold-9-examples.sql" };


    // The current application context provided by Manifold at run time
    private static Context Manifold;

    // static constructor. 
    private static readonly HttpClient _httpClient;
    private static readonly IAmazonS3 _s3Client;
    private static string _bearer_token;
    private static DateTime _bearer_token_expiresAt = DateTime.MinValue;

    static Script()
    {

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
        return "Use include directive in your queries:\r\n-- $include$ [http-for-manifold-9.sql]";
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
    public static string HttpInitToken(string tokenUrl, string clientId, string clientSecret)
    {
        try
        {
            int seconds = HttpInitTokenInternal(tokenUrl, clientId, clientSecret).GetAwaiter().GetResult();
            return $"Token saved and good for {seconds} seconds.";
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }
    }


    // Private async method that uses HttpClient to POST form-encoded data,
    // then parses the JSON response to extract "access_token".
    private static async Task<int> HttpInitTokenInternal(string tokenUrl, string clientId, string clientSecret)
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
                _bearer_token = tokenData["access_token"];
            }
            else
            {
                throw new Exception("No access_token field found in response: " + json);
            }

            // If there's an expires_in, store the expiration
            if (tokenData.ContainsKey("expires_in"))
            {
                // expires_in is in seconds. Let's convert to an absolute time.
                int expiresSeconds = tokenData["expires_in"];
                _bearer_token_expiresAt = DateTime.UtcNow.AddSeconds(expiresSeconds);
                return expiresSeconds;
            }
            else
            {
                // fallback
                _bearer_token_expiresAt = DateTime.UtcNow.AddMinutes(30);
                return 1800;
            }
        }
    }




    /// <summary>
    /// Performs an HTTP GET using an OAuth bearer token.
    /// </summary>
    public static string HttpGetString(string url, bool use_token)
    {
        if (use_token && string.IsNullOrEmpty(_bearer_token))
        {
            return "No token initialized. Call HttpInitToken first.";
        }

        // Optionally check if token is expired or near expiry and refresh automatically
        if (use_token && DateTime.UtcNow > _bearer_token_expiresAt)
        {
            return "Token expired, call HttpInitToken again.";
        }

        try
        {
            return HttpGetStringInternal(url, use_token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return "ERROR: " + ex.Message;
        }
    }


    private static async Task<string> HttpGetStringInternal(string url, bool use_token)
    {
        // Build an HTTP request with the Bearer token
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (use_token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearer_token);
        }

        // Perform the request
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

 
    public static byte[] HttpGetBinary(string url, bool use_token)
    {
        if (use_token && string.IsNullOrEmpty(_bearer_token))
        {
            return Encoding.UTF8.GetBytes("No token initialized. Call HttpInitToken first.");
        }

        if (use_token && DateTime.UtcNow > _bearer_token_expiresAt)
        {
            return Encoding.UTF8.GetBytes("Token expired, call HttpInitToken again.");
        }

        try
        {
            return HttpGetBinaryInternal(url, true).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return Encoding.UTF8.GetBytes(ex.Message); ;
        }
    }

    private static async Task<byte[]> HttpGetBinaryInternal(string url, bool use_token)
    {
        // Build an HTTP request with the Bearer token
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (use_token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearer_token);
        }

        // Perform the request
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        byte[] data = await response.Content.ReadAsByteArrayAsync();
        return data;
    }


    public static string HttpDownload(string url, string localFilePath, bool use_token)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "ERROR: URL cannot be empty.";
        }
        
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            return "ERROR: Local file path cannot be empty.";
        }

        if (use_token && string.IsNullOrEmpty(_bearer_token))
        {
            return "No token initialized. Call HttpInitToken first.";
        }

        if (use_token && DateTime.UtcNow > _bearer_token_expiresAt)
        {
            return "Token expired, call HttpInitToken again.";
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
            HttpDownloadInternal(url, localFilePath, use_token).GetAwaiter().GetResult();
            return $"Success: Downloaded '{url}' to '{localFilePath}'";
        }
        catch (Exception ex)
        {
            // Catch specific exceptions like DirectoryNotFoundException, UnauthorizedAccessException etc. if needed
            return $"ERROR: {ex.Message}";
        }
    }

    private static async Task HttpDownloadInternal(string url, string localFilePath, bool use_token)
    {
        // Build an HTTP request with the Bearer token
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (use_token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearer_token);
        }

        // Use GetAsync with HttpCompletionOption.ResponseHeadersRead for efficiency with large files
        using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
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
