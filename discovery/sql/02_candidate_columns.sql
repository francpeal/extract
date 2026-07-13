/*
  Inventario heuristico: los terminos solo ayudan a localizar candidatos.
  Un resultado no confirma la semantica funcional del objeto o la columna.
*/
WITH table_rows AS
(
    SELECT p.object_id, SUM(p.rows) AS approximate_rows
    FROM sys.partitions AS p
    WHERE p.index_id IN (0, 1)
    GROUP BY p.object_id
),
object_columns AS
(
    SELECT
        o.object_id,
        s.name AS schema_name,
        o.name AS object_name,
        o.type_desc AS object_type,
        tr.approximate_rows,
        c.column_id,
        c.name AS column_name,
        ty.name AS data_type,
        LOWER(o.name) AS normalized_object_name,
        LOWER(c.name) AS normalized_column_name
    FROM sys.objects AS o
    INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
    INNER JOIN sys.columns AS c ON c.object_id = o.object_id
    INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN table_rows AS tr ON tr.object_id = o.object_id
    WHERE o.type IN ('U', 'V')
      AND o.is_ms_shipped = 0
)
SELECT
    oc.schema_name,
    oc.object_name,
    oc.object_type,
    oc.approximate_rows,
    oc.column_id,
    oc.column_name,
    oc.data_type,
    candidate_match.candidate_reason
FROM object_columns AS oc
CROSS APPLY
(
    VALUES
    ('article', CASE WHEN oc.normalized_object_name LIKE '%artic%' OR oc.normalized_column_name LIKE '%artic%'
                       OR oc.normalized_object_name LIKE '%producto%' OR oc.normalized_column_name LIKE '%producto%'
                       OR oc.normalized_object_name LIKE '%item%' OR oc.normalized_column_name LIKE '%item%'
                       OR oc.normalized_object_name LIKE '%sku%' OR oc.normalized_column_name LIKE '%sku%'
                     THEN 1 ELSE 0 END),
    ('price', CASE WHEN oc.normalized_object_name LIKE '%precio%' OR oc.normalized_column_name LIKE '%precio%'
                    OR oc.normalized_object_name LIKE '%price%' OR oc.normalized_column_name LIKE '%price%'
                    OR oc.normalized_object_name LIKE '%tarifa%' OR oc.normalized_column_name LIKE '%tarifa%'
                  THEN 1 ELSE 0 END),
    ('stock', CASE WHEN oc.normalized_object_name LIKE '%stock%' OR oc.normalized_column_name LIKE '%stock%'
                    OR oc.normalized_object_name LIKE '%existenc%' OR oc.normalized_column_name LIKE '%existenc%'
                    OR oc.normalized_object_name LIKE '%inventar%' OR oc.normalized_column_name LIKE '%inventar%'
                    OR oc.normalized_object_name LIKE '%saldo%' OR oc.normalized_column_name LIKE '%saldo%'
                  THEN 1 ELSE 0 END),
    ('warehouse', CASE WHEN oc.normalized_object_name LIKE '%almacen%' OR oc.normalized_column_name LIKE '%almacen%'
                        OR oc.normalized_object_name LIKE '%warehouse%' OR oc.normalized_column_name LIKE '%warehouse%'
                        OR oc.normalized_object_name LIKE '%deposito%' OR oc.normalized_column_name LIKE '%deposito%'
                      THEN 1 ELSE 0 END),
    ('currency', CASE WHEN oc.normalized_object_name LIKE '%moneda%' OR oc.normalized_column_name LIKE '%moneda%'
                       OR oc.normalized_object_name LIKE '%currency%' OR oc.normalized_column_name LIKE '%currency%'
                     THEN 1 ELSE 0 END)
) AS candidate_match(candidate_reason, is_match)
WHERE candidate_match.is_match = 1
ORDER BY
    candidate_match.candidate_reason,
    oc.schema_name,
    oc.object_name,
    oc.column_id;
