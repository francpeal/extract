# Guía de trabajo para agentes

## Lectura obligatoria

Antes de proponer o implementar cambios, leer `README.md` y los documentos de
`docs/` en el orden indicado por el README. Para tareas de despliegue o migración,
leer además `docs/PROCEDURES.md` y `WinBridgeApi/DEPLOY.md`.

## Principios

- Mantener el objetivo limitado a la extracción de información del ERP, empezando
  por precios y stock.
- No asumir nombres de tablas, almacenes, listas de precios, claves o reglas de
  negocio que no estén confirmados.
- Distinguir en la documentación entre estado actual, arquitectura objetivo y
  asuntos pendientes.
- Priorizar consultas de solo lectura y una cuenta SQL con permisos mínimos.
- No ampliar la superficie de red: el acceso previsto es API local más túnel SSH.
- Registrar en `docs/DECISIONS.md` toda decisión que cambie límites, seguridad,
  contrato o despliegue.
- Actualizar `docs/ROADMAP.md` cuando una tarea cambie de estado.

## Cierre de una sesión de implementación

1. Verificar los cambios en proporción a su riesgo.
2. Actualizar el estado y próximos pasos en `docs/ROADMAP.md`.
3. Registrar decisiones nuevas o sustituidas en `docs/DECISIONS.md`.
4. Actualizar el contrato o la arquitectura si el comportamiento cambió.
5. Informar pruebas realizadas, archivos modificados y riesgos pendientes.
