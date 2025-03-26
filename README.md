# Http4Manifold
 Manifold 9 addin

- `FUNCTION HttpGet(@geom GEOM, @index INT32, @newCoord FLOAT64x2) GEOM`
   HttpGet builds a new geom from `@geom` where coord at `@index` is replaced with `@newCoord`


- `FUNCTION HttpPost(@geom GEOM, @index INT32, @newCoord FLOAT64x2) GEOM`
   HttpPost builds a new geom from `@geom` where coord at `@index` is moved to `@newCoord` and nearby coords are adjusted according to distance from change origin. 
Line endpoints are not adjusted, unless referred by `@index`.