# Desafío Técnico — Payment Processing Service

> Documento de requerimiento inicial. **Este archivo no se modifica** a lo largo del desarrollo;
> es la fuente de verdad de lo solicitado por Haulmer. El seguimiento de avances, decisiones de
> arquitectura y stack utilizado se documenta por separado en [`PROGRESO.md`](./PROGRESO.md).

Fuente: `Desafio_Tecnico_Haulmer_retail.pdf` (proceso de selección Haulmer).

**Duración sugerida:** 72 horas desde la recepción del documento.

---

## Nuestro stack (sugerido por Haulmer)

| Capa | Stack |
|---|---|
| Backend | PHP con Laravel, o **.NET (C#)** — preferencia explícita de Haulmer por .NET |
| Base de datos | Relacional (MySQL, PostgreSQL o SQL Server) |
| Pruebas | xUnit o NUnit (.NET) / Pest o PHPUnit (PHP) |
| Infraestructura | Docker y Git |
| Frontend (opcional) | Angular 17+ con TypeScript, vista simple para consultar transacciones |

> Se permite el uso de asistentes de IA, documentando en el README para qué se usaron.

---

## Contexto del problema

Desarrollar un componente dentro de una plataforma de pagos: un **Payment Processing Service**,
responsable de:

- Recibir solicitudes de pago desde múltiples comercios.
- Comunicarse con un adquirente externo (simulado) para autorizar o rechazar transacciones.
- Validar solicitudes y aplicar reglas de negocio.
- Registrar el estado de cada transacción.
- Permitir trazabilidad completa del flujo de procesamiento.

## Diseño de sistema y arquitectura de alto nivel

Se espera una propuesta de arquitectura de alto nivel (diagrama simple, imagen en el README, o
explicación escrita con esquema básico) que incluya:

- Punto de entrada del servicio.
- Validaciones.
- Procesamiento de pagos.
- Integración con adquirente.
- Persistencia de transacciones.
- Mecanismos de trazabilidad/observabilidad.
- Flujo de la transacción desde que el comercio envía la solicitud hasta la respuesta final.

No se busca una respuesta única; interesa el razonamiento, la separación de responsabilidades y
las prioridades de diseño.

## Requerimientos funcionales

- Exponer un endpoint `POST /payments` para recibir solicitudes de pago.
- El endpoint debe recibir: `merchant_id`, monto, moneda y datos de tarjeta.
- Validar los datos de entrada antes de procesar la transacción.
- Crear una nueva transacción con un estado inicial.
- Aplicar reglas de negocio básicas (ej. monto máximo, validación de tarjeta).
- Enviar la transacción a un servicio **Acquirer Mock** que simule la autorización del pago.
- Actualizar el estado final de la transacción según la respuesta del adquirente.

### Estados de transacción sugeridos

`PENDING` → `PROCESSING` → `APPROVED` / `DECLINED` / `FAILED`

### Consulta de transacciones

- `GET /payments/{transaction_id}`
- `GET /payments?merchant_id=...&status=...`

## Requerimientos no funcionales

- Persistir las transacciones en una base de datos.
- Diseñar el modelo de datos considerando trazabilidad de las operaciones.
- Manejar adecuadamente errores de validación y fallos del sistema.
- Evitar duplicidad de transacciones mediante algún mecanismo de **idempotencia**.
- Considerar escenarios donde el adquirente responda con errores temporales.
- Incluir logs que permitan seguir el flujo completo de una transacción.
- Permitir correlacionar eventos o acciones relacionadas con una misma transacción
  (correlation id / trace id).

## Entregables esperados

- Repositorio con el código fuente de la solución.
- Archivo `README.md` explicando la arquitectura y las decisiones técnicas tomadas.
- Instrucciones claras para ejecutar el proyecto localmente.
- Descripción de los supuestos realizados durante el desarrollo.
- Opcional: `Dockerfile` o `docker-compose` para facilitar la ejecución.
- Opcional: pruebas unitarias o de reglas de negocio.

Tras la entrega se coordina una reunión de ~35 minutos para explicar la solución y las decisiones
tomadas.
