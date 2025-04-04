-- $manifold$
-- $include$ [Http4Manifold.sql]


SELECT 
	HttpGet('https://httpbin.org/anything')
FROM 
	(VALUES (1))
;