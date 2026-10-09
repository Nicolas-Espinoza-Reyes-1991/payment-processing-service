# Progreso del desafío — Payment Processing Service

> Documento vivo de seguimiento: hitos, checklist y próximos pasos.
> El requerimiento original e inmutable está en [`REQUERIMIENTOS.md`](./REQUERIMIENTOS.md).
> El razonamiento detrás de cada decisión técnica (para defender en la entrevista) está en
> [`DECISIONES.md`](./DECISIONES.md).
> La evidencia de pruebas end-to-end (requests, responses y capturas reales) está en
> [`PRUEBAS.md`](./PRUEBAS.md).

Última actualización: 2026-10-09

---

## 1. Stack definido

| Aspecto | Decisión |
|---|---|
| Backend | .NET 8 — ASP.NET Core Web API (C#) |
| Base de datos | PostgreSQL |
| Pruebas | xUnit |
| Infraestructura | Docker + Docker Compose, Git |
| Frontend (opcional) | Angular 17+ (TypeScript) |
| Arquitectura | Clean Architecture en capas (Api → Application → Domain → Infrastructure) |

> Detalle y justificación de cada punto: ver [`DECISIONES.md`](./DECISIONES.md).

---

## 1.bis Entorno de desarrollo (versiones instaladas)

| Herramienta | Versión | Estado | Notas |
|---|---|---|---|
| OS | Windows 11 Home (10.0.26300) | ✅ | |
| Git | 2.32.0.windows.2 | ✅ Verificado | |
| Docker | 29.2.1 (build a5c7197) | ✅ Verificado | |
| .NET SDK | 8.0.425 (incluye Runtime 8.0.31, ASP.NET Core Runtime 8.0.31) | ✅ Verificado | |
| Node.js | v22.22.0 | ✅ Verificado | |
| npm | 10.9.4 | ✅ Verificado | |
| Angular CLI | 21.2.0 | ✅ Verificado | Ya instalado previamente, supera el mínimo 17+ pedido |
| Angular (framework) | 21.2.0 | ✅ Verificado | Proyecto `frontend/` creado con `ng new`, componentes standalone (sin NgModules) |
| PostgreSQL | 16 (imagen Docker `postgres:16`) | ✅ Verificado | Vía contenedor Docker, puerto host 5436 (5432 ocupado por otros proyectos) |
| dotnet-ef (CLI) | 8.0.31 | ✅ Verificado | Herramienta global para migraciones |

> Esta tabla alimenta directamente la sección "Instrucciones para ejecutar el proyecto
> localmente" del README final.

---

## 2. Diseño de alto nivel

_(diagrama de arquitectura, componentes principales y flujo de la transacción — se agregará aquí
o se referenciará un archivo en `/docs`)_

---

## 3. Checklist de requerimientos funcionales

- [x] `POST /payments` — crear solicitud de pago — probado en vivo, `201 Created`
- [x] Validación de entrada a nivel de entidad (`merchant_id`, monto, moneda) — `Transaction.Create()`
- [x] Creación de transacción con estado inicial (`PENDING`) — entidad `Transaction` en Domain
- [x] Reglas de negocio básicas (monto máximo) — probado: monto > 1.000.000 → `Declined`. _(Falta: validación de tarjeta más allá del enmascarado — ej. algoritmo de Luhn, opcional)_
- [x] Integración con Acquirer Mock — `AcquirerMockClient` implementado, registrado y probado
- [x] Actualización de estado final según respuesta del adquirente — probado en vivo (Approved/Declined)
- [x] `GET /payments/{transaction_id}` — probado en vivo, devuelve la transacción persistida
- [x] `GET /payments?merchant_id=...&status=...` — probado en vivo, filtros funcionando

## 4. Checklist de requerimientos no funcionales

- [x] Persistencia en base de datos relacional — PostgreSQL + EF Core, tabla `transactions` creada y verificada con datos reales
- [x] Modelo de datos con trazabilidad — `CorrelationId`, timestamps, respuesta del adquirente guardada
- [x] Manejo de errores de validación y fallos del sistema — probado: `400` sin `Idempotency-Key`, `400` con `status` inválido, `400` con monto/merchant_id inválido (corregido en revisión final, ver sección 12 de `PRUEBAS.md`), middleware global para excepciones no controladas (`500` genérico, sin exponer detalles internos)
- [x] Idempotencia (evitar transacciones duplicadas) — probado en vivo: misma petición repetida no duplica fila en BD
- [x] Manejo de errores temporales del adquirente (reintentos / timeouts) — probado en vivo: 3 reintentos con backoff, transacción pasa a `Failed` tras agotarlos (ver `PRUEBAS.md`)
- [x] Logging estructurado del flujo completo — `ILogger` nativo, ciclo de vida completo visible en logs reales filtrando por `CorrelationId`
- [x] Correlation ID / Trace ID por transacción — campo `CorrelationId` en `Transaction`, generado en `Create()`, visible en las respuestas

## 5. Entregables

- [x] Código fuente en repositorio Git — GitHub, 6 PRs mergeados a `main`
- [x] `README.md` con arquitectura y decisiones técnicas
- [x] Instrucciones de ejecución local — paso a paso en `README.md`
- [x] Supuestos documentados — sección dedicada en `README.md`
- [x] Dockerfile / docker-compose (opcional) — PostgreSQL vía `docker-compose.yml`
- [x] Pruebas unitarias / de reglas de negocio (opcional) — 18 tests xUnit, 0 fallos

---

## 6. Uso de asistentes de IA

_(se documentará aquí, y luego en el README final, para qué se usó la IA durante el desarrollo:
diseño de arquitectura, generación de código, tests, documentación, etc., tal como pide el
desafío)_

---

## 7. Bitácora de avances

| Fecha | Hito |
|---|---|
| 2026-10-08 | Lectura del desafío. Creación de `REQUERIMIENTOS.md` y `PROGRESO.md`. |
| 2026-10-08 | Decisión de stack: .NET 8 (ASP.NET Core Web API) + PostgreSQL + xUnit + Angular 17+ para el frontend opcional. |
| 2026-10-08 | Decisión de arquitectura en capas (Clean Architecture simplificada), descartando hexagonal puro y microservicios. Ver `DECISIONES.md`. |
| 2026-10-09 | SDK .NET 8.0.425 instalado y verificado. Repositorio Git inicializado, `.gitignore` generado, solución `PaymentProcessingService.sln` creada. |
| 2026-10-09 | Creados y restaurados los 6 proyectos: Api, Domain, Application, Infrastructure, Domain.Tests, Application.Tests. Se resolvió un problema de NuGet sin fuente configurada (ver `CONCEPTOS.md`). |
| 2026-10-09 | Los 6 proyectos agregados a `PaymentProcessingService.sln`. Referencias entre capas conectadas (Application→Domain, Infrastructure→Domain/Application, Api→Application/Infrastructure, tests→sus respectivos proyectos). `dotnet build` exitoso: 0 errores. Esqueleto de arquitectura completo. |
| 2026-10-09 | ADR-006 (modelo de datos) y ADR-007 (idempotencia) definidos. Creada entidad `Transaction` y `TransactionStatus` en Domain, con validaciones y transiciones de estado controladas. `dotnet build` limpio (0 errores, 0 advertencias). |
| 2026-10-09 | Repositorio conectado a GitHub (`payment-processing-service`). Primeros 3 commits con formato Conventional Commits: `docs`, `chore` (scaffolding) y `feat(domain)` (entidad Transaction). Push a `main` exitoso. |
| 2026-10-09 | Rama `feature/application-contracts` creada. Definidas interfaces `ITransactionRepository` e `IAcquirerClient` en Application. Creado `CreatePaymentUseCase` orquestando el flujo completo (idempotencia, creación, autorización, actualización de estado). `dotnet build` limpio. |
| 2026-10-09 | PR `feature/application-contracts` y PR de documentación mergeados a `main` vía GitHub. Rama `feature/infrastructure-persistence` creada. `docker-compose.yml` agregado (PostgreSQL 16); resuelto conflicto de puerto 5432 (reasignado a 5436) con otros proyectos del usuario ya corriendo en Docker. Contenedor `payment-processing-postgres` levantado y saludable. |
| 2026-10-09 | Paquetes EF Core instalados: `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (Infrastructure) y `Microsoft.EntityFrameworkCore.Design` 8.0.31 (Api). Resuelto conflicto de versión (NU1202) fijando rama 8.x. Herramienta global `dotnet-ef` 8.0.31 instalada. Creado `PaymentProcessingDbContext` con mapeo explícito de `Transaction` (Fluent API), `Status` como string, y constraint único en `IdempotencyKey`. |
| 2026-10-09 | Creado `TransactionRepository` (implementación real de `ITransactionRepository`). Creado `AcquirerMockClient` (implementación de `IAcquirerClient`, adelantado por necesidad del árbol de dependencias). Cadena de conexión agregada en `appsettings.Development.json`. `Program.cs` configurado con `AddDbContext` + `AddScoped` para las 3 piezas (repositorio, acquirer mock, caso de uso). Migración `InitialCreate` generada y aplicada: tabla `transactions` creada y verificada directamente en PostgreSQL. `dotnet build` limpio (0 errores). |
| 2026-10-09 | Creado `PaymentsController` (`POST /payments`, `GET /payments/{id}`, `GET /payments?merchant_id=&status=`) y los DTOs en `Contracts/`. Archivos de plantilla (`WeatherForecast*`) eliminados. Servidor probado en vivo con `dotnet run` + Swagger: creación de pago aprobado y rechazado (regla de monto máximo), idempotencia verificada (misma petición repetida no duplica fila en BD), consulta por id y por filtros, y validaciones de error (`400` sin header `Idempotency-Key`, `400` con `status` inválido). **Backend funcional end-to-end confirmado.** |
| 2026-10-09 | ADR-008 (manejo de errores temporales) y ADR-009 (logging) definidos. `AcquirerMockClient` simula timeout determinístico (tarjeta `...9999`). `CreatePaymentUseCase` con reintentos (3 intentos, backoff simple) y logging estructurado con `ILogger` nativo, incluyendo `CorrelationId` en cada log. Middleware global de excepciones agregado en `Program.cs` (`500` genérico, sin exponer detalles internos). Probado en vivo: ciclo completo `Pending → Processing → 3 reintentos → Failed` visible en logs reales, capturado en `PRUEBAS.md`. |
| 2026-10-09 | README.md completo: arquitectura, flujo de transacción, endpoints, decisiones destacadas, instrucciones de ejecución, supuestos, alcance fuera del desafío, y uso de IA. |
| 2026-10-09 | Tests unitarios: 10 tests en `Domain.Tests` (reglas de negocio y transiciones de estado de `Transaction`) y 4 en `Application.Tests` (`CreatePaymentUseCase`, con fakes hechos a mano para `ITransactionRepository`/`IAcquirerClient`, sin librerías de mocking). 14/14 tests pasando. |
| 2026-10-09 | Sección "Flujo de trabajo con Git" agregada al README (consistencia en documentación del proceso, pedido explícito del usuario). |
| 2026-10-09 | Frontend Angular 21.2.0 creado (`ng new`, componentes standalone). CORS configurado en el backend. Vista de consulta de transacciones (tabla + filtros por `merchant_id`/`status`) consumiendo `GET /payments` real, probada en vivo con `ng serve` + backend corriendo en paralelo — filtro por estado verificado funcionando. |
| 2026-10-09 | Rediseño visual con paleta e identidad tipográfica reales de Haulmer (azul `#2E4BF2`, rosa `#EC1E82`, botones píldora, wordmark tipográfico). Agregado formulario de creación de pago como modal, con validación nativa de Angular (`NgForm`, `required`/`pattern`/`min`) y notificación toast. Refactor a arquitectura de componentes separados por responsabilidad (`TransactionListComponent`, `PaymentFormModalComponent`, `ToastComponent` + `ToastService`), documentado en ADR-010. Probado en vivo: validación bloqueando envío inválido, y creación exitosa con refresco automático de la tabla. **Proyecto funcionalmente completo al 100% del alcance planeado (obligatorio + opcional).** |
| 2026-10-09 | Generado `Guia_Entrevista_Haulmer.docx` (21 páginas): guía de estudio para la defensa, sintetizando arquitectura, SOLID, conceptos de .NET, los 10 ADRs, idempotencia, errores temporales, logging, seguridad, testing, Angular, Git y 10 preguntas frecuentes con respuestas, más glosario. Archivo de uso personal, no forma parte del entregable del repositorio. |
| 2026-10-09 | Debate a fondo sobre monolito vs. microservicios (equipos, aislamiento de fallas, disponibilidad), resuelto con evidencia concreta (matemática de disponibilidad combinada, caso real de Amazon Prime Video 2023, Shopify/Stripe como monolitos a escala). Ampliada la guía a 24 páginas: nueva sección "Arquitecturas y patrones de diseño del proyecto" con comparaciones en tabla (Monolito vs. Microservicios, Capas vs. Hexagonal) y el inventario completo de patrones de diseño aplicados con su alternativa descartada. |
| 2026-10-09 | Agregado punto evolutivo "workers asíncronos / colas de mensajes" (desacoplar la llamada al adquirente del hilo HTTP) como mejora reconocida fuera de alcance por tiempo, en `README.md` (Fuera de alcance), `DECISIONES.md` (ADR-003) y la guía de entrevista. Agregado comentario explicativo en `AcquirerMockClient.cs` sobre el disparador de prueba determinístico (tarjeta `...9999`), tras discutir buenas prácticas de comentarios en código ("comentar el por qué, no el qué"). |
| 2026-10-09 | **Revisión de seguridad pre-entrega**: detectado que las validaciones de negocio (`ArgumentException` de `Transaction.Create`) devolvían `500` en vez de `400` al no estar capturadas entre Domain y el controller. Corregido con un `catch (ArgumentException)` en `PaymentsController`, traduciendo a `400 Bad Request` con mensaje claro. Verificado en vivo (monto negativo, merchant_id vacío → `400`; caso válido → `201` sin regresión) y confirmado que los 14 tests unitarios siguen pasando. También se confirmó: sin riesgo de SQL Injection (EF Core parametrizado), sin XSS (API JSON pura + Angular escapa por defecto), sin mass assignment (DTOs explícitos), sin fuga de stack traces, tarjeta nunca logueada completa. Evidencia completa en `PRUEBAS.md` (sección 12). |
| 2026-10-09 | **Rama `feature/security-hardening`**. 4 hallazgos adicionales corregidos (ADR-011): (1) **API Key** — middleware en `Program.cs` exigiendo header `X-Api-Key` en `/payments` (exento `/health`/`/swagger`), Swagger con botón "Authorize", frontend con `apiKeyInterceptor` automático; probado `401` sin clave/clave incorrecta, `200` con clave correcta, end-to-end en Angular. (2) **Validación de largo en Domain** — `merchantId`/`currency`/`cardBrand`/`idempotencyKey` ahora validados contra los límites de la base de datos, mismo patrón `400` en vez de `500`; 4 tests nuevos (14→18 total). (3) **Paquetes vulnerables actualizados** — `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`; confirmado 0 vulnerabilidades en los 6 proyectos (`dotnet list package --vulnerable`) y en frontend (`npm audit`). (4) **Resiliencia de conexión** — `EnableRetryOnFailure` en EF Core/Npgsql. (5) **Health check** — `GET /health` sin autenticación, para monitoreo. `dotnet build` y `dotnet test` (18/18) verificados tras cada cambio. Evidencia completa en `PRUEBAS.md` (sección 13). |

---

## 8. Pendientes / próximos pasos

- [x] Número de Pull Requests en README — resuelto cambiando la redacción para no depender de
      un número fijo que se desactualiza en cada commit (ver "Flujo de trabajo con Git")
- [ ] Revisión completa de toda la documentación (ortografía, consistencia) antes de entregar —
      programada para después de descansar
