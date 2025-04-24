-- $manifold$
-- $include$ [http-for-manifold-9.sql]

-- Returns http(s) response as a string
SELECT 
	HttpGetString('https://httpbin.org/anything', false)
FROM 
	(VALUES (1))
;

-- Returns http(s) response as a binary
-- Converts it to Manifold Tile if possible
SELECT	
	TileFromBinary(HttpGetBinary('https://manifold.net/images/people/y/21.png', false))
FROM 
	(VALUES (1))
;
-- https://manifold.net/images/mfd9_logo_left.png

-- Downloads from HTTP/HTTPS URL to a local file and returns status message
-- Returns status message (success or error)
-- Be careful to escape \\ in file path or use @'' string.
SELECT 
	HttpDownload('https://manifold.net/images/mfd9_logo_left.png', @'C:\Downloads\mfd9_logo_left.png', false)
FROM 
	(VALUES (1))
;


VALUE @COPERNICUS_TOKEN_URL NVARCHAR = "https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token";
VALUE @COPERNICUS_CLIENT_ID NVARCHAR = '';
VALUE @COPERNICUS_CLIENT_SECRET NVARCHAR = '';

SELECT 
	HttpInitToken(@COPERNICUS_TOKEN_URL, @COPERNICUS_CLIENT_ID, @COPERNICUS_CLIENT_SECRET)
FROM 
	(VALUES (1))
;

VALUE @jp2_url NVARCHAR = 'https://zipper.dataspace.copernicus.eu/odata/v1/Products(d8cd67b1-6d1a-4242-b301-b94ebcf7a8a9)/Nodes(S2B_MSIL2A_20250401T095029_N0511_R079_T35VLF_20250401T120350.SAFE)/Nodes(GRANULE)/Nodes(L2A_T35VLF_A042147_20250401T095027)/Nodes(IMG_DATA)/Nodes(R10m)/Nodes(T35VLF_20250401T095029_TCI_10m.jp2)/$value'

SELECT 
	HttpDownload(@jp2_url, @'D:\Data\S2B_MSIL2A_20250401T095029_N0511_R079_T35VLF_20250401T120350.jp2', true)
FROM 
	(VALUES (1))
;

SELECT 
	HttpInitToken(@COPERNICUS_TOKEN_URL, @COPERNICUS_CLIENT_ID, @COPERNICUS_CLIENT_SECRET)
FROM 
	(VALUES (1))
;


SELECT 
	S3GetString('s3://eodata/Sentinel-2/MSI/L2A/2025/03/28/S2B_MSIL2A_20250328T114639_N0511_R023_T33XVG_20250328T135433.SAFE/MTD_MSIL2A.xml')
FROM 
	(VALUES (1))
;

SELECT 
	S3Download('s3://eodata/.../T33XVG_20250328T114639_TCI_10m.jp2', 'C:\data\sentinel\image.jp2')
FROM 
	(VALUES (1))
;


-- curl --silent --request POST --url https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token --header "Content-Type: application/x-www-form-urlencoded" --data "username=<USERNAME>" --data "password=<PWD>" --data "grant_type=password" --data "client_id=cdse-public" 
-- curl --silent --request POST --url https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token --header 'content-type: application/x-www-form-urlencoded' --data 'grant_type=client_credentials&client_id=fwe43-verf34-ytd4523-45567' --data-urlencode 'client_secret=erer@#$&!tyjyt'