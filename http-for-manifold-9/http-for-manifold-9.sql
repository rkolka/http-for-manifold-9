-- $manifold$

-- Returns Http GET `@url` response or error string
FUNCTION HttpGetString(
    @url NVARCHAR,
    @use_token BOOLEAN) 
    NVARCHAR 
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpGetString';

-- Returns binary content or error string as bytes
FUNCTION HttpGetBinary(
    @url NVARCHAR,
    @use_token BOOLEAN)
    VARBINARY              
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpGetBinary';

-- Downloads from HTTP/HTTPS URL to a local file
-- Returns status message (success or error)
FUNCTION HttpDownload(
    @url NVARCHAR,
    @localFilePath NVARCHAR,
    @use_token BOOLEAN)
    NVARCHAR          
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpDownload';


-- Gets OAuth token
FUNCTION HttpInitToken(
   @token_url NVARCHAR,
   @client_id NVARCHAR,
   @client_secret NVARCHAR)
   NVARCHAR
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpInitToken';


-- NEW: Performs a generic HTTP POST request. Headers and Form Data are provided as JSON strings.
-- Returns response body as string or 'ERROR: ...'.
FUNCTION HttpPostGeneric(
    @url NVARCHAR,
    @headersJson NVARCHAR, -- e.g., '{"Accept":"application/json"}' (Content-Type for form data is usually automatic)
    @dataJson NVARCHAR     -- e.g., '{"grant_type":"password", "username":"...", "password":"..."}'
)
NVARCHAR
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpPostGeneric';

-- Returns Http POST `@url` `@postData` response or exception as string
FUNCTION HttpPost(
    @url NVARCHAR,
    @postData NVARCHAR,
    @contentType NVARCHAR)
    NVARCHAR
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.HttpPost';

-- Creates a Manifold Tile from VARBINARY image data (e.g., png, jpg, gif, bmp)
-- Returns a Manifold Tile object or NULL on error
FUNCTION TileFromBinary(
    @imageData VARBINARY)
    TILE              
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.TileFromBinary';

-- Gets S3 object content as string
FUNCTION S3GetString(
    @s3Url NVARCHAR)
    NVARCHAR          -- Returns content or error message
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.S3GetString';

-- Downloads S3 object to a local file and returns status message
-- Returns status message (success or error)
FUNCTION S3Download(
    @s3Url NVARCHAR,
    @localFilePath NVARCHAR)
    NVARCHAR          
AS SCRIPT FILE 'http-for-manifold-9.dll' ENTRY 'Script.S3Download';

