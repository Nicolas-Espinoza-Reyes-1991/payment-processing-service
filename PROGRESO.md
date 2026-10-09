# Progreso del desafío — Payment Processing Service

> Documento vivo de seguimiento: hitos, checklist y próximos pasos.
> El requerimiento original e inmutable está en [`REQUERIMIENTOS.md`](./REQUERIMIENTOS.md).
> El razonamiento detrás de cada decisión técnica (para defender en la entrevista) está en
> [`DECISIONES.md`](./DECISIONES.md).
> La evidencia de pruebas end-to-end (requests, responses y capturas reales) está en
> [`PRUEBAS.md`](./PRUEBAS.md).

Última actualización: 2026-10-08

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
| Node.js / npm | _pendiente_ | ⬜ Pendiente | Necesario para Angular CLI, se verifica más adelante |
| Angular CLI | _pendiente_ | ⬜ Pendiente | Versión 17+, se instala en la etapa de frontend |
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
- [x] Manejo de errores de validación — probado: `400` sin `Idempotency-Key`, `400` con `status` inválido. _(Falta: manejo de errores no controlados/500 con middleware global)_
- [x] Idempotencia (evitar transacciones duplicadas) — probado en vivo: misma petición repetida no duplica fila en BD
- [ ] Manejo de errores temporales del adquirente (reintentos / timeouts)
- [ ] Logging estructurado del flujo completo
- [x] Correlation ID / Trace ID por transacción — campo `CorrelationId` en `Transaction`, generado en `Create()`, visible en las respuestas

## 5. Entregables

- [ ] Código fuente en repositorio Git
- [ ] `README.md` con arquitectura y decisiones técnicas
- [ ] Instrucciones de ejecución local
- [ ] Supuestos documentados
- [ ] Dockerfile / docker-compose (opcional)
- [ ] Pruebas unitarias / de reglas de negocio (opcional)

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

---

## 8. Pendientes / próximos pasos

- [ ] Agregar los 6 proyectos a la solución y conectar referencias entre capas (Paso 5)
- [ ] Diseñar diagrama de flujo de alto nivel
- [ ] Definir modelo de datos (tabla `transactions`, estados, trazabilidad)
- [ ] Definir estrategia de idempotencia
- [ ] Implementar Acquirer Mock
- [ ] Implementar endpoints
- [ ] Escribir pruebas
- [ ] Escribir README final
