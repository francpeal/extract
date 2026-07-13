# Protocolo para sesiones de trabajo

## Inicio de cada sesión

1. Leer `README.md` y `AGENTS.md`.
2. Leer `docs/PROJECT.md`, `docs/ARCHITECTURE.md` y `docs/ROADMAP.md`.
3. Leer `docs/API.md`, `docs/SECURITY.md` o `WinBridgeApi/DEPLOY.md` según la tarea.
4. Revisar `docs/DECISIONS.md` antes de proponer cambios estructurales.
5. Revisar el estado de Git sin asumir que cambios existentes pueden descartarse.

## Formato recomendado de una sesión

### 1. Objetivo

Definir un resultado comprobable y acotado para la sesión.

### 2. Contexto confirmado

Enumerar datos comprobados y separar explícitamente supuestos o preguntas.

### 3. Plan

Dividir el trabajo en pasos verificables. No implementar decisiones funcionales
pendientes como si estuvieran confirmadas.

### 4. Ejecución y verificación

Implementar el alcance acordado y ejecutar las comprobaciones adecuadas. No usar
el ERP productivo para pruebas destructivas.

### 5. Cierre

Informar:

- resultado alcanzado;
- archivos modificados;
- pruebas ejecutadas y su resultado;
- decisiones registradas;
- riesgos, bloqueos y siguiente paso recomendado.

## Mantenimiento documental

| Si cambia... | Actualizar... |
|---|---|
| Objetivo, alcance o restricción | `PROJECT.md` |
| Componentes, responsabilidades o despliegue | `ARCHITECTURE.md` |
| Endpoint, DTO, error o versión | `API.md` |
| Límite de confianza o control operativo | `SECURITY.md` |
| Decisión con alternativas y consecuencias | `DECISIONS.md` |
| Tarea completada, bloqueo o siguiente hito | `ROADMAP.md` |
| Procedimiento de instalación | `WinBridgeApi/DEPLOY.md` |
| Operación o migración del servidor Linux | `PROCEDURES.md` |

## Criterio de información

Usar estas etiquetas cuando ayuden a evitar ambigüedad:

- **Confirmado:** existe evidencia en código, entorno o información del propietario.
- **Decidido:** se eligió una alternativa y está registrada.
- **Supuesto:** permite avanzar, pero necesita validación.
- **Pendiente:** no debe implementarse aún sin definición.

No usar este documento como bitácora cronológica. El estado vivo pertenece a
`ROADMAP.md` y las decisiones duraderas a `DECISIONS.md`.
