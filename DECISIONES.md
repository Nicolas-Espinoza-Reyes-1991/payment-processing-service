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

**Evolución reconocida — procesamiento asíncrono:** actualmente `CreatePaymentUseCase` llama al
adquirente de forma síncrona, bloqueando la respuesta HTTP hasta tener el resultado final. Es
una simplificación consciente, coherente con el alcance del desafío. En un escenario de
producción con alto volumen, el siguiente paso evolutivo natural (antes de considerar
microservicios) sería desacoplar esa llamada con una cola de mensajes (RabbitMQ, AWS SQS): el
comercio recibiría `202 Accepted` de inmediato, un *worker* en segundo plano procesaría la
autorización, y el resultado se notificaría vía webhook. Esto evita que una llamada lenta al
adquirente bloquee el hilo HTTP principal, sin necesitar partir el sistema en servicios
separados — es un ejemplo concreto de evolución **dentro** del monolito modular, no hacia
microservicios.

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

---

## ADR-010: Arquitectura del frontend Angular — componentes separados por responsabilidad

**Contexto:** el frontend (opcional) se implementó inicialmente como un único componente
(`App`) con la tabla, los filtros, el modal de creación de pago y las notificaciones todo
mezclado. Funcionaba, pero mezclaba responsabilidades claramente distintas en un mismo archivo.

**Decisión:** se refactorizó en 3 componentes + 1 servicio, aplicando el mismo principio de
separación de responsabilidades (Single Responsibility) usado en todo el backend:

- **`TransactionListComponent`** (presentacional/"tonto"): recibe los datos por `input()`,
  emite la intención de búsqueda por `output()`, nunca llama a la API directamente.
- **`PaymentFormModalComponent`**: encapsula el formulario de creación, su validación, y el
  envío a la API — una única responsabilidad bien delimitada, equivalente conceptualmente a
  `CreatePaymentUseCase` en el backend (una sola capacidad de negocio).
- **`ToastComponent`** (presentacional): solo renderiza el estado actual de `ToastService`, sin
  lógica propia.
- **`ToastService`** (`providedIn: 'root'`): estado compartido de notificaciones, inyectable
  desde cualquier componente — mismo patrón de Inyección de Dependencias aplicado en .NET con
  `AddScoped`/`AddSingleton`.
- **`App`** queda como orquestador liviano: mantiene el estado de la lista de transacciones y
  conecta los 3 componentes hijos vía `input()`/`output()`, sin lógica de formulario ni de
  notificaciones propia.

**Paralelismo con la arquitectura del backend:** los componentes presentacionales
(`TransactionListComponent`, `ToastComponent`) cumplen un rol similar al de `Domain` — no saben
nada de HTTP ni de infraestructura, solo reciben datos y emiten intenciones. `TransactionsService`
(la llamada HTTP real) cumple el rol de `Infrastructure`. `PaymentFormModalComponent` orquesta
una capacidad de negocio concreta, similar a un caso de uso de `Application`.

**Por qué no un patrón más elaborado (NgRx, estado global, feature modules):** para 4
componentes y un flujo de datos simple (una lista, un formulario), un gestor de estado como
NgRx agregaría complejidad sin resolver ningún problema real presente — mismo criterio aplicado
al descartar microservicios/hexagonal en el backend (ver ADR-003): la arquitectura correcta es
la que resuelve la complejidad que existe, no la que podría existir.

**Diseño visual:** se tomó como referencia la paleta e identidad visual del sitio público de
Haulmer (haulmer.com) — azul `#2E4BF2` como color primario, rosa/magenta `#EC1E82` como acento,
botones en forma de píldora, tarjetas con bordes muy redondeados, y un wordmark tipográfico
("Haulmer" en negrita, sin ícono gráfico — consistente con su identidad real, que tampoco usa
un ícono) en el header. No se reprodujo ningún asset gráfico (logo/ícono) de la empresa, solo se
tomó inspiración de paleta y tipografía para un proyecto personal de postulación.

**Validación contra la guía oficial de Angular:** este diseño coincide con lo que el propio
equipo de Angular recomienda — standalone components (default desde Angular 17+), composición
de componentes chicos comunicados por `input()`/`output()`, y el patrón contenedor/presentacional
(estándar en toda la industria frontend, no exclusivo de Angular). `PaymentFormModalComponent`
no es 100% presentacional (inyecta sus propios servicios para el envío) — una excepción
deliberada y aceptada: un componente de formulario puede poseer su propia lógica de envío,
igual que `CreatePaymentUseCase` concentra una sola capacidad de negocio en el backend.

---

## ADR-011: Revisión de seguridad final — hallazgos y correcciones

**Contexto:** antes de la entrega, se hizo una revisión de seguridad dedicada (inyección SQL,
XSS, mass assignment, fuga de datos en errores, exposición de la API, paquetes vulnerables).
Se encontraron y corrigieron 5 hallazgos reales.

### 1. Sin autenticación en los endpoints

**Decisión:** se agregó un control de **API Key** simple — un middleware en `Program.cs` que
exige el header `X-Api-Key` en todas las rutas salvo `/health` y `/swagger`, devolviendo `401`
si falta o no coincide con el valor configurado. Swagger se configuró con un esquema de
seguridad (`AddSecurityDefinition`) para poder autorizar desde la UI con el botón "Authorize".
El frontend Angular lo adjunta automáticamente vía un `HttpInterceptorFn`
(`apiKeyInterceptor`), para no tener que agregar el header a mano en cada llamada.

**Por qué API Key y no OAuth2/JWT completo:** un esquema de usuarios/tokens completo es una
pieza de alcance mucho mayor (gestión de usuarios, refresh tokens, expiración), no proporcional
al resto del desafío. API Key es el control mínimo razonable para demostrar que el endpoint no
queda completamente abierto, dejando documentado que en producción real correspondería
autenticación por comercio (API Keys individuales, guardadas hasheadas) o un esquema más
robusto si hay usuarios finales.

**Por qué no se consideró "fuera de alcance" como otros puntos:** a diferencia de rate limiting
u observabilidad centralizada, no tener ningún control de acceso en una API que simula un
sistema de pagos es una omisión que cualquier revisor notaría de inmediato — el costo de
implementarlo (un middleware simple) era mucho menor que el costo de no tenerlo.

### 2. Validaciones de largo ausentes en el Domain (causaban `500` en vez de `400`)

**Hallazgo:** `Transaction.Create()` no validaba el largo de `merchantId`, `currency`,
`cardBrand` ni `idempotencyKey` contra los límites configurados en la base de datos
(`HasMaxLength` en `PaymentProcessingDbContext`). Un valor que excediera esos límites llegaba
sin control hasta PostgreSQL, que lo rechazaba con una excepción no controlada — el mismo
patrón de bug que el de ADR anterior (validación de monto), mapeado incorrectamente a `500`.

**Corrección:** se agregaron las mismas validaciones de largo en `Transaction.Create()`
(`ArgumentException`, ya capturada como `400` por el controller desde la corrección anterior),
replicando los límites de la base de datos también en el Domain — la base de datos deja de ser
la única línea de defensa.

### 3. Paquetes NuGet transitivos vulnerables en proyectos de test

**Hallazgo:** `dotnet list package --vulnerable` reportó `System.Net.Http` y
`System.Text.RegularExpressions` (versión 4.3.0, severidad "High") como dependencias
transitivas de versiones antiguas de `Microsoft.NET.Test.Sdk`/`xunit`/`coverlet.collector` en
los proyectos de test — no afectaban el código de producción (`Api`/`Application`/
`Domain`/`Infrastructure` salieron limpios).

**Corrección:** se actualizaron `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` y
`coverlet.collector` a sus versiones más recientes compatibles con .NET 8, eliminando las
dependencias transitivas vulnerables. Confirmado con `dotnet list package --vulnerable`: 0
paquetes vulnerables en los 6 proyectos de la solución.

### 4. Sin reintentos ante fallas transitorias de conexión a PostgreSQL

**Decisión:** se agregó `EnableRetryOnFailure(maxRetryCount: 3)` a la configuración de
`UseNpgsql` en `Program.cs` — una línea de configuración nativa de EF Core/Npgsql que reintenta
automáticamente operaciones de base de datos ante errores transitorios de red/conexión (no
errores de lógica). Complementa, a otro nivel, la misma filosofía de resiliencia que ya se
aplicó para la llamada al adquirente (ADR-008).

### 5. Sin endpoint de monitoreo (`/health`)

**Decisión:** se agregó `app.MapHealthChecks("/health")`, usando el *health checks* nativos de
ASP.NET Core (sin paquetes adicionales). Es un estándar esperado en cualquier servicio real —
necesario para que un balanceador de carga, Kubernetes, o cualquier sistema de monitoreo externo
pueda verificar si la aplicación está viva, sin necesitar autenticación (por eso se excluyó
explícitamente del middleware de API Key).

### Verificación

Los 5 hallazgos se probaron en vivo contra el servidor real (ver `PRUEBAS.md`, sección 13), y
los 18 tests unitarios (14 Domain + 4 Application, subieron de 14 a 18 con las nuevas
validaciones de largo) siguen pasando sin regresiones tras todos los cambios.

---

## ADR-012: Idempotencia — detectar reuso de la misma clave con datos distintos

**Contexto:** la implementación original de idempotencia (ADR-007) solo verificaba si la
`IdempotencyKey` ya existía — si existía, devolvía la transacción guardada **sin comparar** si
los datos del nuevo request (monto, comercio, moneda) coincidían con los originales. Un cliente
que reusara una clave por error, con datos distintos, recibía silenciosamente la transacción
vieja, sin ningún aviso de la inconsistencia.

**Decisión:** se agregó una comparación explícita en `CreatePaymentUseCase.ExecuteAsync`: si la
clave existe pero `MerchantId`/`Amount`/`Currency` no coinciden con el request original, se
lanza `IdempotencyConflictException`, traducida por el controller a `409 Conflict` con un
mensaje explicando el conflicto. El frontend Angular ya estaba preparado para mostrar cualquier
mensaje de error del backend (banner dentro del modal + toast), sin necesitar cambios
adicionales más allá de también disparar el toast en el flujo de error.

**Por qué esto protege algo real, aunque el propio frontend nunca lo dispare:** el frontend
Angular genera una `IdempotencyKey` nueva (`crypto.randomUUID()`) en cada envío — por diseño,
nunca reutiliza una clave, así que nunca va a disparar este escenario por sí mismo. Pero la API
no es consumida únicamente por este frontend: en un escenario real, cualquier otro cliente
(el backend del propio comercio, una integración de terceros, pruebas manuales con
Postman/`curl`) podría reutilizar una clave por error. La validación protege la API contra
**cualquier** cliente, no solo el que construimos nosotros — mismo principio de "no confiar
ciegamente en el cliente" aplicado ya en las validaciones de Domain.

**Verificación:** probado directamente contra la API real (no vía UI, porque el frontend nunca
genera este escenario por diseño): reenvío de la misma clave con monto distinto → `409` con el
mensaje esperado; reenvío con los mismos datos → sigue devolviendo la transacción original sin
duplicar, sin regresión. Test unitario agregado en `Application.Tests` reproduciendo el mismo
escenario de forma determinística. 19/19 tests pasando.
