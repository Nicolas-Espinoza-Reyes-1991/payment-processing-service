# Pruebas end-to-end — Payment Processing Service

> Evidencia de pruebas manuales realizadas contra el backend corriendo en vivo
> (`dotnet run`), con PostgreSQL real levantado vía Docker. Complementa el checklist de
> `PROGRESO.md` con el detalle de cada prueba: request, response y verificación en base de
> datos.

Fecha de las pruebas: 2026-10-09
Servidor: `http://localhost:5165` (puerto asignado automáticamente por Kestrel)

> **Nota sobre el header `X-Api-Key`:** las secciones 1-12 se capturaron **antes** de agregar
> el control de autenticación (ver sección 13 y ADR-011), por eso sus ejemplos de request no
> incluyen el header `X-Api-Key` — en ese momento la API todavía no lo exigía. Desde la
> sección 13 en adelante, y en el estado actual del proyecto, **todos** los endpoints de
> `/payments` requieren ese header (ver README, sección "Autenticación").

---

## 1. Swagger UI — documentación viva de la API

Al levantar el servidor con `dotnet run --project src/Api` y abrir `/swagger`, ASP.NET Core
genera automáticamente la interfaz con los 3 endpoints implementados.

![Swagger UI mostrando los endpoints POST /payments, GET /payments y GET /payments/{id}](docs/screenshots/swagger-ui.jpg)

---

## 2. `POST /payments` — creación de un pago aprobado

Request armado y ejecutado desde Swagger (captura del `curl` equivalente generado
automáticamente, con el request body y el header `Idempotency-Key`):

![Request POST /payments con header Idempotency-Key y body JSON](docs/screenshots/post-payments-request.jpg)

**Request:**
```http
POST /payments
Idempotency-Key: test-key-001
Content-Type: application/json

{
  "merchantId": "merchant-001",
  "amount": 15000,
  "currency": "CLP",
  "cardNumber": "4111111111111234",
  "cardBrand": "Visa"
}
```

**Response — `201 Created`:**
```json
{
  "transactionId": "858f4e07-913b-49e4-be81-f1eb604b9d62",
  "status": 2,
  "correlationId": "50b11f51-6503-4f59-b590-c929bd74ff6f",
  "acquirerResponseCode": "00",
  "acquirerMessage": "Transacción aprobada (simulado por Acquirer Mock)"
}
```

**Header de respuesta `Location`:**
```
http://localhost:5165/payments/858f4e07-913b-49e4-be81-f1eb604b9d62
```

**Resultado:** `status: 2` corresponde a `Approved` en el enum `TransactionStatus`
(`Pending=0, Processing=1, Approved=2, Declined=3, Failed=4`) — confirma que el flujo completo
`Pending → Processing → Approved` se ejecutó correctamente.

---

## 3. `GET /payments/{id}` — consulta de la transacción persistida

**Request:** `GET /payments/858f4e07-913b-49e4-be81-f1eb604b9d62`

**Response — `200 OK`:**
```json
{
  "transactionId": "858f4e07-913b-49e4-be81-f1eb604b9d62",
  "merchantId": "merchant-001",
  "amount": 15000.00,
  "currency": "CLP",
  "cardLast4": "1234",
  "cardBrand": "Visa",
  "status": 2,
  "correlationId": "50b11f51-6503-4f59-b590-c929bd74ff6f",
  "createdAt": "2026-10-09T06:55:46.687101Z",
  "updatedAt": "2026-10-09T06:55:47.242897Z",
  "acquirerResponseCode": "00",
  "acquirerMessage": "Transacción aprobada (simulado por Acquirer Mock)"
}
```

**Resultado clave:** `cardLast4: "1234"` confirma que solo se guardaron los últimos 4 dígitos
de `"4111111111111234"` — el número completo nunca se persistió (ver ADR-006 en
`DECISIONES.md`).

---

## 4. Idempotencia — reenvío de la misma petición

Se repitió **exactamente la misma petición** (mismo `Idempotency-Key: test-key-001`, mismo
body) una segunda vez.

**Response — `201 Created` (idéntica a la primera):**
```json
{
  "transactionId": "858f4e07-913b-49e4-be81-f1eb604b9d62",
  "status": 2,
  "correlationId": "50b11f51-6503-4f59-b590-c929bd74ff6f",
  "acquirerResponseCode": "00",
  "acquirerMessage": "Transacción aprobada (simulado por Acquirer Mock)"
}
```

**Verificación directa en PostgreSQL** (sin pasar por la API, para descartar cualquier caché):
```bash
docker exec payment-processing-postgres psql -U payment_user -d payment_processing \
  -c "SELECT \"Id\", \"MerchantId\", \"Amount\", \"Status\", \"IdempotencyKey\" FROM transactions;"
```
```
                  Id                  |  MerchantId  |  Amount  |  Status  | IdempotencyKey
--------------------------------------+--------------+----------+----------+----------------
 858f4e07-913b-49e4-be81-f1eb604b9d62 | merchant-001 | 15000.00 | Approved | test-key-001
(1 row)
```

**Resultado:** mismo `transactionId` devuelto, **una sola fila** en la base de datos — la
idempotencia (ADR-007) funciona correctamente tanto a nivel de lógica de aplicación como de
constraint `UNIQUE` en la base.

---

## 5. `POST /payments` — rechazo por regla de negocio (monto máximo)

**Request:**
```http
POST /payments
Idempotency-Key: test-key-002
Content-Type: application/json

{
  "merchantId": "merchant-002",
  "amount": 2000000,
  "currency": "CLP",
  "cardNumber": "4111111111115678",
  "cardBrand": "Mastercard"
}
```

**Response — `201 Created`:**
```json
{
  "transactionId": "57829adb-c941-4330-83d7-41602f9638ce",
  "status": 3,
  "correlationId": "39d353ba-df1c-484c-8b1c-74fb2dd3643f",
  "acquirerResponseCode": "51",
  "acquirerMessage": "Fondos insuficientes (simulado por Acquirer Mock)"
}
```

**Resultado:** `status: 3` = `Declined`. El monto ($2.000.000) superó el límite simulado en
`AcquirerMockClient` ($1.000.000), devolviendo el código ISO 8583 `"51"` (fondos insuficientes).

---

## 6. `GET /payments?merchant_id=...&status=...` — búsqueda con filtros

**Request:** `GET /payments?merchant_id=merchant-002&status=Declined`

**Response — `200 OK`:**
```json
[
  {
    "transactionId": "57829adb-c941-4330-83d7-41602f9638ce",
    "merchantId": "merchant-002",
    "amount": 2000000,
    "currency": "CLP",
    "cardLast4": "5678",
    "cardBrand": "Mastercard",
    "status": 3,
    "correlationId": "39d353ba-df1c-484c-8b1c-74fb2dd3643f",
    "createdAt": "2026-10-09T06:56:35.945702Z",
    "updatedAt": "2026-10-09T06:56:36.105454Z",
    "acquirerResponseCode": "51",
    "acquirerMessage": "Fondos insuficientes (simulado por Acquirer Mock)"
  }
]
```

**Resultado:** el filtro combinado por `merchant_id` y `status` devuelve exactamente la
transacción esperada.

---

## 7. Validaciones de error

### 7.1 — `POST /payments` sin el header `Idempotency-Key`

**Response — `400 Bad Request`:**
```json
{ "error": "El header Idempotency-Key es obligatorio." }
```

### 7.2 — `GET /payments?status=NoExiste` (valor de estado inválido)

**Response — `400 Bad Request`:**
```json
{ "error": "Estado inválido: NoExiste" }
```

---

## 8. Manejo de errores temporales del adquirente — reintentos y estado `Failed`

Se usó la tarjeta terminada en `9999` (disparador determinístico configurado en
`AcquirerMockClient`, ver ADR-008 en `DECISIONES.md`) para simular que el adquirente no
responde a tiempo.

**Request:**
```http
POST /payments
Idempotency-Key: test-timeout-001
Content-Type: application/json

{
  "merchantId": "merchant-003",
  "amount": 5000,
  "currency": "CLP",
  "cardNumber": "4111111111119999",
  "cardBrand": "Visa"
}
```

**Response — `201 Created`:**
```json
{
  "transactionId": "23834103-537e-4694-8e74-d4832bf937de",
  "status": 4,
  "correlationId": "a0a1d8c1-d63d-4509-a52d-d2e96978d1ae",
  "acquirerResponseCode": null,
  "acquirerMessage": "Acquirer Mock: tiempo de espera agotado (simulado)."
}
```

`status: 4` = `Failed` — tras agotar los 3 reintentos, la transacción queda en un estado final
explícito, nunca ambigua.

**Logs reales del servidor, capturados en vivo durante esta misma petición** (recortados a los
logs propios de la aplicación; se omiten los `info` de SQL generados por EF Core):

```
info: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Transaccion 23834103-537e-4694-8e74-d4832bf937de creada en estado Pending (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae)
info: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Transaccion 23834103-537e-4694-8e74-d4832bf937de pasó a Processing (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae)
warn: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Intento 1/3 fallido contra el adquirente para transaccion 23834103-537e-4694-8e74-d4832bf937de (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae): Acquirer Mock: tiempo de espera agotado (simulado).
warn: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Intento 2/3 fallido contra el adquirente para transaccion 23834103-537e-4694-8e74-d4832bf937de (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae): Acquirer Mock: tiempo de espera agotado (simulado).
warn: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Intento 3/3 fallido contra el adquirente para transaccion 23834103-537e-4694-8e74-d4832bf937de (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae): Acquirer Mock: tiempo de espera agotado (simulado).
fail: PaymentProcessingService.Application.UseCases.CreatePaymentUseCase[0]
      Transaccion 23834103-537e-4694-8e74-d4832bf937de marcada como Failed tras 3 intentos fallidos (CorrelationId: a0a1d8c1-d63d-4509-a52d-d2e96978d1ae)
```

**Resultado clave:** se puede seguir el ciclo de vida completo de la transacción
(`Pending → Processing → 3 intentos fallidos → Failed`) filtrando únicamente por el
`CorrelationId` — exactamente la trazabilidad que pide el PDF, demostrada con logs reales, no
solo en teoría.

---

## 9. Tests unitarios automatizados

```bash
dotnet test
```
```
Correctas! - Con error: 0, Superado: 10, Omitido: 0, Total: 10 - PaymentProcessingService.Domain.Tests.dll
Correctas! - Con error: 0, Superado: 4, Omitido: 0, Total: 4 - PaymentProcessingService.Application.Tests.dll
```

14/14 tests pasando (10 de reglas de negocio en Domain, 4 del flujo completo en Application,
incluyendo idempotencia y reintentos simulados sin PostgreSQL ni HTTP real).

---

## 10. Frontend Angular — consumo real de la API

Levantado con `ng serve` (puerto 4200) en paralelo al backend (puerto 5165), con CORS
configurado entre ambos. La vista consulta `GET /payments` en vivo y muestra las transacciones
creadas durante las pruebas anteriores (incluida la que falló por timeout simulado).

![Vista Angular mostrando la tabla de transacciones con estados coloreados](docs/screenshots/angular-frontend.jpg)

**Prueba de filtro:** al seleccionar el estado `Declined` y buscar, la tabla se actualiza
mostrando únicamente la transacción con ese estado (`merchant-002`, $2.000.000, rechazada) —
confirma que el filtro `GET /payments?status=Declined` funciona correctamente desde la UI, no
solo desde Swagger/curl.

---

## 11. Frontend Angular — creación de pago desde el modal (formulario + validación)

Arquitectura final en 3 componentes (`TransactionListComponent`, `PaymentFormModalComponent`,
`ToastComponent`) + `ToastService`, ver ADR-010 en `DECISIONES.md`.

**Validación client-side:** al intentar confirmar el formulario con el campo "Comercio" vacío,
el envío se bloquea, el campo se marca en rojo y aparece el mensaje de error — sin ninguna
petición HTTP disparada:

![Validación del formulario mostrando el error en el campo Comercio vacío](docs/screenshots/angular-modal-validation.jpg)

**Creación exitosa:** completando el formulario correctamente y confirmando, se envía
`POST /payments` con un `Idempotency-Key` generado automáticamente (`crypto.randomUUID()`), el
modal se cierra, aparece una notificación toast con el resultado real del backend, y la tabla
se refresca sola mostrando la nueva transacción:

![Toast de confirmación tras crear un pago exitosamente, con la tabla actualizada](docs/screenshots/angular-toast-success.jpg)

**Resultado:** el frontend no solo consulta datos — ejecuta el flujo completo de creación de un
pago contra el backend real (validación client-side + HTTP + persistencia en PostgreSQL +
actualización de la UI), con la misma arquitectura de componentes separados por responsabilidad
usada conceptualmente en el backend.

---

## 12. Hallazgo de revisión final — código de estado incorrecto en validaciones de negocio

Durante la revisión de seguridad previa a la entrega, se detectó que las validaciones de
`Transaction.Create()` (ej. monto inválido, `merchant_id` vacío) lanzaban una `ArgumentException`
que nadie capturaba entre Domain y el controller — el middleware global de excepciones la
trataba como un error interno genérico, devolviendo `500` en vez de `400`.

**No era una falla de la validación en sí** (la transacción inválida nunca se creaba), sino del
código de estado HTTP comunicado al cliente — semánticamente incorrecto para un error de datos
de entrada.

**Antes de la corrección:**
```json
POST /payments  { "amount": -500, ... }
→ 500 { "error": "Ocurrió un error interno inesperado. Contacte a soporte si el problema persiste." }
```

**Corrección aplicada:** se agregó un `catch (ArgumentException ex)` en `PaymentsController.Create`,
traduciendo las excepciones de validación del dominio a `400 Bad Request` con el mensaje
específico, en el límite HTTP (responsabilidad de la capa Api, no de Domain/Application).

**Después de la corrección, probado en vivo:**
```json
POST /payments  { "amount": -500, ... }
→ 400 { "error": "El monto debe ser mayor a cero. (Parameter 'amount')" }

POST /payments  { "merchantId": "", ... }
→ 400 { "error": "merchant_id es obligatorio. (Parameter 'merchantId')" }

POST /payments  { "merchantId": "merchant-001", "amount": 5000, ... }  (caso válido)
→ 201 Created  (sin regresión)
```

Se confirmó además que los 14 tests unitarios (`dotnet test`) siguen pasando sin regresiones
tras el cambio.

---

## 13. Revisión de seguridad final — 4 hallazgos adicionales y correcciones

Continuación de la revisión de seguridad (sección 12), con 4 hallazgos más — ver razonamiento
completo en ADR-011 de `DECISIONES.md`.

### 13.1 — API Key agregada y probada en vivo

```
GET /payments                                           (sin header)  → 401
GET /payments  X-Api-Key: clave-incorrecta               (clave mala)  → 401
GET /payments  X-Api-Key: haulmer-demo-api-key-2026      (clave OK)    → 200
```

Swagger configurado con esquema de seguridad (botón "Authorize"); el frontend Angular adjunta
la clave automáticamente vía `apiKeyInterceptor`, verificado cargando la tabla de transacciones
end-to-end con la clave aplicada.

### 13.2 — Validación de largo agregada al Domain (mismo patrón del hallazgo anterior)

```
POST /payments  { "merchantId": "x".repeat(200), ... }
→ Antes:   500 "Ocurrió un error interno inesperado..."
→ Después: 400 "merchant_id no puede superar los 100 caracteres. (Parameter 'merchantId')"
```

Se agregaron 4 tests nuevos en `Domain.Tests` (`merchantId` largo, moneda de largo inválido,
`idempotencyKey` larga) — de 14 a **18 tests totales**, todos pasando.

### 13.3 — Paquetes NuGet vulnerables actualizados

```bash
dotnet list package --vulnerable --include-transitive
```
**Antes:** `System.Net.Http` y `System.Text.RegularExpressions` 4.3.0 (severidad "High"),
transitivos en los proyectos de test.
**Después:** actualizados `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`,
`coverlet.collector` a sus últimas versiones compatibles con .NET 8 — confirmado: **0 paquetes
vulnerables en los 6 proyectos** de la solución. `npm audit` del frontend: 0 vulnerabilidades
desde el principio.

### 13.4 — Resiliencia de conexión y health check

- `EnableRetryOnFailure(maxRetryCount: 3)` agregado a la configuración de `UseNpgsql`.
- `GET /health` agregado, probado sin header de autenticación:
```
GET /health → 200 "Healthy"
```

### Verificación final

```bash
dotnet build   # 0 errores
dotnet test    # 18/18 pasando
```

---

## Conclusión

Las 13 secciones de prueba confirman que el sistema funciona **de punta a punta, con datos
reales en PostgreSQL y un frontend real que tanto consulta como crea transacciones**: creación,
consulta, idempotencia, reglas de negocio, filtros, validaciones de error (backend y frontend),
manejo de errores temporales con reintentos, trazabilidad completa por logs, 18 pruebas
unitarias automatizadas, una interfaz Angular funcional con arquitectura de componentes
separados, y una revisión de seguridad final que detectó y corrigió 5 hallazgos reales antes de
la entrega (autenticación, validación de datos, dependencias vulnerables, resiliencia de
conexión, monitoreo). Checklist detallado de requerimientos cubiertos: ver secciones 3 y 4 de
`PROGRESO.md`.
