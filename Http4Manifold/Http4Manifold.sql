-- $manifold$

-- Returns Http GET `@url` response or exception as string
FUNCTION HttpGetString(
    @url NVARCHAR) 
    NVARCHAR 
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpGetString';

-- Returns Http POST `@url` `@postData` response or exception as string
FUNCTION HttpPost(
    @url NVARCHAR,
    @postData NVARCHAR,
    @contentType NVARCHAR)
    NVARCHAR
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpPost';

-- Gets OAuth token
FUNCTION HttpGetToken(
   @token_url NVARCHAR,
   @client_id NVARCHAR,
   @client_secret NVARCHAR)
   NVARCHAR
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpGetToken';


-- Gets `@url` with OAuth token.
FUNCTION HttpGetWithToken(
   @url NVARCHAR,
   @bearer_token NVARCHAR)
   NVARCHAR
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpGetWithToken';


-- Gets S3 object content as string
-- Example: SELECT S3Get('s3://eodata/Sentinel-2/MSI/L2A/2025/03/28/S2B_MSIL2A_20250328T114639_N0511_R023_T33XVG_20250328T135433.SAFE/MTD_MSIL2A.xml');
FUNCTION S3Get(
    @s3Url NVARCHAR)
    NVARCHAR          -- Returns content or error message
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.S3Get';

-- Downloads S3 object to a local file and returns status message
-- Example: SELECT S3Download('s3://eodata/.../T33XVG_20250328T114639_TCI_10m.jp2', 'C:\data\sentinel\image.jp2');
FUNCTION S3Download(
    @s3Url NVARCHAR,
    @localFilePath NVARCHAR)
    NVARCHAR          -- Returns status message (success or error)
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.S3Download';


-- Downloads from HTTP/HTTPS URL to a local file and returns status message
-- Example: SELECT HttpDownload('https://www.manifold.net/images/manifold_icon_128.png', 'C:\temp\manifold_icon.png');
FUNCTION HttpDownload(
    @url NVARCHAR,
    @localFilePath NVARCHAR)
    NVARCHAR          -- Returns status message (success or error)
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpDownload';


-- Example: DECLARE @b VARBINARY = HttpGetBinary('https://www.manifold.net/images/manifold_icon_128.png'); SELECT Length(@b);
FUNCTION HttpGetBinary(
    @url NVARCHAR)
    VARBINARY              -- Returns binary content or NULL on error
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpGetBinary';

-- NEW: Creates a Manifold Tile from VARBINARY image data (e.g., png, jpg, gif, bmp)
-- Example: SELECT TileFromBinary(HttpGetBinary('https://www.manifold.net/images/manifold_icon_128.png'));
-- Requires System.Drawing.Common package reference in the C# project.
FUNCTION TileFromBinary(
    @imageData VARBINARY)
    TILE              -- Returns a Manifold Tile object or NULL on error
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.TileFromBinary';