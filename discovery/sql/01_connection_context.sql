SELECT
    DB_NAME() AS database_name,
    ORIGINAL_LOGIN() AS original_login,
    SUSER_SNAME() AS execution_login,
    USER_NAME() AS database_user,
    CAST(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'SELECT') AS int) AS has_database_select,
    CAST(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS int) AS has_view_definition,
    @@VERSION AS sql_server_version;
