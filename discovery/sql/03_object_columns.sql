/* Params requeridos: @schemaName y @objectName (texto). */
SELECT
    s.name AS schema_name,
    o.name AS object_name,
    o.type_desc AS object_type,
    c.column_id,
    c.name AS column_name,
    ty.name AS data_type,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition AS default_definition,
    cc.definition AS computed_definition
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
INNER JOIN sys.columns AS c ON c.object_id = o.object_id
INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
LEFT JOIN sys.computed_columns AS cc
    ON cc.object_id = c.object_id
   AND cc.column_id = c.column_id
WHERE s.name = @schemaName
  AND o.name = @objectName
  AND o.type IN ('U', 'V')
  AND o.is_ms_shipped = 0
ORDER BY c.column_id;
