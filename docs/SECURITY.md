# Seguridad y operación

## Modelo de confianza acordado

La API no es pública. El acceso previsto proviene únicamente del servidor Ubuntu
autorizado mediante un túnel SSH restringido por IP de origen. El HTTP plano se
considera aceptable dentro de ese túnel.

La autenticación propia de la API no es un requisito del primer hito. Se mantiene
como defensa adicional opcional si cambia la topología, aparecen más consumidores
o el análisis de riesgo lo exige.

## Controles requeridos

- Enlazar Kestrel a `127.0.0.1` siempre que el extremo SSH sea local.
- No publicar el puerto 5000 en la red del cliente ni en Internet.
- Mantener la restricción del túnel a la IP del servidor Ubuntu.
- Usar una cuenta SQL dedicada con permisos de solo lectura y acceso limitado a
  los objetos necesarios.
- Mantener `/query` solo durante el despliegue piloto autorizado, exclusivamente
  sobre loopback/túnel y con una cuenta SQL técnicamente limitada a lectura.
- Retirar `/query` antes de cerrar la aceptación productiva definitiva.
- Mantener credenciales y valores reales fuera de Git.
- No registrar contraseñas, cadenas de conexión ni conjuntos de datos completos.
- Ejecutar el servicio Windows con el mínimo privilegio viable.

## Estado verificado el 2026-07-14

Controles comprobados:

- WinBridgeApi escucha en loopback y Ubuntu lo consume por
  `127.0.0.1:15000` a través del túnel SSH;
- el ETL usa el rol PostgreSQL `sico_etl`, un DSN sin contraseña y
  `PGPASSFILE` con modo `0600`;
- la conexión validada fue `('sico_etl', 'dap', 'America/Lima', 5)`;
- el recorrido integrado fue `dry-run`, sin escrituras de negocio;
- `sico-etl.service` y `sico-etl.timer` no están registrados.

Controles pendientes de aceptación:

- confirmar que la cuenta SQL efectiva de WinBridgeApi carece de escritura y DDL;
- retirar o proteger `/query`;
- reemplazar `LocalSystem` por una cuenta Windows de privilegio mínimo;
- revisar firewall y `pg_hba.conf`, porque PostgreSQL escucha actualmente en
  interfaces IPv4 e IPv6 además de loopback.

## Riesgo residual aceptado

Sin autenticación HTTP, cualquier proceso que consiga acceder al extremo local del
túnel podría invocar la API. Esta exposición se acepta inicialmente porque los
límites de red y SSH identifican al único origen autorizado, y la API objetivo solo
permitirá consultas de lectura cerradas.

La aceptación debe revisarse si ocurre cualquiera de estos cambios:

- la API escucha en una interfaz distinta de localhost;
- el puerto se abre a una red compartida;
- se habilitan nuevos servidores consumidores;
- se incorporan operaciones de escritura;
- el túnel deja de estar restringido por origen;
- los requisitos del cliente exigen autenticación o auditoría individual.

### Excepción temporal para `/query`

El propietario autorizó el 2026-07-14 conservar temporalmente `GET/POST /query`
para consultas operativas durante el despliegue. El endpoint permite cualquier
sentencia que autorice la cuenta SQL y no interpreta de forma confiable si una
consulta es de lectura. Por ello, la cuenta SQL de WinBridgeApi debe carecer de
permisos de escritura y DDL; el endpoint no debe exponerse fuera del túnel ni
considerarse parte del contrato estable del ETL.

## Secretos y configuración

- Los `appsettings*.json` versionados contienen únicamente placeholders.
- El ETL usa un DSN sin contraseña y obtiene la credencial desde
  `/etc/sico-etl/pgpass` mediante `PGPASSFILE`; el archivo pertenece a `sico-etl`,
  usa modo `0600` y nunca debe mostrarse, versionarse ni copiarse a backups sin
  cifrado.
- En producción, la cadena de conexión debe inyectarse mediante configuración
  externa, variables de entorno o un archivo protegido y no versionado.
- La cuenta del servicio debe tener permiso de lectura solamente sobre la fuente
  de configuración necesaria.
- El ETL recibe `WINBRIDGE_BASE_URL` y `POSTGRES_DSN` mediante entorno protegido;
  `.env.example` contiene únicamente valores ficticios.
- La URL de WinBridgeApi aceptada por el ETL debe apuntar a loopback. No se permite
  configurar HTTP directo hacia una interfaz remota.
- La cuenta PostgreSQL del ETL debe limitarse a las seis tablas de destino y a las
  tablas de control necesarias. Las migraciones deben ejecutarse con otra cuenta
  cuando la cuenta operativa no tenga DDL.
- Correos, teléfonos, cuerpos JSON y filas completas no deben registrarse.
- El staging PostgreSQL puede contener payload temporal: debe restringirse a la
  cuenta ETL, marcar vencimiento a las 24 horas y purgarse operativamente.

## Operación

- Mantener timeout de conexión y de comando.
- Aplicar límites de filas, tamaño de respuesta y paginación.
- Registrar inicio, fin, duración, tipo de extracción y cantidad de registros.
- No registrar el cuerpo completo de las respuestas.
- Diferenciar proceso vivo de disponibilidad de SQL Server.
- Definir reintentos en Ubuntu con espera progresiva; la API no debe reintentar
  consultas largas de manera invisible.
- Usar un advisory lock para impedir ejecuciones ETL solapadas.
- Abortar la publicación ante claves duplicadas, nulas o reducciones de volumen
  superiores al umbral aprobado; conservar siempre el último dataset válido.

## Observación sobre firewall

La defensa principal debe ser enlazar el proceso a localhost. Las reglas de
firewall son complementarias y deben probarse en el servidor real; no debe
asumirse que una regla `allow` prevalecerá sobre un bloqueo explícito general.
