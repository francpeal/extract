/* Ejecutar únicamente contra PostgreSQL con la cuenta operativa del ETL. */
DELETE FROM etl_staging_records
WHERE expires_at < now();
