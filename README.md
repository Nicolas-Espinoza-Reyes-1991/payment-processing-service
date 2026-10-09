# Payment Processing Service

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791) ![Docker](https://img.shields.io/badge/Docker-Compose-2496ED)

Servicio de procesamiento de pagos desarrollado como desafío técnico para Haulmer. Recibe
solicitudes de pago desde comercios, aplica reglas de negocio, se comunica con un adquirente
(simulado mediante un Acquirer Mock) y expone el estado de cada transacción con trazabilidad
completa del flujo.

> Requerimiento original: [`REQUERIMIENTOS.md`](./REQUERIMIENTOS.md)
> Razonamiento detrás de cada decisión técnica: [`DECISIONES.md`](./DECISIONES.md)
> Evidencia de pruebas end-to-end (requests, responses y logs reales): [`PRUEBAS.md`](./PRUEBAS.md)
> Bitácora de desarrollo: [`PROGRESO.md`](./PROGRESO.md)

---

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 8 — ASP.NET Core Web API (C#) |
| Base de datos | PostgreSQL 16 |
| ORM | Entity Framework Core 8 |
| Pruebas | xUnit |
| Infraestructura | Docker / Docker Compose |
| Documentación de API | Swagger / OpenAPI |
| Frontend (opcional) | Angular 21.2.0 (TypeScript, componentes standalone) |

---

## Arquitectura de alto nivel

Diseñé la solución con **Clean Architecture simplificada, en capas**, priorizando separación de
responsabilidades y testabilidad por sobre patrones de mayor complejidad (hexagonal puro,
microservicios) que no se justifican para el alcance de este desafío. El razonamiento completo
de esta decisión, incluyendo por qué descarté las alternativas, está en el ADR-003 de
[`DECISIONES.md`](./DECISIONES.md).

```
                    ┌─────────────┐
                    │     Api     │   Controllers, DTOs HTTP, Swagger
                    └──────┬──────┘
                           │ depende de
                 ┌─────────┴─────────┐
                 ▼                   ▼
         ┌───────────────┐   ┌────────────────┐
         │  Application   │   │ Infrastructure │
         │ Casos de uso,  │──▶│ EF Core,       │
         │ interfaces     │   │ PostgreSQL,    │
         │ (contratos)    │   │ Acquirer Mock  │
         └───────┬────────┘   └───────┬────────┘
                  │ depende de         │ depende de
                  └──────────┬─────────┘
                             ▼
                      ┌─────────────┐
                      │   Domain    │   Entidad Transaction,
                      │  (no depende │   estados, reglas de negocio
                      │   de nadie) │
                      └─────────────┘
```

**Punto clave:** `Domain` no depende de ningún framework externo (ni EF Core ni ASP.NET) — las
reglas de negocio se validan y testean de forma aislada. `Application` solo conoce interfaces
(`ITransactionRepository`, `IAcquirerClient`), nunca implementaciones concretas — es el
principio de Inversión de Dependencias (la "D" de SOLID) aplicado de forma literal.

### Flujo de una transacción — `POST /payments`

1. El comercio envía la solicitud con un header `Idempotency-Key`.
2. **Application** verifica primero si ya existe una transacción con esa clave (idempotencia) —
   si existe, la devuelve sin crear una nueva.
3. Si no existe, **Domain** valida los datos y crea la transacción en estado `Pending`.
4. Se persiste en PostgreSQL y pasa a `Processing`.
5. **Application** llama al **Acquirer Mock** (en Infrastructure) para autorizar el pago, con
   hasta 3 reintentos ante errores temporales (ver ADR-008).
6. Según la respuesta, la transacción pasa a `Approved`, `Declined`, o `Failed` si se agotan los
   reintentos.
7. Se persiste el estado final y se responde al comercio con `201 Created`.

Cada paso queda registrado en logs estructurados, correlacionados por un `CorrelationId` único
por transacción — permite reconstruir el flujo completo de una transacción específica filtrando
por ese identificador. Evidencia real de este flujo completo, incluyendo el escenario de error
temporal con reintentos, en la sección 8 de [`PRUEBAS.md`](./PRUEBAS.md).

---

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/payments` | Crea una solicitud de pago. Requiere headers `X-Api-Key` e `Idempotency-Key`. |
| `GET` | `/payments/{id}` | Consulta una transacción por su id. Requiere `X-Api-Key`. |
| `GET` | `/payments?merchant_id=&status=` | Busca transacciones, con filtros opcionales combinables. Requiere `X-Api-Key`. |
| `GET` | `/health` | Health check — sin autenticación, para monitoreo/orquestación. |

Todos los endpoints de `/payments` requieren el header `X-Api-Key` (ver sección
[Autenticación](#autenticación)).

### Ejemplo — crear un pago

**Request:**
```http
POST /payments
X-Api-Key: haulmer-demo-api-key-2026
Idempotency-Key: order-00123
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

`status` es el enum `TransactionStatus`: `0=Pending, 1=Processing, 2=Approved, 3=Declined,
4=Failed`. Más ejemplos (rechazo por monto, error temporal del adquirente, validaciones,
búsqueda con filtros) en [`PRUEBAS.md`](./PRUEBAS.md).

---

## Decisiones técnicas destacadas

| Decisión | Resumen | Detalle |
|---|---|---|
| Arquitectura en capas, no hexagonal ni microservicios | El alcance del desafío no justifica la complejidad adicional de esos patrones | ADR-003 |
| No se persiste el número completo de tarjeta | Solo se guardan los últimos 4 dígitos y la marca — norma de la industria (PCI-DSS) | ADR-006 |
| Idempotencia vía header `Idempotency-Key` | Mismo patrón que usan adquirentes reales (ej. Stripe); reforzado con constraint `UNIQUE` en base de datos | ADR-007 |
| Reintentos manuales en vez de una librería como Polly | El requerimiento se cubre con ~10 líneas; no se suma una dependencia nueva para el alcance actual | ADR-008 |
| Logging nativo (`ILogger`) en vez de Serilog | Suficiente para trazabilidad en consola durante la demo; sin dependencias adicionales | ADR-009 |
| `Status` guardado como texto en PostgreSQL, no como entero | Legible directamente en una consulta SQL, sin recordar la equivalencia número-estado | ADR-006 |
| Frontend en componentes separados por responsabilidad, sin NgRx | Mismo principio Single Responsibility del backend; un gestor de estado global sería sobre-ingeniería para el alcance actual | ADR-010 |
| API Key en vez de OAuth2/JWT completo | Control mínimo razonable para no dejar la API abierta; un esquema de usuarios completo es alcance mucho mayor | ADR-011 |

Razonamiento completo de cada una, con alternativas consideradas y por qué se descartaron, en
[`DECISIONES.md`](./DECISIONES.md).

---

## Autenticación

Todos los endpoints de `/payments` requieren el header `X-Api-Key` con el valor configurado en
`appsettings.Development.json` (`Security:ApiKey`, por defecto `haulmer-demo-api-key-2026` para
este entorno de desarrollo). Sin ese header, o con un valor incorrecto, la API responde
`401 Unauthorized`. El endpoint `/health` está exento, para permitir monitoreo sin autenticación.

En Swagger, usá el botón **"Authorize"** (arriba a la derecha) para cargar la clave una sola vez
y que se adjunte automáticamente en cada prueba. El frontend Angular la adjunta solo, vía un
interceptor HTTP (`apiKeyInterceptor`).

Es un control básico, apropiado para este desafío — en producción real correspondería una API
Key por comercio (guardada hasheada en base de datos) o un esquema más robusto como OAuth2/JWT.
Detalle completo en ADR-011 de [`DECISIONES.md`](./DECISIONES.md).

---

## Flujo de trabajo con Git

Desarrollé el proyecto con un flujo de ramas por funcionalidad e integración vía Pull Request,
simulando el proceso de un equipo real aunque el desarrollo fue individual:

- **Una rama por bloque de trabajo** (`feature/application-contracts`,
  `feature/infrastructure-persistence`, `feature/api-endpoints`,
  `feature/error-handling-logging`, `feature/readme-architecture`, `feature/unit-tests`, entre
  otras), nunca commits directos sobre `main` salvo el scaffolding inicial.
- **Múltiples Pull Requests** mergeados a `main`, uno por cada bloque de trabajo, cada uno con
  su propia descripción explicando qué incluye, qué decisiones de diseño conlleva y qué queda
  pendiente para el siguiente — [ver el historial completo en
  GitHub](https://github.com/Nicolas-Espinoza-Reyes-1991/payment-processing-service/pulls?q=is%3Apr+is%3Aclosed).
- **Mensajes de commit con [Conventional Commits](https://www.conventionalcommits.org/)**:
  prefijos `feat:`, `fix:`, `docs:`, `chore:`, `test:` según el tipo de cambio, con commits
  atómicos (código y documentación separados en commits distintos dentro de un mismo bloque de
  trabajo).
- Cada rama se compiló y probó (`dotnet build` / `dotnet test` / pruebas manuales en vivo) antes
  de abrir el Pull Request correspondiente.

---

## Cómo ejecutar el proyecto localmente

### Requisitos previos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Herramienta `dotnet-ef` (si no la tenés): `dotnet tool install --global dotnet-ef --version 8.*`

### 1. Clonar el repositorio

```bash
git clone https://github.com/Nicolas-Espinoza-Reyes-1991/payment-processing-service.git
cd payment-processing-service
```

### 2. Levantar PostgreSQL con Docker

```bash
docker compose up -d
```

Esto levanta PostgreSQL 16 en el puerto `5436` del host (no el 5432 estándar, para evitar
conflictos con otras instancias de Postgres que pudieran estar corriendo localmente).

### 3. Aplicar las migraciones de base de datos

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Crea la tabla `transactions` con su estructura completa (columnas, índices, constraint único de
idempotencia).

### 4. Levantar el backend

```bash
dotnet run --project src/Api
```

El servidor queda escuchando en el puerto que indique la consola (ej.
`http://localhost:5165`).

### 5. Probar la API

Abrí `http://localhost:<puerto>/swagger` en el navegador para la interfaz interactiva de
Swagger, desde donde se pueden probar los 3 endpoints sin necesidad de Postman u otra
herramienta externa. Hacé clic en **"Authorize"** y pegá la API Key
(`haulmer-demo-api-key-2026`, configurada en `appsettings.Development.json`) antes de probar
cualquier endpoint de `/payments` — ver sección [Autenticación](#autenticación).

### 6. (Opcional) Levantar el frontend Angular

Con el backend corriendo (paso 4), en **otra terminal**:

```bash
cd frontend
npm install
ng serve
```

Abrí `http://localhost:4200` para la vista de consulta de transacciones, con filtros por
`merchant_id` y `status`, y un botón **"+ Nuevo pago"** que abre un modal para crear una
transacción real contra el backend (con validación de campos en el propio formulario). Requiere
que el backend esté corriendo en `http://localhost:5165` (CORS ya configurado en `Program.cs`
para aceptar ese origen).

**Arquitectura del frontend:** componentes separados por responsabilidad
(`TransactionListComponent` para la tabla/filtros, `PaymentFormModalComponent` para el
formulario de creación, `ToastComponent` + `ToastService` para notificaciones), con el
componente raíz (`App`) como orquestador liviano — mismo principio de separación de
responsabilidades aplicado en el backend. Detalle completo en ADR-010 de
[`DECISIONES.md`](./DECISIONES.md). Diseño visual inspirado en la paleta e identidad
tipográfica reales de Haulmer.

---

## Guía rápida de pruebas (para quien evalúe el proyecto)

Con el backend corriendo (ver sección anterior), esta tabla cubre los escenarios principales
del desafío — podés probarlos directamente desde Swagger (`http://localhost:<puerto>/swagger`).

**Antes que nada:** hacé clic en **"Authorize"** (arriba a la derecha de Swagger) y pegá:
```
haulmer-demo-api-key-2026
```

| # | Qué probar | Cómo | Resultado esperado |
|---|---|---|---|
| 1 | Crear un pago aprobado | `POST /payments`, header `Idempotency-Key: test-1`, body con `amount: 1000` | `201 Created`, `status: 2` (Approved) |
| 2 | Crear un pago rechazado | Igual al anterior, con `amount: 2000000` (supera el límite) | `201`, `status: 3` (Declined), `acquirerResponseCode: "51"` |
| 3 | Simular un error temporal del adquirente | Igual al #1, con `cardNumber` terminando en `9999` | `201`, `status: 4` (Failed) — mirá la consola del servidor: vas a ver 3 reintentos (`warn`) antes del `fail` |
| 4 | Idempotencia — repetir una solicitud | Repetir el request #1 con el **mismo** `Idempotency-Key: test-1` y los **mismos** datos | `201`, misma `transactionId` que el #1 — no crea una transacción nueva |
| 5 | Idempotencia — detectar conflicto | Repetir el `Idempotency-Key: test-1`, pero con `amount` **distinto** | `409 Conflict`, con mensaje explicando el conflicto |
| 6 | Validación — monto inválido | `POST /payments` con `amount: -100` | `400 Bad Request` |
| 7 | Validación — comercio vacío | `POST /payments` con `merchantId: ""` | `400 Bad Request` |
| 8 | Falta el header de idempotencia | `POST /payments` sin `Idempotency-Key` | `400 Bad Request` |
| 9 | Sin autenticación | Cualquier request a `/payments` sin `X-Api-Key` (o con el botón "Authorize" sin usar) | `401 Unauthorized` |
| 10 | Consultar una transacción por id | `GET /payments/{id}`, con el `transactionId` de cualquier prueba anterior | `200 OK`, con los datos completos |
| 11 | Buscar con filtros | `GET /payments?merchant_id=merchant-001&status=Approved` | `200 OK`, lista filtrada |
| 12 | Health check (sin autenticación) | `GET /health` | `200 OK`, `"Healthy"` |

Evidencia detallada de cada uno de estos escenarios (requests, responses reales, y capturas),
ya ejecutada por el desarrollador, en [`PRUEBAS.md`](./PRUEBAS.md).

---

## Supuestos

- Las credenciales de PostgreSQL en `docker-compose.yml` y `appsettings.Development.json` están
  en texto plano, por tratarse de un entorno de desarrollo local con datos ficticios. En
  producción se gestionarían con `dotnet user-secrets` o un gestor de secretos externo
  (Azure Key Vault, AWS Secrets Manager).
- El Acquirer Mock simula: aprobación por defecto, rechazo si el monto supera $1.000.000
  (código `"51"`, fondos insuficientes), y un timeout simulado si la tarjeta termina en `9999`
  (código de prueba determinístico, para poder reproducir el escenario de error a demanda).
- El control de autenticación (`X-Api-Key`) usa una clave fija en texto plano
  (`appsettings.Development.json`), apropiada solo para este entorno de demostración — en
  producción se gestionaría como las credenciales de base de datos (secreto externo, no en
  texto plano en el repositorio).
- Los montos se validan como mayores a cero; no se definió un monto mínimo específico más allá
  de esa validación básica.
- El PDF menciona el campo `merchant_id` (snake_case) como ejemplo; el cuerpo JSON del
  `POST /payments` usa `merchantId` (camelCase), la convención por defecto de ASP.NET Core y la
  más común en APIs .NET. El query string de `GET /payments` sí usa `merchant_id` (snake_case)
  literal, tal como lo especifica el PDF para ese endpoint.
- La validación de tarjeta se limita a formato (número presente, longitud razonable) y
  enmascarado de los últimos 4 dígitos — no se implementó el algoritmo de Luhn ni verificación
  de vencimiento/CVV, al no ser parte explícita del requerimiento (ver "Fuera de alcance").

---

## Fuera de alcance / mejoras para un entorno de producción

Decisiones de alcance conscientes para mantener el desafío dentro de una ventana de desarrollo
razonable, documentadas explícitamente para distinguir "no implementado por decisión" de "no
contemplado":

- **Gestión de secretos**: migrar credenciales de `appsettings`/`docker-compose` a un gestor
  externo (Key Vault, Secrets Manager, variables de entorno inyectadas en el pipeline de CI/CD).
- **Autenticación y autorización más robusta**: ya hay un control básico de API Key (ver
  sección "Autenticación" y ADR-011); en producción real correspondería API Keys por comercio
  (hasheadas en base de datos) o un esquema de usuarios con OAuth2/JWT.
- **Resiliencia más sofisticada**: si el volumen de transacciones lo justificara, reemplazar el
  bucle de reintentos manual por una librería como Polly (circuit breaker, jitter, fallback).
- **Observabilidad centralizada**: exportar los logs estructurados a un backend como Seq,
  Application Insights o el stack ELK, en vez de solo consola.
- **Rate limiting** en los endpoints públicos.
- **CI/CD**: pipeline automatizado de build, test y despliegue (GitHub Actions u otro).
- **Validación de tarjeta más robusta**: algoritmo de Luhn u otra validación de formato, más
  allá del enmascarado de los últimos 4 dígitos ya implementado.
- **Procesamiento asíncrono de la llamada al adquirente**: hoy `CreatePaymentUseCase` llama al
  adquirente de forma síncrona, bloqueando la respuesta HTTP hasta tener el resultado final. A
  escala de producción real, esto se desacoplaría con una cola de mensajes (RabbitMQ, AWS SQS):
  el comercio recibiría `202 Accepted` de inmediato, un *worker* en segundo plano procesaría la
  autorización, y el resultado final se notificaría vía webhook — evitando que una llamada lenta
  al adquirente bloquee el hilo HTTP principal. No se implementó porque el alcance y el tiempo
  del desafío no lo requerían, pero es el paso evolutivo natural si el volumen de transacciones
  lo justificara.

---

## Pruebas

Proyectos `tests/Domain.Tests` y `tests/Application.Tests` (xUnit). Evidencia de pruebas
manuales end-to-end contra el backend corriendo en vivo, con PostgreSQL real — requests,
responses, logs y capturas de pantalla reales — en [`PRUEBAS.md`](./PRUEBAS.md).

```bash
dotnet test
```

---

## Uso de asistentes de IA

Utilicé Claude (Anthropic) como asistente de desarrollo durante todo el proceso, bajo un
esquema de trabajo orientado a mantener control total sobre cada decisión mientras optimizaba
los tiempos dentro de la ventana sugerida del desafío:

- **Actué como orquestador y revisor de cada paso**: cada comando, cada archivo de código y
  cada decisión de arquitectura las ejecuté personalmente, tras recibir la explicación del
  "qué" y el "por qué" correspondiente — nunca acepté un cambio sin entenderlo primero.
- **Aceleré la curva de diseño y documentación sin resignar criterio propio**: usé la IA para
  explorar alternativas de arquitectura, generar implementaciones propuestas y mantener la
  documentación al día en paralelo al desarrollo, lo que permitió avanzar más rápido en las
  etapas de diseño y registro sin sacrificar la profundidad de las decisiones tomadas.
- **Cuestioné activamente las decisiones antes de aceptarlas**: varias decisiones de
  arquitectura (estilo de capas vs. hexagonal vs. microservicios, qué librerías usar y por qué,
  alcance de cada funcionalidad) las discutí y cuestioné antes de incorporarlas, con su
  razonamiento documentado en `DECISIONES.md`.
- **Validé cada resultado de forma independiente**: compilé, ejecuté y probé cada cambio en mi
  propia máquina antes de avanzar al siguiente paso (builds, logs, consultas directas a la base
  de datos), con la evidencia completa documentada en `PRUEBAS.md`.

Todo el código del repositorio fue escrito, compilado y probado por mí — el uso de IA se enfocó
en acelerar la exploración de alternativas y la documentación del proceso, no en reemplazar las
decisiones ni la verificación final de cada parte del sistema.

---

## Estructura del repositorio

```
├── src/
│   ├── Api/              # Controllers, DTOs HTTP, Program.cs (composición raíz)
│   ├── Application/      # Casos de uso, interfaces (contratos)
│   ├── Domain/           # Entidad Transaction, reglas de negocio
│   └── Infrastructure/   # EF Core, PostgreSQL, Acquirer Mock
├── tests/
│   ├── Domain.Tests/
│   └── Application.Tests/
├── frontend/              # Angular 21 — consulta y creación de pagos (opcional)
│   └── src/app/
│       ├── components/    # TransactionListComponent, PaymentFormModalComponent, ToastComponent
│       ├── services/      # TransactionsService, ToastService
│       └── interceptors/  # apiKeyInterceptor — adjunta X-Api-Key automáticamente
├── docs/screenshots/      # Capturas de pruebas reales
├── docker-compose.yml
├── REQUERIMIENTOS.md      # Enunciado original del desafío
├── DECISIONES.md          # ADRs — razonamiento de cada decisión técnica
├── CONCEPTOS.md           # Glosario técnico de apoyo
├── PROGRESO.md            # Bitácora de desarrollo
└── PRUEBAS.md             # Evidencia de pruebas end-to-end
```
