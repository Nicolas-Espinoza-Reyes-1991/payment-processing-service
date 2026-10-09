# Progreso del desafío — Payment Processing Service

> Documento vivo de seguimiento: hitos, checklist y próximos pasos.
> El requerimiento original e inmutable está en [`REQUERIMIENTOS.md`](./REQUERIMIENTOS.md).
> El razonamiento detrás de cada decisión técnica (para defender en la entrevista) está en
> [`DECISIONES.md`](./DECISIONES.md).

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
| PostgreSQL | _pendiente_ | ⬜ Pendiente | Se levanta vía contenedor Docker, no instalación local |

> Esta tabla alimenta directamente la sección "Instrucciones para ejecutar el proyecto
> localmente" del README final.

---

## 2. Diseño de alto nivel

_(diagrama de arquitectura, componentes principales y flujo de la transacción — se agregará aquí
o se referenciará un archivo en `/docs`)_

---

## 3. Checklist de requerimientos funcionales

- [ ] `POST /payments` — crear solicitud de pago
- [x] Validación de entrada a nivel de entidad (`merchant_id`, monto, moneda) — `Transaction.Create()`
- [x] Creación de transacción con estado inicial (`PENDING`) — entidad `Transaction` en Domain
- [ ] Reglas de negocio básicas (monto máximo, validación de tarjeta)
- [ ] Integración con Acquirer Mock
- [ ] Actualización de estado final según respuesta del adquirente
- [ ] `GET /payments/{transaction_id}`
- [ ] `GET /payments?merchant_id=...&status=...`

## 4. Checklist de requerimientos no funcionales

- [ ] Persistencia en base de datos relacional
- [ ] Modelo de datos con trazabilidad
- [ ] Manejo de errores de validación y fallos del sistema
- [ ] Idempotencia (evitar transacciones duplicadas)
- [ ] Manejo de errores temporales del adquirente (reintentos / timeouts)
- [ ] Logging estructurado del flujo completo
- [ ] Correlation ID / Trace ID por transacción

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
