/* Ejecutar con una cuenta de solo lectura. No modifica el esquema ni los datos. */
SELECT
    c.table_schema,
    c.table_name,
    c.ordinal_position,
    c.column_name,
    c.data_type,
    c.character_maximum_length,
    c.numeric_precision,
    c.numeric_scale,
    c.is_nullable,
    c.column_default,
    c.is_identity
FROM information_schema.columns AS c
WHERE c.table_schema = current_schema()
  AND c.table_name IN
      ('articulos', 'clientes', 'almacenes', 'lista_precios', 'precios', 'stock_almacen')
ORDER BY c.table_name, c.ordinal_position;

SELECT
    tc.table_schema,
    tc.table_name,
    tc.constraint_name,
    tc.constraint_type,
    kcu.ordinal_position,
    kcu.column_name,
    ccu.table_schema AS referenced_schema,
    ccu.table_name AS referenced_table,
    ccu.column_name AS referenced_column
FROM information_schema.table_constraints AS tc
LEFT JOIN information_schema.key_column_usage AS kcu
    ON kcu.constraint_schema = tc.constraint_schema
   AND kcu.constraint_name = tc.constraint_name
LEFT JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_schema = tc.constraint_schema
   AND ccu.constraint_name = tc.constraint_name
WHERE tc.table_schema = current_schema()
  AND tc.table_name IN
      ('articulos', 'clientes', 'almacenes', 'lista_precios', 'precios', 'stock_almacen')
ORDER BY tc.table_name, tc.constraint_type, tc.constraint_name, kcu.ordinal_position;

SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = current_schema()
  AND tablename IN
      ('articulos', 'clientes', 'almacenes', 'lista_precios', 'precios', 'stock_almacen')
ORDER BY tablename, indexname;

SELECT 'articulos' AS table_name, count(*) AS row_count FROM articulos
UNION ALL SELECT 'clientes', count(*) FROM clientes
UNION ALL SELECT 'almacenes', count(*) FROM almacenes
UNION ALL SELECT 'lista_precios', count(*) FROM lista_precios
UNION ALL SELECT 'precios', count(*) FROM precios
UNION ALL SELECT 'stock_almacen', count(*) FROM stock_almacen;

SELECT 'articulos.codigo' AS candidate_key, codigo, count(*) AS occurrences
FROM articulos GROUP BY codigo HAVING count(*) > 1
UNION ALL
SELECT 'almacenes.codigo', codigo, count(*)
FROM almacenes GROUP BY codigo HAVING count(*) > 1
UNION ALL
SELECT 'lista_precios.codigo', codigo, count(*)
FROM lista_precios GROUP BY codigo HAVING count(*) > 1
UNION ALL
SELECT 'clientes.cod_dap', cod_dap, count(*)
FROM clientes WHERE cod_dap IS NOT NULL GROUP BY cod_dap HAVING count(*) > 1;

SELECT 'clientes.ruc' AS candidate_key, ruc, count(*) AS occurrences
FROM clientes
GROUP BY ruc
HAVING count(*) > 1;

SELECT
    count(*) FILTER (WHERE codigo IS NULL OR btrim(codigo) = '') AS articulos_invalid_code
FROM articulos;

SELECT
    count(*) FILTER (WHERE cod_dap IS NULL OR btrim(cod_dap) = '') AS clientes_invalid_dap,
    count(*) FILTER (WHERE ruc IS NULL OR btrim(ruc) = '') AS clientes_invalid_ruc
FROM clientes;

SELECT 'precios' AS table_name, cod_articulo, cod_lista, count(*) AS occurrences
FROM precios
GROUP BY cod_articulo, cod_lista
HAVING count(*) > 1;

SELECT 'stock_almacen' AS table_name, cod_articulo, cod_almacen, count(*) AS occurrences
FROM stock_almacen
GROUP BY cod_articulo, cod_almacen
HAVING count(*) > 1;
