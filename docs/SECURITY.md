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
- Retirar `/query` antes de considerar productiva la integración.
- Mantener credenciales y valores reales fuera de Git.
- No registrar contraseñas, cadenas de conexión ni conjuntos de datos completos.
- Ejecutar el servicio Windows con el mínimo privilegio viable.

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

## Secretos y configuración

- Los `appsettings*.json` versionados contienen únicamente placeholders.
- En producción, la cadena de conexión debe inyectarse mediante configuración
  externa, variables de entorno o un archivo protegido y no versionado.
- La cuenta del servicio debe tener permiso de lectura solamente sobre la fuente
  de configuración necesaria.

## Operación

- Mantener timeout de conexión y de comando.
- Aplicar límites de filas, tamaño de respuesta y paginación.
- Registrar inicio, fin, duración, tipo de extracción y cantidad de registros.
- No registrar el cuerpo completo de las respuestas.
- Diferenciar proceso vivo de disponibilidad de SQL Server.
- Definir reintentos en Ubuntu con espera progresiva; la API no debe reintentar
  consultas largas de manera invisible.

## Observación sobre firewall

La defensa principal debe ser enlazar el proceso a localhost. Las reglas de
firewall son complementarias y deben probarse en el servidor real; no debe
asumirse que una regla `allow` prevalecerá sobre un bloqueo explícito general.
