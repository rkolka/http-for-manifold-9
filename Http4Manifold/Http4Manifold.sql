-- $manifold$

-- Returns Http GET `@url` response or exception as string
FUNCTION HttpGet(
    @url NVARCHAR) 
    NVARCHAR 
AS SCRIPT FILE 'Http4Manifold.dll' ENTRY 'Script.HttpGet';

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
