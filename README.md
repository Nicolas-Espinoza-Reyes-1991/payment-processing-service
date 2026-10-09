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
| `POST` | `/payments` | Crea una solicitud de pago. Requiere header `Idempotency-Key`. |
| `GET` | `/payments/{id}` | Consulta una transacción por su id. |
| `GET` | `/payments?merchant_id=&status=` | Busca transacciones, con filtros opcionales combinables. |

### Ejemplo — crear un pago

**Request:**
```http
POST /payments
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

Razonamiento completo de cada una, con alternativas consideradas y por qué se descartaron, en
[`DECISIONES.md`](./DECISIONES.md).

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
herramienta externa.

---

## Supuestos

- Las credenciales de PostgreSQL en `docker-compose.yml` y `appsettings.Development.json` están
  en texto plano, por tratarse de un entorno de desarrollo local con datos ficticios. En
  producción se gestionarían con `dotnet user-secrets` o un gestor de secretos externo
  (Azure Key Vault, AWS Secrets Manager).
- El Acquirer Mock simula: aprobación por defecto, rechazo si el monto supera $1.000.000
  (código `"51"`, fondos insuficientes), y un timeout simulado si la tarjeta termina en `9999`
  (código de prueba determinístico, para poder reproducir el escenario de error a demanda).
- No se implementó autenticación/autorización en los endpoints, al no estar especificada en el
  requerimiento — se asume que este servicio correría detrás de un gateway/API Manager en un
  entorno real.
- Los montos se validan como mayores a cero; no se definió un monto mínimo específico más allá
  de esa validación básica.

---

## Fuera de alcance / mejoras para un entorno de producción

Decisiones de alcance conscientes para mantener el desafío dentro de una ventana de desarrollo
razonable, documentadas explícitamente para distinguir "no implementado por decisión" de "no
contemplado":

- **Gestión de secretos**: migrar credenciales de `appsettings`/`docker-compose` a un gestor
  externo (Key Vault, Secrets Manager, variables de entorno inyectadas en el pipeline de CI/CD).
- **Autenticación y autorización**: agregar un esquema de autenticación (API Key, OAuth2/JWT)
  para los endpoints, actualmente abiertos.
- **Resiliencia más sofisticada**: si el volumen de transacciones lo justificara, reemplazar el
  bucle de reintentos manual por una librería como Polly (circuit breaker, jitter, fallback).
- **Observabilidad centralizada**: exportar los logs estructurados a un backend como Seq,
  Application Insights o el stack ELK, en vez de solo consola.
- **Rate limiting** en los endpoints públicos.
- **CI/CD**: pipeline automatizado de build, test y despliegue (GitHub Actions u otro).
- **Validación de tarjeta más robusta**: algoritmo de Luhn u otra validación de formato, más
  allá del enmascarado de los últimos 4 dígitos ya implementado.

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
├── docs/screenshots/      # Capturas de pruebas reales
├── docker-compose.yml
├── REQUERIMIENTOS.md      # Enunciado original del desafío
├── DECISIONES.md          # ADRs — razonamiento de cada decisión técnica
├── CONCEPTOS.md           # Glosario técnico de apoyo
├── PROGRESO.md            # Bitácora de desarrollo
└── PRUEBAS.md             # Evidencia de pruebas end-to-end
```
