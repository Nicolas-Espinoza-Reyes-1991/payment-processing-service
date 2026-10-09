# Decisiones de arquitectura — Payment Processing Service

> Registro de decisiones técnicas en formato ADR (Architecture Decision Record), pensado como
> material de defensa para la entrevista. Cada decisión documenta el contexto, qué se eligió,
> qué alternativas se consideraron y por qué se descartaron.
>
> El seguimiento de hitos y checklist está en [`PROGRESO.md`](./PROGRESO.md).
> El requerimiento original está en [`REQUERIMIENTOS.md`](./REQUERIMIENTOS.md).

---

## ADR-001: Backend en .NET 8 (ASP.NET Core Web API)

**Contexto:** El PDF permite elegir entre PHP/Laravel y .NET/C#, pero indica preferencia
explícita de Haulmer por .NET ("nos encanta muchísimo más").

**Decisión:** ASP.NET Core Web API sobre .NET 8.

**Alternativas consideradas:** PHP con Laravel.

**Por qué no la alternativa:** Laravel es igualmente válido y productivo, pero elegir .NET
permite evaluar la solución en el stack que el equipo usa día a día, que es justamente lo que
el PDF invita a hacer.

**Por qué la versión 8 específicamente, y no una más nueva:** Microsoft publica una versión
nueva de .NET cada noviembre, alternando entre soporte **LTS** (Long Term Support, 3 años de
soporte) y **STS** (Standard Term Support, 18 meses). .NET 8 (lanzado noviembre 2023) es
versión **LTS**, con soporte hasta noviembre de 2026. Se prefirió sobre una versión más nueva
(ej. .NET 9, STS) por tres motivos:
1. **Estabilidad en producción:** las empresas, en la práctica, migran a versiones LTS para
   sistemas reales, no a cada versión intermedia — elegir LTS refleja una decisión de
   ingeniería real, no solo "la más nueva disponible".
2. **Compatibilidad de paquetes de terceros:** durante el desarrollo se confirmó este punto en
   la práctica — `Npgsql.EntityFrameworkCore.PostgreSQL` en su versión más reciente (10.x) solo
   soportaba .NET 10, no .NET 8 (ver detalle del error `NU1202` en `CONCEPTOS.md`). Las
   versiones LTS tienen mayor tiempo de maduración del ecosistema de paquetes alrededor.
3. **Soporte vigente:** con soporte hasta noviembre de 2026, .NET 8 cubre con margen la
   duración de este desafío y cualquier evolución futura cercana del proyecto.

**Historia breve de versiones de .NET (para contexto):** .NET Framework (2002-2019, solo
Windows) → .NET Core (2016 en adelante, multiplataforma, nace como proyecto separado) → a
partir de .NET 5 (2020) se unifican bajo el nombre simple ".NET", continuando la numeración de
.NET Core (se saltó el "4" a propósito para no confundir con .NET Framework 4.x).

**Consecuencias:** Pruebas con xUnit, ORM natural es Entity Framework Core, tipado fuerte en
todo el dominio.

---

## ADR-002: Base de datos PostgreSQL

**Contexto:** Se requiere una base relacional (MySQL, PostgreSQL o SQL Server).

**Decisión:** PostgreSQL.

**Alternativas consideradas:** SQL Server (integración "nativa" con .NET/EF Core), MySQL.

**Por qué no las alternativas:**
- SQL Server: imagen Docker más pesada, licenciamiento más restrictivo fuera de desarrollo,
  sin ventaja real para este alcance.
- MySQL: válido, pero el tipo `numeric` de Postgres y su soporte de constraints/checks es más
  cómodo para modelar montos monetarios y reglas de negocio a nivel de base de datos.

**Consecuencias:** Imagen oficial `postgres` liviana en Docker Compose, EF Core con el
proveedor `Npgsql`.

---

## ADR-003: Arquitectura en capas (Clean Architecture simplificada) — no hexagonal, no microservicios

**Contexto:** Hay que decidir cómo organizar el código para separar responsabilidades
(entrada HTTP, reglas de negocio, integración con adquirente, persistencia) con trazabilidad
completa del flujo.

**Decisión:** Arquitectura en capas al estilo Clean Architecture:
`Api` (controllers/entrada HTTP) → `Application` (casos de uso, orquestación) →
`Domain` (entidades, reglas de negocio puras, estados) → `Infrastructure` (EF Core/Postgres,
cliente del Acquirer Mock, logging).

El `Domain` no depende de ningún framework externo (ni EF Core ni ASP.NET), lo que permite
testear las reglas de negocio con xUnit sin levantar base de datos ni HTTP.

**Alternativas consideradas:** Arquitectura hexagonal (puertos y adaptadores), microservicios.

**Por qué no microservicios:**
- Resuelven un problema que no existe acá: escalar equipos y despliegues de forma
  independiente. El desafío es de un solo desarrollador, un solo repo, evaluado en una sola
  sesión.
- El dominio es chico (transacciones + reglas de negocio + adquirente mock): no hay bounded
  contexts que justifiquen partirlo en servicios separados.
- Agregan complejidad accidental real (orquestación, comunicación de red entre servicios,
  consistencia eventual, más piezas de Docker) sin resolver ningún problema del enunciado —
  en una evaluación de 72h eso se lee como sobre-ingeniería, no como solidez técnica.
- Regla práctica ("monolith first", Fowler): partir monolítico y dividir en microservicios
  solo cuando el dominio o el equipo lo exijan. Acá nunca se llega a ese punto.

**Por qué no arquitectura hexagonal "pura":**
- Hexagonal (puertos y adaptadores) brilla cuando se esperan **múltiples adaptadores reales**
  por puerto (hoy Postgres, mañana Mongo; hoy REST, mañana gRPC) o se quiere aislar el dominio
  al extremo. Acá solo hay un adaptador de entrada (HTTP) y uno de salida (Postgres) — el
  beneficio de intercambiabilidad es teórico, no se llega a demostrar.
- Clean Architecture en capas da el mismo beneficio central (dominio aislado, testeable, sin
  dependencias de infraestructura) con una estructura más explícita y familiar en el ecosistema
  .NET — reduce fricción porque es el vocabulario que el equipo evaluador ya usa a diario.
- No cierra la puerta a evolucionar hacia hexagonal o microservicios más adelante: al tener
  `Application`/`Domain` ya desacoplados de `Infrastructure`, extraerlos después es barato.

**Consecuencias:** Carpetas/proyectos separados por capa dentro de una única solución .NET;
tests unitarios concentrados en `Domain`/`Application`; tests de integración sobre
`Infrastructure`/`Api`.

---

## ADR-004: Framework de pruebas xUnit

**Contexto:** El PDF sugiere xUnit o NUnit para .NET.

**Decisión:** xUnit.

**Por qué no NUnit:** Ambos son equivalentes en capacidad; xUnit es el estándar de facto en
proyectos .NET modernos (incluida la plantilla por defecto de `dotnet new`), lo que reduce
configuración adicional.

---

## ADR-005: Frontend opcional en Angular 17+

**Contexto:** El PDF ofrece como opcional sumar una vista en Angular 17+ para consultar
transacciones.

**Decisión:** Incluirlo, como última etapa del desarrollo, después de tener el backend
completo y testeado.

**Por qué:** Las 72h sugeridas alcanzan para completarlo sin comprometer la calidad del
backend (que es el foco real del desafío), y permite demostrar el flujo end-to-end completo
en la entrevista.

**Consecuencias:** Se prioriza backend → tests → documentación, y el frontend se aborda solo
si el resto está sólido primero (ver orden en `PROGRESO.md`).

---

## ADR-006: Modelo de datos de `Transaction` y trazabilidad

**Contexto:** hay que definir qué campos persiste una transacción, considerando trazabilidad
completa del flujo (requerimiento no funcional del PDF) y evitando guardar datos sensibles de
tarjeta sin necesidad.

**Decisión:** la entidad `Transaction` tendrá: `Id`, `MerchantId`, `Amount`, `Currency`,
`CardLast4`, `CardBrand`, `Status`, `IdempotencyKey`, `CorrelationId`, `CreatedAt`, `UpdatedAt`,
`AcquirerResponseCode`, `AcquirerMessage`.

**Por qué no guardar el número completo de tarjeta:** es una norma estándar de la industria
(PCI-DSS) no persistir el PAN (número completo) salvo que el sistema esté certificado para
eso. Aunque es un adquirente mock, se decide modelar la entidad como se haría en un sistema
real: solo se guardan los últimos 4 dígitos y la marca, suficientes para trazabilidad y
soporte, sin exponer el dato sensible completo.

**Trazabilidad:** el campo `CorrelationId` viaja en cada log asociado a una transacción,
permitiendo reconstruir el flujo completo (recepción → validación → llamada al adquirente →
actualización de estado) filtrando por ese id.

**Por qué `Status` es un `enum` y no un `string`:** un `enum` restringe el valor a un conjunto
cerrado y conocido en tiempo de compilación (`Pending`, `Processing`, `Approved`, `Declined`,
`Failed`). Con un `string`, un error de tipeo como `"aproved"` compilaría sin problema y
recién fallaría en producción, de forma silenciosa, al comparar contra `"approved"` en otro
lado del código. Con `enum`, ese mismo error es detectado por el compilador antes de poder
ejecutar el programa — convierte un bug potencial en tiempo de ejecución en un error imposible
de cometer en tiempo de compilación. Además, el IDE autocompleta las opciones válidas, lo que
reduce errores y acelera el desarrollo.

**Consecuencia práctica — resuelto:** al configurar `PaymentProcessingDbContext`, se decidió
mapear `Status` explícitamente a texto en PostgreSQL con `HasConversion<string>()`, en vez de
dejar el comportamiento por defecto de EF Core (guardarlo como entero: 0, 1, 2...). Así una
consulta SQL directa (`SELECT * FROM transactions WHERE status = 'Approved'`) es legible sin
tener que recordar la equivalencia número→estado — prioriza la trazabilidad y el debugging
manual por sobre el mínimo ahorro de espacio en disco de guardar un entero.

---

## ADR-007: Estrategia de idempotencia

**Contexto:** el PDF pide evitar duplicidad de transacciones mediante algún mecanismo de
idempotencia (ej. si el comercio reintenta una solicitud por timeout, no debe crearse una
transacción duplicada).

**Decisión:** el comercio envía un header `Idempotency-Key` en `POST /payments`. Antes de crear
una transacción, se busca si ya existe una con esa clave:
- Si existe, se devuelve la transacción ya creada (mismo resultado, sin duplicar).
- Si no existe, se crea una nueva, guardando la clave.

Como segunda barrera (ante condiciones de carrera con dos requests simultáneos), la columna
`IdempotencyKey` tendrá una restricción `UNIQUE` a nivel de base de datos.

**Alternativas consideradas:** generar la idempotencia a partir de un hash de los datos de la
solicitud (merchant_id + monto + tarjeta). Se descartó porque requeriría asumir qué campos son
"iguales" para considerar dos solicitudes como la misma, lo cual es ambiguo (ej. dos compras
distintas del mismo monto y tarjeta en el mismo segundo serían indistinguibles). Un header
explícito, generado por el cliente (comercio), es el estándar de la industria (Stripe, por
ejemplo, usa exactamente este patrón).

---

## ADR-008: Manejo de errores temporales del Acquirer Mock

**Contexto:** el PDF pide "considerar escenarios donde el adquirente pueda responder con
errores temporales" — hasta este punto, `AcquirerMockClient` siempre respondía (aprobado o
rechazado), nunca fallaba, por lo que no había nada que manejar ni demostrar.

**Decisión — simulación del error:** se agregó un disparador **determinístico** (no aleatorio)
en `AcquirerMockClient`: si los últimos 4 dígitos de la tarjeta son `"9999"`, el método lanza
una `TimeoutException` en vez de responder, simulando que el adquirente real no contestó a
tiempo.

**Por qué determinístico y no aleatorio:** un disparo aleatorio (ej. "falla el 10% de las
veces") haría el comportamiento difícil de reproducir a demanda — no se podría mostrar el
escenario de error de forma confiable en la entrevista, ni testear de forma determinística. Con
una tarjeta específica, el fallo se puede provocar siempre que se necesite.

**Decisión — manejo del error (reintentos):** en `CreatePaymentUseCase`, la llamada al
adquirente se envuelve en un bucle de **reintentos simples** (sin librerías externas como
Polly): hasta 3 intentos totales, con una breve espera creciente entre intentos (backoff
simple). Si los 3 intentos fallan, la transacción pasa a estado `Failed` y se persiste así,
nunca queda en un estado intermedio ambiguo.

**Por qué reintentos simples en vez de una librería como Polly:** Polly es la librería estándar
de la industria .NET para políticas de resiliencia (reintentos, circuit breaker, timeout) y es
una elección perfectamente válida en un proyecto real de mayor escala. Para el alcance de este
desafío, un bucle de reintentos manual de ~10 líneas resuelve el requerimiento exacto que pide
el PDF sin sumar una dependencia nueva — ya se discutió el criterio de mantener el set de
librerías mínimo y justificado (ver nota en `CONCEPTOS.md` sobre confiabilidad de
dependencias). Si el proyecto creciera y necesitara políticas más sofisticadas (circuit
breaker, jitter, fallback), Polly sería la elección natural.

**Consecuencias:** la transacción solo llega a `Failed` después de agotar los reintentos —
nunca por una falla aislada de un solo intento. Este comportamiento se puede reproducir a
demanda usando `"cardNumber"` terminada en `9999` en cualquier `POST /payments`.

---

## ADR-009: Estrategia de logging/observabilidad

**Contexto:** el PDF pide logs que permitan seguir el flujo completo de una transacción, y
poder correlacionar eventos relacionados con una misma transacción.

**Decisión:** se usa `ILogger<T>` nativo de .NET (parte de `Microsoft.Extensions.Logging`, ya
incluido en el SDK — no requiere ningún paquete NuGet adicional), con logging estructurado:
cada log relevante del flujo de `CreatePaymentUseCase` incluye el `CorrelationId` de la
transacción como parte del mensaje/scope, permitiendo filtrar todos los eventos de una misma
transacción buscando ese identificador.

**Por qué no Serilog u otra librería de logging más avanzada:** Serilog (u otras como NLog) son
elecciones comunes en proyectos reales para enviar logs a destinos externos (archivos
estructurados, Elasticsearch, Seq, Application Insights). Para este desafío, donde los logs se
consultan en la consola/terminal durante la demo, el logging nativo de ASP.NET Core cumple el
requerimiento sin sumar otra dependencia — mismo criterio que en ADR-008. Se documenta como
posible evolución futura si el sistema necesitara centralizar logs de múltiples instancias.
