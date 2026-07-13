/* Params requeridos: @schemaName y @objectName (texto). */
SELECT
    'KEY' AS relation_kind,
    s.name AS schema_name,
    o.name AS object_name,
    i.name AS constraint_or_relation_name,
    CASE WHEN i.is_primary_key = 1 THEN 'PRIMARY KEY' ELSE 'UNIQUE' END AS detail,
    c.name AS local_column,
    NULL AS referenced_schema,
    NULL AS referenced_object,
    NULL AS referenced_column,
    ic.key_ordinal AS ordinal
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
INNER JOIN sys.indexes AS i
    ON i.object_id = o.object_id
   AND (i.is_primary_key = 1 OR i.is_unique_constraint = 1)
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE s.name = @schemaName
  AND o.name = @objectName

UNION ALL

SELECT
    'FOREIGN KEY' AS relation_kind,
    CASE WHEN ps.name = @schemaName AND po.name = @objectName THEN ps.name ELSE rs.name END AS schema_name,
    CASE WHEN ps.name = @schemaName AND po.name = @objectName THEN po.name ELSE ro.name END AS object_name,
    fk.name AS constraint_or_relation_name,
    CASE
        WHEN ps.name = @schemaName AND po.name = @objectName THEN 'OUTBOUND'
        ELSE 'INBOUND'
    END AS detail,
    CASE
        WHEN ps.name = @schemaName AND po.name = @objectName THEN pc.name
        ELSE rc.name
    END AS local_column,
    CASE
        WHEN ps.name = @schemaName AND po.name = @objectName THEN rs.name
        ELSE ps.name
    END AS referenced_schema,
    CASE
        WHEN ps.name = @schemaName AND po.name = @objectName THEN ro.name
        ELSE po.name
    END AS referenced_object,
    CASE
        WHEN ps.name = @schemaName AND po.name = @objectName THEN rc.name
        ELSE pc.name
    END AS referenced_column,
    fkc.constraint_column_id AS ordinal
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.objects AS po ON po.object_id = fk.parent_object_id
INNER JOIN sys.schemas AS ps ON ps.schema_id = po.schema_id
INNER JOIN sys.columns AS pc
    ON pc.object_id = po.object_id
   AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.objects AS ro ON ro.object_id = fk.referenced_object_id
INNER JOIN sys.schemas AS rs ON rs.schema_id = ro.schema_id
INNER JOIN sys.columns AS rc
    ON rc.object_id = ro.object_id
   AND rc.column_id = fkc.referenced_column_id
WHERE (ps.name = @schemaName AND po.name = @objectName)
   OR (rs.name = @schemaName AND ro.name = @objectName)
ORDER BY relation_kind, constraint_or_relation_name, ordinal;
