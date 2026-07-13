/*
  Params requeridos: @schemaName, @objectName y @sampleSize.
  Ejecutar solo sobre un objeto candidato ya revisado. El limite efectivo es 100.
*/
DECLARE @qualifiedName nvarchar(517);
DECLARE @effectiveSampleSize int;
DECLARE @sql nvarchar(max);

SELECT @qualifiedName = QUOTENAME(s.name) + N'.' + QUOTENAME(o.name)
FROM sys.objects AS o
INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE s.name = @schemaName
  AND o.name = @objectName
  AND o.type IN ('U', 'V')
  AND o.is_ms_shipped = 0;

IF @qualifiedName IS NULL
BEGIN
    RAISERROR('The requested table or view does not exist.', 16, 1);
    RETURN;
END;

SET @effectiveSampleSize = CASE
    WHEN @sampleSize IS NULL OR @sampleSize < 1 THEN 10
    WHEN @sampleSize > 100 THEN 100
    ELSE @sampleSize
END;

SET @sql = N'SELECT TOP (' + CONVERT(nvarchar(3), @effectiveSampleSize)
    + N') * FROM ' + @qualifiedName + N';';

EXEC sp_executesql @sql;
