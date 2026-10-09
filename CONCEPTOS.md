# Conceptos y comandos — guía de repaso

> Explicación en lenguaje simple de cada herramienta, comando y término técnico usado durante el
> desarrollo. Pensado como material de repaso rápido antes de la entrevista — no reemplaza
> `DECISIONES.md` (el "por qué elegí X"), sino que explica "qué es X" y "para qué sirve".
>
> Se va completando a medida que aparecen nuevos conceptos.

---

## .NET y herramientas base

### ¿Qué es ".NET" exactamente? (y por qué no es lo mismo que el comando `dotnet`)

Es un error común confundirlos, vale la pena tenerlo claro:

- **.NET** es una **plataforma de desarrollo completa**: incluye el runtime que ejecuta el
  código compilado (el CLR — Common Language Runtime), un conjunto grande de librerías base
  (BCL), soporte para varios lenguajes (C#, F#, VB.NET), y frameworks específicos construidos
  encima, como **ASP.NET Core** (para web APIs) o **Entity Framework Core** (para acceso a
  bases de datos). Es el ecosistema entero — equivalente a hablar de "Node.js" o "la JVM + Java".
- **`dotnet` (la CLI)** es solo **una herramienta dentro de esa plataforma**: el programa de
  línea de comandos para interactuar con .NET desde la terminal (crear proyectos, compilar,
  ejecutar, testear), sin depender de una interfaz gráfica como Visual Studio.
- El **SDK de .NET** (lo que instalaste, versión `8.0.425`) es el paquete que trae todo lo
  necesario para *desarrollar*: el compilador, las herramientas de build, y la CLI `dotnet`.
  Es distinto del **Runtime**, que es lo mínimo necesario solo para *ejecutar* una app .NET ya
  compilada (por eso en producción a veces se instala solo el runtime, más liviano).

Jerarquía simplificada:

```
.NET (la plataforma)
 ├── Runtime (ejecuta el código compilado — CLR)
 ├── Librerías base (BCL)
 ├── Lenguaje C#
 ├── Frameworks: ASP.NET Core, Entity Framework Core
 └── SDK de .NET (compilador + herramientas de build + CLI)
      └── dotnet CLI  ← el comando que usás en la terminal
```

**Analogía:** .NET es el motor y la carrocería completa de un auto. `dotnet` (CLI) es el
tablero de comandos desde donde lo manejás — no es el auto, es la forma de controlarlo.

### ¿Qué es `dotnet`?

Es la **CLI (interfaz de línea de comandos) del SDK de .NET**: un único programa capaz de crear,
compilar, ejecutar, testear y publicar proyectos .NET. Equivalente a `npm`/`npx` en el mundo
JavaScript, o a `composer`/`php artisan` en Laravel.

Funciona como una navaja suiza: `dotnet <subcomando>`, donde cada subcomando hace una tarea
puntual (`new`, `build`, `run`, `test`, `add`, `sln`, etc.).

### `dotnet new gitignore`

Genera un archivo `.gitignore` con las reglas estándar para proyectos .NET (ignora `bin/` y
`obj/`, carpetas que se regeneran automáticamente al compilar y no deben versionarse en Git).

**Por qué importa:** mantiene el repositorio limpio — solo código fuente, sin binarios
compilados ni archivos temporales que generan ruido y conflictos.

### ¿`.gitignore` de .NET cubre archivos sensibles (conexión a BD, secretos)?

**Solo parcialmente.** El `.gitignore` generado por `dotnet new gitignore` ignora `.env`,
certificados (`*.pfx`) y perfiles de publicación (`*.pubxml`, con una advertencia explícita de
Microsoft sobre cadenas de conexión sin encriptar), pero **no ignora `appsettings.json` ni
`appsettings.Development.json`** — que es donde, por convención, se suelen poner cadenas de
conexión a la base de datos.

La forma "correcta" en .NET para desarrollo local no es ponerlo en `.gitignore`, sino usar
**User Secrets** (`dotnet user-secrets`): un archivo JSON que .NET guarda **fuera** de la
carpeta del proyecto (en el perfil de usuario de la máquina), así que nunca corre riesgo de
terminar en el repositorio. Se configura con `dotnet user-secrets init` y
`dotnet user-secrets set "Clave" "Valor"`. Lo vamos a usar al conectar con PostgreSQL — se
documenta en detalle en ese momento.

### `dotnet new sln -n <Nombre>`

Crea un archivo `.sln` ("solution file"): un **organizador** de proyectos, no contiene código
en sí. Agrupa varios proyectos `.csproj` (Api, Domain, Application, Infrastructure, tests) para
que las herramientas (VS Code, Visual Studio, el propio CLI) los compilen y gestionen como un
solo conjunto, respetando las dependencias entre ellos.

**Analogía:** si cada proyecto (`.csproj`) es un libro, la solución (`.sln`) es la biblioteca
que los agrupa y sabe qué libro depende de cuál.

**Por qué importa:** sin la solución habría que compilar y referenciar cada proyecto a mano. Con
ella, un solo comando compila todo en el orden correcto, y la estructura en capas de la
arquitectura se ve de un vistazo en el editor.

---

### ¿Qué es NuGet? (y el problema que tuvimos)

**NuGet** es el gestor de paquetes de .NET — el equivalente a `npm` en JavaScript o `composer`
en PHP. Cuando un proyecto necesita una librería externa (ej. Swagger para documentar el API,
o el framework xUnit para tests), no viene incluida en el SDK: se descarga de un repositorio
público llamado **nuget.org**, de forma similar a como `npm install` descarga de npmjs.com.

**Problema encontrado:** al crear los proyectos `Api` y `Domain.Tests`, falló la descarga de
paquetes con error `NU1100`. El diagnóstico (`dotnet nuget list source`) mostró
`"No se encontró ningún origen"` — la máquina no tenía ninguna fuente de NuGet configurada
(la conexión a internet funcionaba bien, confirmado con `ping nuget.org`). Es decir, .NET no
sabía *dónde* buscar los paquetes, aunque podía llegar a internet perfectamente.

**Solución:**
```bash
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```
Esto registra la fuente oficial pública de NuGet. Después de esto, `dotnet restore` (el comando
que descarga/instala los paquetes que pide un `.csproj`) funcionó normalmente.

**Por qué importa entenderlo:** los proyectos `classlib` vacíos (`Domain`, `Application`,
`Infrastructure`) se crearon sin error porque no tienen ninguna dependencia externa todavía —
no necesitaron descargar nada. En cambio `Api` (necesita Swashbuckle/Swagger) y los proyectos
de tests (necesitan xUnit) sí dependen de paquetes de NuGet, por eso fueron los únicos que
fallaron.

### `dotnet new webapi` / `classlib` / `xunit` — los 3 tipos de proyecto que creamos

Cada `dotnet new <plantilla>` genera una carpeta con un `.csproj` y archivos base distintos
según el tipo de proyecto:

- **`webapi`** (usado para `Api`): genera un proyecto ejecutable que levanta un servidor HTTP
  (ASP.NET Core). Es el único que realmente "corre" y escucha peticiones. Con el flag
  `--use-controllers` genera el estilo de Controllers (clases con `[HttpPost]`, `[HttpGet]`)
  en vez del estilo "Minimal API".
- **`classlib`** (usado para `Domain`, `Application`, `Infrastructure`): genera una "librería de
  clases" — código C# que **no se ejecuta solo**, está pensado para ser usado (referenciado)
  por otro proyecto. No tiene servidor, no tiene punto de entrada propio.
- **`xunit`** (usado para `Domain.Tests`, `Application.Tests`): genera un proyecto de pruebas ya
  configurado con el framework xUnit (que elegimos en ADR-004) y sus paquetes NuGet necesarios.

### `dotnet sln add <ruta-al-csproj>` — agregar un proyecto a la solución

**Qué hace:** registra un proyecto `.csproj` dentro del archivo `.sln`, modificando ese archivo
automáticamente (nunca se edita el `.sln` a mano). Es una operación puramente **organizativa**:
le dice a las herramientas (VS Code, `dotnet build`) "este proyecto forma parte de este grupo",
pero **no** establece ningún permiso para que un proyecto use código de otro — eso lo hace
`dotnet add reference` (ver abajo).

**Comandos usados** (uno por cada uno de los 6 proyectos):
```bash
dotnet sln add src/Api/PaymentProcessingService.Api.csproj
dotnet sln add src/Domain/PaymentProcessingService.Domain.csproj
dotnet sln add src/Application/PaymentProcessingService.Application.csproj
dotnet sln add src/Infrastructure/PaymentProcessingService.Infrastructure.csproj
dotnet sln add tests/Domain.Tests/PaymentProcessingService.Domain.Tests.csproj
dotnet sln add tests/Application.Tests/PaymentProcessingService.Application.Tests.csproj
```
(`dotnet sln add` también acepta varias rutas en un solo comando, separadas por espacio — se
hizo una por una para ir verificando cada paso.)

### `dotnet add <proyecto> reference <otro-proyecto>` — conectar las capas

**Qué hace:** le da permiso a un proyecto para **usar el código ya compilado** (el `.dll`) de
otro proyecto. A diferencia de `#include`/`require` en otros lenguajes (que copian texto), acá
no se copia nada: se enlaza el binario compilado. Es un cambio real dentro del `.csproj` del
primer proyecto — agrega una etiqueta `<ProjectReference>` apuntando al segundo.

**Diferencia con `dotnet sln add`:** `sln add` es organizativo (agrupa proyectos en la
solución); `add reference` es funcional (determina qué código puede usar qué otro código, y si
no está, el compilador tira error al intentar usar una clase de un proyecto no referenciado).

**Relación con la palabra clave `using` en C#:** la referencia a nivel de proyecto es el primer
paso (da el permiso general). Dentro de un archivo `.cs` puntual, para usar efectivamente una
clase de otro proyecto hace falta además escribir `using NombreDelNamespace;` al principio del
archivo — eso "activa" el permiso en ese archivo específico.

**Comandos usados, y el porqué de cada dirección:**
```bash
# Application necesita la entidad Transaction y sus reglas (viven en Domain)
dotnet add src/Application/PaymentProcessingService.Application.csproj reference src/Domain/PaymentProcessingService.Domain.csproj

# Infrastructure necesita conocer las entidades de Domain para poder persistirlas
dotnet add src/Infrastructure/PaymentProcessingService.Infrastructure.csproj reference src/Domain/PaymentProcessingService.Domain.csproj

# Infrastructure implementa las interfaces que Application define (ej. "algo que guarde una transacción")
dotnet add src/Infrastructure/PaymentProcessingService.Infrastructure.csproj reference src/Application/PaymentProcessingService.Application.csproj

# Api llama a los casos de uso definidos en Application
dotnet add src/Api/PaymentProcessingService.Api.csproj reference src/Application/PaymentProcessingService.Application.csproj

# Api necesita conocer las clases concretas de Infrastructure para configurarlas en Program.cs
# (inyección de dependencias — se documenta en detalle cuando se implemente)
dotnet add src/Api/PaymentProcessingService.Api.csproj reference src/Infrastructure/PaymentProcessingService.Infrastructure.csproj

# Los tests necesitan referenciar lo que van a probar
dotnet add tests/Domain.Tests/PaymentProcessingService.Domain.Tests.csproj reference src/Domain/PaymentProcessingService.Domain.csproj
dotnet add tests/Application.Tests/PaymentProcessingService.Application.Tests.csproj reference src/Application/PaymentProcessingService.Application.csproj
```

**Regla de oro verificada:** nunca se agregó una referencia *desde* `Domain` hacia otro
proyecto — Domain no depende de nadie (ver ADR-003 en `DECISIONES.md`). El comando `dotnet
build` final, con 0 errores, confirma que el compilador respeta exactamente ese mapa de
dependencias.

### Mapa mental — proyectos y dirección de las referencias

```
                    ┌─────────────┐
                    │     Api     │   (recibe HTTP)
                    └──────┬──────┘
                           │ puede usar
                 ┌─────────┴─────────┐
                 ▼                   ▼
         ┌───────────────┐   ┌────────────────┐
         │  Application   │   │ Infrastructure │
         │ (coordina el   │──▶│ (Postgres,     │
         │  flujo)        │   │  Acquirer Mock)│
         └───────┬────────┘   └───────┬────────┘
                  │ puede usar         │ puede usar
                  └──────────┬─────────┘
                             ▼
                      ┌─────────────┐
                      │   Domain    │   (reglas de negocio)
                      │  NO depende │
                      │  de nadie   │
                      └─────────────┘
```

Cada flecha es una referencia creada con `dotnet add reference`. Todas las flechas terminan
apuntando, directa o indirectamente, hacia `Domain`; de `Domain` no sale ninguna.

---

## La entidad `Transaction` — patrones de diseño usados y por qué

El archivo `src/Domain/Transaction.cs` no es una clase cualquiera con campos sueltos: usa
varios patrones deliberados para que sea **imposible** dejarla en un estado inválido. Esto se
llama diseñar un **"rich domain model"** (modelo de dominio rico): la clase no es solo un
contenedor de datos, también contiene las reglas que la protegen.

### 1. `{ get; private set; }` — propiedades de solo lectura desde afuera

```csharp
public decimal Amount { get; private set; }
```

Cualquier código de otro proyecto (Application, Infrastructure) puede **leer** el valor
(`transaction.Amount`), pero **no puede escribirlo directamente** desde afuera
(`transaction.Amount = -500` no compila, da error). Solo la propia clase `Transaction`, desde
sus propios métodos, puede modificar sus datos.

**Por qué:** esto es **encapsulamiento**, uno de los pilares de la programación orientada a
objetos. Si cualquier código externo pudiera modificar cualquier campo libremente, nada
impediría que alguien pusiera un monto negativo o cambiara el estado sin seguir el flujo
correcto. Al encapsular, la clase se convierte en la única responsable de mantenerse válida.

### 2. Constructor privado + método estático `Create(...)` — patrón "Factory Method"

```csharp
private Transaction() { }

public static Transaction Create(string merchantId, decimal amount, ...)
{
    if (amount <= 0) throw new ArgumentException(...);
    // ... más validaciones ...
    return new Transaction { ... };
}
```

El constructor normal (`private Transaction() { }`) está marcado como `private`, por lo tanto
**nadie fuera de la clase puede escribir `new Transaction()`**. La única forma de crear una
transacción es a través del método `Create(...)`, que:
1. Valida todos los datos de entrada primero (monto > 0, campos obligatorios, etc.).
2. Si algo es inválido, lanza una excepción y la transacción **nunca llega a existir** con
   datos corruptos.
3. Si todo es válido, arma el objeto y lo devuelve, siempre arrancando en estado `Pending`.

**Por qué:** así es **imposible** que exista en algún lugar del sistema una `Transaction` con
monto negativo o sin `merchant_id` — el propio lenguaje te lo impide, no depende de que alguien
se acuerde de validar en cada lugar donde se usa la clase.

### 3. Métodos de transición de estado (`MarkAsProcessing()`, `Approve()`, `Decline()`, `Fail()`)

```csharp
public void Approve(string acquirerResponseCode, string acquirerMessage)
{
    if (Status != TransactionStatus.Processing)
        throw new InvalidOperationException($"Solo... Estado actual: {Status}.");

    Status = TransactionStatus.Approved;
    ...
}
```

En vez de permitir cambiar el estado libremente desde afuera, cada cambio de estado tiene su
propio método, y cada uno **valida en qué estado tiene que estar la transacción antes de
permitir el cambio**. Por ejemplo, `Approve()` solo funciona si el estado actual es
`Processing` — si alguien intentara aprobar una transacción que todavía está en `Pending`
(saltándose el paso de `Processing`), el código lanza un error inmediatamente.

**Por qué:** esto convierte el diagrama de estados del PDF (`PENDING → PROCESSING →
APPROVED/DECLINED/FAILED`) en una regla real del código, no solo en una idea en un diagrama.
Es imposible que la aplicación, por un bug, salte estados o deje una transacción en un estado
que no tiene sentido.

### Resumen para la entrevista

Si te preguntan "¿por qué la clase `Transaction` está armada así, con setters privados y
métodos en vez de propiedades editables?", la respuesta corta es: **para que las reglas de
negocio (montos válidos, flujo de estados correcto) se cumplan siempre, sin depender de que
cada desarrollador se acuerde de validar manualmente en cada lugar donde se usa la entidad.**
La validación vive en un solo lugar (la propia clase), no desparramada por el sistema.

### Warning `CS8618` — Nullable Reference Types

Al compilar `Transaction.cs` por primera vez aparecieron 5 warnings `CS8618` sobre las
propiedades de tipo `string` (`MerchantId`, `Currency`, `CardLast4`, `CardBrand`,
`IdempotencyKey`).

**Qué es:** desde C# 8, el compilador tiene una característica llamada **"nullable reference
types"**: por defecto, asume que un `string` **nunca** debería valer `null`, y avisa si no
puede demostrar matemáticamente que siempre va a tener un valor asignado. Como el constructor
privado (`private Transaction() { }`) no les asignaba nada, el compilador no podía garantizar
que siempre tendrían un valor antes de usarse (aunque el método `Create(...)` sí los llena
siempre — el compilador no sigue esa lógica, solo mira el constructor).

**Solución aplicada:** darles un valor inicial en la propia declaración de la propiedad:
```csharp
public string MerchantId { get; private set; } = string.Empty;
```
Esto garantiza al compilador que, en el peor caso, el valor es un string vacío (`""`) y nunca
`null` — satisface la regla sin cambiar el comportamiento real (en la práctica, `Create(...)`
siempre sobrescribe ese valor inicial con el dato real).

**Por qué importa:** esta característica de C# existe para **prevenir en tiempo de compilación**
uno de los errores más comunes y costosos en producción en muchos lenguajes: el
`NullReferenceException` (intentar usar algo que resultó ser `null`). Tomarse el tiempo de
resolver estos warnings, en vez de ignorarlos, es buena práctica — en un proyecto real, muchos
equipos configuran el build para que estos warnings se traten como errores (`TreatWarningsAsErrors`),
justamente para no acumular deuda técnica silenciosa.

---

## Git — flujo de trabajo profesional usado en este proyecto

### Conventional Commits — formato de mensajes

Cada commit usa el formato `<tipo>(<scope opcional>): <descripción>`:

- `feat:` → funcionalidad nueva (ej. `feat(domain): agregar entidad Transaction`)
- `fix:` → corrección de un bug
- `docs:` → solo documentación, sin cambios de código
- `chore:` → tareas de configuración/mantenimiento sin lógica de negocio (ej. scaffolding)
- `test:` → agregar o modificar pruebas

**Por qué importa:** es un estándar ampliamente usado en la industria. Permite generar
changelogs automáticos, entender de un vistazo qué tipo de cambio trae cada commit sin abrir el
diff, y es lo que vas a encontrar en casi cualquier equipo profesional.

### Commits atómicos — un commit, un propósito

En vez de un commit gigante con todo el trabajo mezclado, se separó en 3 commits lógicos:
1. `docs: agregar documentacion inicial del desafio` — solo los `.md`
2. `chore: scaffolding de la solucion .NET con arquitectura en capas` — proyectos vacíos, `.sln`, `.gitignore`
3. `feat(domain): agregar entidad Transaction y TransactionStatus` — la primera lógica de negocio real

**Por qué importa:** si en el futuro hay que revertir o revisar un cambio puntual (ej. "¿cuándo
se agregó la regla de monto mínimo?"), un historial con commits chicos y bien descritos permite
encontrarlo al instante con `git log` o `git blame`. Un commit único de "todo el proyecto" no
permite esa trazabilidad.

**Técnica usada para separar commits mezclados en una misma carpeta:** cuando dos commits
distintos tenían archivos en la misma carpeta (ej. `src/Domain` tenía tanto el `.csproj` del
scaffolding como las clases de negocio), se usó `git add` especificando archivos puntuales en
vez de la carpeta completa, para controlar exactamente qué entra en cada commit.

### `git branch -M main` — por qué renombrar la rama

Git, por versiones antiguas, usa `master` como nombre de rama por defecto. La convención
moderna (y la que usa GitHub por defecto en repos nuevos) es `main`. Se renombró con
`git branch -M main` antes del primer commit para evitar inconsistencias entre el nombre local
y el del remoto.

### `git remote add origin <url>` — conectar con GitHub

Un repositorio Git puede existir sin estar conectado a ningún servicio externo (como veníamos
trabajando). `git remote add origin <url>` le agrega una referencia a un repositorio remoto
(en este caso, en GitHub), nombrada `origin` por convención. `git push -u origin main` sube los
commits locales a ese remoto, y `-u` (upstream) vincula la rama local con la remota para que
los próximos `git push` no necesiten especificar destino.

### Estrategia de branching para lo que sigue (a implementar)

De acá en adelante, en vez de seguir commiteando directo sobre `main`, cada bloque de trabajo
nuevo (persistencia, Acquirer Mock, endpoints, Docker, frontend) se va a desarrollar en su
propia rama (`feature/<nombre>`), y se va a integrar a `main` mediante un **Pull Request** en
GitHub.

**Por qué importa:** simula el flujo de trabajo de un equipo real, aunque el desarrollo sea
individual — el historial de Pull Requests queda como evidencia organizada del proceso de
desarrollo, cada uno con su propia descripción del "qué" y el "por qué", algo que un evaluador
puede revisar directamente en GitHub sin tener que leer todo el código de una sola vez.

---

## Principios SOLID aplicados en este proyecto

Repaso de los 5 principios SOLID con ejemplos concretos del código ya escrito — material directo
para la entrevista.

### S — Single Responsibility Principle (una clase, una responsabilidad)

✅ Aplicado. Cada capa tiene una sola razón para cambiar: `Transaction` solo protege sus propias
reglas de negocio; `ITransactionRepository` solo define cómo persistir transacciones; el futuro
`PaymentsController` solo traduce HTTP a llamadas de Application. Ninguna clase mezcla "validar
reglas" con "guardar en base de datos" con "responder HTTP".

### O — Open/Closed Principle (abierto a extensión, cerrado a modificación)

🟡 Se demuestra al implementar `ITransactionRepository` en Infrastructure: si mañana se
quisiera cambiar de PostgreSQL a otra base, o agregar un segundo adquirente, **no se toca
ninguna línea de Application** — solo se agrega una implementación nueva de la interfaz.
Application queda cerrado a modificación, abierto a extensión.

### L — Liskov Substitution Principle (una implementación debe poder reemplazar a otra sin romper nada)

🟡 Se demuestra en los tests: en vez de usar la implementación real con PostgreSQL, los tests de
Application van a usar una implementación "falsa" en memoria (un *fake*) que también cumple
`ITransactionRepository`. Si el código de Application funciona igual con cualquiera de las dos,
sin saber cuál está usando, eso es Liskov en la práctica.

### I — Interface Segregation Principle (interfaces chicas y específicas)

✅ Aplicado: `ITransactionRepository` tiene exactamente 5 métodos, todos relacionados a
transacciones — no es una interfaz genérica tipo `IRepository<T>` con decenas de métodos de los
cuales solo se usarían unos pocos. Ninguna implementación futura tiene que escribir código
"basura" para métodos que no necesita.

### D — Dependency Inversion Principle (depender de abstracciones, no de implementaciones concretas)

✅ Es el principio más directamente aplicado hasta ahora: Application no depende de "PostgreSQL"
ni de "Entity Framework" — depende de la interfaz `ITransactionRepository` (una abstracción).
Infrastructure, la capa de detalles técnicos, depende de esa misma abstracción para
implementarla. Es la razón técnica concreta detrás de "Domain no depende de nadie" (ver
ADR-003 en `DECISIONES.md`) — DIP es, en el fondo, el principio que justifica toda la elección
de Clean Architecture.

### Sobre escalabilidad a futuro

La arquitectura actual deja la puerta abierta a varios escenarios sin rediseñar nada:

- **Cambiar de base de datos:** solo se reemplaza la implementación de `ITransactionRepository`.
- **Agregar un adquirente real** (reemplazando el Mock): se define una interfaz similar
  (`IAcquirerClient`) e Infrastructure implementa la versión real — Application ni se entera.
- **Escalar horizontalmente** (múltiples instancias del API detrás de un balanceador): el Api no
  guarda estado en memoria, todo vive en PostgreSQL, así que es seguro levantar varias instancias.
- **Migrar a microservicios más adelante**, si el negocio realmente lo exigiera: como
  Application/Domain ya están desacoplados de Infrastructure, extraerlos a un servicio aparte
  sería mucho más barato que si todo estuviera mezclado desde el principio (ver ADR-003).

---

## Convención de idioma: código en inglés, documentación en español

- **Código** (clases, métodos, variables, nombres de archivo `.cs`): **siempre en inglés** —
  `Transaction`, `TransactionStatus`, `Create`, `Approve`, `ITransactionRepository`,
  `GetByIdAsync`. Es el estándar de facto en la industria del software, independiente del país:
  todo el ecosistema .NET/ASP.NET Core/EF Core está en inglés, y mezclar idiomas en el código se
  ve inconsistente.
- **Documentación** (`REQUERIMIENTOS.md`, `DECISIONES.md`, `CONCEPTOS.md`, `PROGRESO.md`):
  **en español**, decisión propia para que sirva como material de estudio y defensa.
- **Mensajes de error de negocio que ve el usuario final** (ej. los mensajes dentro de
  `Transaction.cs`): se mantienen en español, por ser una empresa chilena y mensajes dirigidos
  al comercio/usuario final — es una decisión válida y consistente con el contexto del negocio.

## Nuevo tipo: `record` (usado en `AcquirerResult`)

```csharp
public record AcquirerResult(bool IsApproved, string ResponseCode, string Message);
```

Un `record` es un tipo de C# pensado para representar **datos inmutables** (que no cambian una
vez creados) — ideal para modelar la respuesta de un servicio externo: una vez que el adquirente
responde, no tiene sentido que nadie modifique esa respuesta después. Es más compacto que una
clase tradicional: en una sola línea define las 3 propiedades (`IsApproved`, `ResponseCode`,
`Message`) con sus getters, constructor y comparación de igualdad incluidos automáticamente.

---

## `CreatePaymentUseCase` — el caso de uso que orquesta el flujo completo

Archivo `src/Application/UseCases/CreatePaymentUseCase.cs`. Es la clase que coordina el flujo
descrito en el PDF: recibir la solicitud → validar/crear la transacción → llamar al adquirente
→ actualizar el estado final.

### DTOs (`CreatePaymentRequest` / `CreatePaymentResponse`)

Son `record` que representan "los datos que entran" y "los datos que salen" del caso de uso.
**La entidad `Transaction` nunca se devuelve directamente hacia afuera** — se mapea a
`CreatePaymentResponse`. Esto evita que el Api (o cualquier otra capa externa) dependa de los
detalles internos del Domain; si el Domain cambia su estructura interna, el contrato hacia
afuera (`CreatePaymentResponse`) puede mantenerse estable.

### Inyección de Dependencias (Dependency Injection) — el constructor

```csharp
public CreatePaymentUseCase(ITransactionRepository transactionRepository, IAcquirerClient acquirerClient)
```

La clase **no crea** sus propias dependencias (nunca hace `new AlgoConcreto()` adentro) — las
**recibe ya armadas** desde afuera, como parámetros del constructor. Esta es la forma práctica
de aplicar el principio de Inversión de Dependencias (la "D" de SOLID): la clase solo conoce
interfaces, nunca implementaciones concretas. Quien arma y "inyecta" las implementaciones reales
es el framework (ASP.NET Core), configurado en `Program.cs` — se documenta en detalle cuando se
llegue a ese paso.

### Flujo de `ExecuteAsync`, paso a paso

1. **Idempotencia primero** (ADR-007): busca una transacción existente con la misma
   `IdempotencyKey`; si existe, la devuelve tal cual, sin crear ninguna nueva.
2. **Enmascarar la tarjeta inmediatamente:** `request.CardNumber[^4..]` — sintaxis de C# para
   "los últimos N caracteres de un string" (el `^4` cuenta posiciones desde el final). El
   número completo de tarjeta nunca se guarda en ninguna variable que sobreviva más allá de esa
   línea — consistente con la decisión de no persistir el PAN completo (ADR-006).
3. **Crear la transacción** con `Transaction.Create(...)`, ya validada por el propio Domain.
4. **Guardar** (`AddAsync`) y pasar a `Processing`.
5. **Llamar al adquirente** mediante `IAcquirerClient` (implementación pendiente, en
   Infrastructure).
6. **Aprobar o rechazar** la transacción según la respuesta, y persistir el cambio.
7. **Devolver la respuesta mapeada**, nunca la entidad interna.

### Compiló sin tener ninguna implementación real — ¿por qué?

Se esperaba que `dotnet build` fallara por no existir todavía ninguna clase que implemente
`ITransactionRepository` ni `IAcquirerClient`, pero compiló sin errores. La razón: el
compilador de C# solo exige que los **tipos usados como parámetros** existan (y existen, son
las interfaces ya creadas) — nunca intenta crear una instancia concreta de ellas dentro de esta
clase (`new ITransactionRepository()` ni siquiera es sintaxis válida, las interfaces no se
pueden instanciar).

**La distinción importante:** la falta de una implementación real recién se detecta en **tiempo
de ejecución** (`dotnet run`), cuando ASP.NET Core intente armar un `CreatePaymentUseCase` real
para atender una petición HTTP y no sepa qué clase concreta usar para cada interfaz — no en
**tiempo de compilación** (`dotnet build`), que solo valida la sintaxis y los tipos, sin
ejecutar nada.

---

## Docker y Docker Compose — levantar PostgreSQL sin instalarlo en la máquina

### ¿Qué es un contenedor, en una frase?

Un contenedor es un "mini sistema aislado" que empaqueta una aplicación (en este caso,
PostgreSQL) con todo lo que necesita para correr, sin interferir con el resto de tu máquina ni
requerir una instalación tradicional. Se puede prender y apagar como un servicio, y borrar sin
dejar rastro si ya no se necesita.

### `docker-compose.yml` — declarar qué contenedores necesita el proyecto

En vez de escribir comandos largos de Docker a mano cada vez, se describe en un archivo YAML
qué servicios hacen falta. En este proyecto, un solo servicio: `postgres`.

```yaml
services:
  postgres:
    image: postgres:16
    container_name: payment-processing-postgres
    environment:
      POSTGRES_DB: payment_processing
      POSTGRES_USER: payment_user
      POSTGRES_PASSWORD: payment_pass
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

- `image: postgres:16` — usa la imagen oficial de PostgreSQL, versión 16, descargada
  automáticamente la primera vez.
- `environment` — variables con las que PostgreSQL se autoconfigura al arrancar (nombre de la
  base, usuario, contraseña).
- `ports: "5432:5432"` — conecta el puerto 5432 de la máquina host con el puerto 5432 de dentro
  del contenedor (el que usa PostgreSQL por defecto), permitiendo conectarse a `localhost:5432`
  desde fuera del contenedor.
- `volumes` — persiste los datos de la base en un espacio separado del ciclo de vida del
  contenedor, para no perderlos si se reinicia o recrea el contenedor.

**Nota de seguridad (para mencionar en la entrevista):** las credenciales de `environment` están
escritas en texto plano en un archivo versionado en Git — válido para un desafío técnico con
datos ficticios, pero en un proyecto real nunca se haría así; se usarían variables de entorno
externas, o un gestor de secretos (Azure Key Vault, AWS Secrets Manager, etc.).

### `docker compose up -d` — levantar el contenedor

- `docker compose up` — lee `docker-compose.yml` y crea/arranca los servicios definidos.
- `-d` ("detached") — corre en segundo plano, sin bloquear la terminal mostrando logs en vivo.

### Problema encontrado: puerto 5432 ya ocupado

Al correr `docker compose up -d` por primera vez, falló con:
```
Error response from daemon: failed to set up container networking: ...
Bind for 0.0.0.0:5432 failed: port is already allocated
```

**Diagnóstico:** `docker ps` mostró que ya había **4 contenedores PostgreSQL** corriendo de
otros proyectos en la misma máquina, uno de ellos (`hotel-reservas-db-1`) usando exactamente el
puerto 5432 del host. No fue un error del proyecto — el puerto estaba legítimamente en uso por
otro servicio ya activo.

**Solución:** se cambió el mapeo de puertos en `docker-compose.yml`, de `"5432:5432"` a
`"5436:5432"` (el primer número siguiente libre, ya que 5432-5435 estaban ocupados por otros
proyectos). El formato `"host:contenedor"` significa que el puerto interno del contenedor
(5432, el estándar de PostgreSQL, que nunca cambia) se expone hacia afuera en el puerto 5436 de
la máquina. A partir de este cambio, la cadena de conexión de .NET apunta a `localhost:5436`,
no al puerto estándar.

**Por qué no se tocaron los otros contenedores:** eran de otros proyectos activos del usuario,
sin relación con este desafío — pararlos o eliminarlos podría afectar trabajo ajeno a este
repositorio. La resolución correcta ante un conflicto de puertos es casi siempre reasignar el
puerto del servicio nuevo, no desalojar servicios existentes que no son parte del proyecto.

---

## `dotnet add package` — agregar paquetes NuGet a un proyecto específico

```bash
dotnet add <proyecto>.csproj package <NombreDelPaquete>
```

Descarga un paquete de NuGet y lo agrega como dependencia del proyecto indicado (a diferencia
de `dotnet add reference`, que conecta dos proyectos *propios* entre sí, esto trae código
*externo* de terceros).

### Problema encontrado: versión de paquete incompatible con .NET 8

Al instalar `Npgsql.EntityFrameworkCore.PostgreSQL` (el paquete que traduce entre Entity
Framework Core y PostgreSQL) sin especificar versión, NuGet instaló por defecto la versión
**10.0.3**, y falló con:
```
error: NU1202: El paquete Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 no es compatible con
net8.0 (.NETCoreApp,Version=v8.0). El paquete ... admite: net10.0
```

**Causa:** `dotnet add package` sin `--version` instala la **última versión absoluta**
publicada del paquete, no necesariamente la compatible con la versión de .NET del proyecto. El
número de versión de un paquete NuGet no siempre coincide con la rama de .NET que soporta — acá
la versión 10.x del paquete está pensada para .NET 10, no para nuestro .NET 8.

**Solución:**
```bash
dotnet add src/Infrastructure/PaymentProcessingService.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.*
```
El flag `--version 8.0.*` fija la instalación a la rama de versiones `8.0.x` (la correspondiente
a .NET 8), en vez de tomar automáticamente la última versión absoluta disponible.

**Lección para la entrevista:** al agregar dependencias a un proyecto .NET, siempre verificar
que la versión del paquete sea compatible con la versión del framework objetivo (`net8.0` en
este caso) — no asumir que "la más nueva" es siempre la correcta.

### Paquetes de EF Core instalados y por qué

```bash
dotnet add src/Infrastructure/PaymentProcessingService.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.*
dotnet add src/Api/PaymentProcessingService.Api.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.*
```

- **`Npgsql.EntityFrameworkCore.PostgreSQL`** (en Infrastructure): el "traductor" entre Entity
  Framework Core y PostgreSQL específicamente. Cada motor de base de datos tiene su propio
  paquete equivalente (ej. `Microsoft.EntityFrameworkCore.SqlServer` para SQL Server).
- **`Microsoft.EntityFrameworkCore.Design`** (en Api, no en Infrastructure): habilita la
  generación de **migraciones** desde la terminal. Se instala en el **proyecto de arranque**
  (`Api`, el que tiene `Program.cs`) porque las herramientas de EF Core necesitan poder
  "levantar" la aplicación para leer su configuración al generar o aplicar migraciones.

### `dotnet tool install --global dotnet-ef`

```bash
dotnet tool list --global          # verificar qué herramientas globales hay instaladas
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef --version                # verificar que quedó instalada y accesible
```

`dotnet tool install --global` instala una herramienta de línea de comandos a nivel de **todo
el sistema** (no de un proyecto puntual) — a diferencia de `dotnet add package` (que agrega una
dependencia a un proyecto específico), esto queda disponible como el comando `dotnet ef` desde
cualquier terminal, en cualquier carpeta de la máquina. Es la herramienta oficial de EF Core
para generar y aplicar migraciones.

---

## `PaymentProcessingDbContext` — la clase central de EF Core

Archivo `src/Infrastructure/Persistence/PaymentProcessingDbContext.cs`. Hereda de `DbContext`
(clase base de EF Core) y representa la conexión a la base de datos, exponiendo "tablas" como
propiedades C# en vez de SQL escrito a mano.

- **`DbSet<Transaction> Transactions`**: representa la tabla `transactions`. Permite escribir
  `_context.Transactions.Where(...)` y que EF Core lo traduzca a SQL real.
- **`OnModelCreating` (Fluent API)**: configuración explícita de cómo se mapea cada campo, en
  vez de dejar que EF Core adivine automáticamente:
  - `HasMaxLength(...)` limita el tamaño de columnas de texto.
  - `HasColumnType("numeric(18,2)")` en `Amount`: tipo numérico preciso de PostgreSQL para
    dinero — nunca `float`/`double` para montos (tienen errores de redondeo).
  - `HasConversion<string>()` en `Status`: guarda el enum como texto legible (`'Approved'`) en
    vez del entero por defecto de EF Core — decisión cerrada en ADR-006 de `DECISIONES.md`.
  - `HasIndex(t => t.IdempotencyKey).IsUnique()`: aplica la estrategia de idempotencia
    (ADR-007) a nivel de base de datos — PostgreSQL rechaza duplicados, como segunda barrera
    además de la validación en `CreatePaymentUseCase`.

## `TransactionRepository` — implementación real de `ITransactionRepository`

Archivo `src/Infrastructure/Persistence/TransactionRepository.cs`. Implementa la interfaz
definida en Application usando el `DbContext`.

- **LINQ**: `_context.Transactions.FirstOrDefaultAsync(t => t.Id == id)` — se escribe la
  consulta como código C# (una expresión lambda), y EF Core la traduce a SQL real por detrás.
- **`SaveChangesAsync()`**: EF Core "trackea" (hace seguimiento de) los cambios en memoria con
  `AddAsync`/`Update`, pero **no escribe nada en la base hasta llamar a `SaveChangesAsync()`** —
  permite agrupar varios cambios en una sola operación si hiciera falta.

## `AcquirerMockClient` — implementación del Acquirer Mock que pide el PDF

Archivo `src/Infrastructure/Acquiring/AcquirerMockClient.cs`. Implementa `IAcquirerClient` con
una regla simple: aprueba, salvo que el monto supere 1.000.000 (simulando "fondos
insuficientes"), con un `Task.Delay(150)` simulando latencia de red real. Usa códigos de
respuesta estándar de la industria de pagos (ISO 8583): `"00"` = aprobada, `"51"` = fondos
insuficientes — no son códigos inventados.

**Se implementó antes de lo planeado:** originalmente iba después de terminar Infrastructure,
pero se adelantó porque `dotnet ef migrations add` necesita poder construir toda la aplicación
(incluyendo validar que `CreatePaymentUseCase` pueda resolver todas sus dependencias), y sin una
implementación de `IAcquirerClient` registrada, ASP.NET Core fallaba al validar el contenedor de
Inyección de Dependencias — ver más abajo.

## Migraciones de EF Core — generar y aplicar

```bash
dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project src/Api
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

- **`migrations add <Nombre>`**: genera el código que describe cómo crear/modificar las tablas
  a partir del `DbContext` configurado. `--project` indica dónde viven los archivos de
  migración (Infrastructure, junto al `DbContext`); `--startup-project` indica qué proyecto
  "arrancar" para leer la configuración (Api, que tiene `Program.cs` y la cadena de conexión).
  No toca la base de datos todavía — solo genera código C#.
- **`database update`**: ejecuta las migraciones pendientes contra la base de datos real
  conectada. Acá sí se crea físicamente la tabla.

### Problema encontrado: `Unable to resolve service for type 'IAcquirerClient'`

Al correr `migrations add` por primera vez, falló porque ASP.NET Core valida que **todo el
árbol de dependencias registrado en el contenedor de DI** se pueda construir al armar la
aplicación — y `CreatePaymentUseCase` (ya registrado) necesitaba `IAcquirerClient` en su
constructor, que todavía no tenía ninguna implementación registrada (`dotnet ef` necesita
levantar la aplicación completa para leer su configuración, no solo el `DbContext` aislado).

**Solución:** se implementó `AcquirerMockClient` y se registró en `Program.cs` con
`builder.Services.AddScoped<IAcquirerClient, AcquirerMockClient>();`, completando así el árbol
de dependencias.

**Lección:** en ASP.NET Core, un servicio registrado con una dependencia sin resolver no falla
"en silencio" — falla fuerte y explícito al construir la aplicación (fail-fast), incluso antes
de recibir ninguna petición HTTP real. Esto es una buena práctica del framework: evita
descubrir en producción, a mitad de una petición, que falta una pieza — el error aparece
apenas se intenta levantar la app.

## Registro de servicios en `Program.cs` — Inyección de Dependencias real

```csharp
builder.Services.AddDbContext<PaymentProcessingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentProcessingDb")));

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAcquirerClient, AcquirerMockClient>();
builder.Services.AddScoped<CreatePaymentUseCase>();
```

Esto es lo que resuelve, en código real, la pregunta que quedó pendiente desde que se escribió
`CreatePaymentUseCase`: "¿quién decide qué implementación concreta usar para cada interfaz?" —
la respuesta es este bloque en `Program.cs`, el único lugar de todo el proyecto que conoce tanto
las interfaces de Application como las implementaciones concretas de Infrastructure.

- **`AddDbContext<T>(...)`**: registra el `DbContext`, configurado para usar PostgreSQL
  (`UseNpgsql`) con la cadena de conexión leída de `appsettings.Development.json`.
- **`AddScoped<Interfaz, Implementación>`**: "cuando algo pida esta interfaz, dale esta
  implementación concreta". `Scoped` significa que se crea una instancia nueva por cada
  petición HTTP — ni una instancia global compartida entre todas las peticiones, ni una nueva
  cada vez que se usa dentro de la misma petición.

## Verificación directa en PostgreSQL

```bash
docker exec payment-processing-postgres psql -U payment_user -d payment_processing -c "\d transactions"
```

Comando para conectarse directamente al contenedor de Postgres y ejecutar `psql` (el cliente de
línea de comandos de PostgreSQL) dentro de él, pidiendo la estructura (`\d`) de la tabla
`transactions` — usado para confirmar, de forma independiente a EF Core, que la migración
realmente creó la tabla con las columnas e índices esperados.

---

## `PaymentsController` — el punto de entrada HTTP

Archivo `src/Api/Controllers/PaymentsController.cs`. Conecta HTTP con todo lo ya construido.

- **`[ApiController]`**: activa comportamientos automáticos de ASP.NET Core (validación
  automática del modelo, respuestas 400 automáticas si falta un campo obligatorio marcado como
  no-nullable).
- **`[Route("payments")]`**: prefijo de todas las rutas de la clase.
- **DTOs de Api (`Contracts/PaymentContracts.cs`) vs DTOs de Application**: el de Api
  (`CreatePaymentHttpRequest`) no incluye `IdempotencyKey` — se recibe por separado, vía el
  header HTTP `Idempotency-Key` (`[FromHeader(Name = "Idempotency-Key")]`), siguiendo el patrón
  estándar de la industria (Stripe lo hace igual), no como parte del cuerpo JSON.
- **Consultas (`GET`) inyectan `ITransactionRepository` directamente**, sin pasar por un caso de
  uso intermedio: al no tener ninguna regla de negocio (solo "traer datos"), crear una clase de
  caso de uso solo para reenviar la llamada no agregaría valor. Si una consulta futura necesitara
  lógica compleja, ahí sí se justificaría un caso de uso dedicado.
- **`{id:guid}`** (route constraint): restringe esa parte de la URL a que solo matchee si es un
  GUID válido — si no lo es, ASP.NET Core devuelve 404 automáticamente antes de ejecutar el
  método.
- **`CreatedAtAction(nameof(GetById), new { id = result.TransactionId }, result)`**: además de
  devolver `201 Created` con el cuerpo de la respuesta, agrega un header `Location` apuntando al
  `GET /payments/{id}` del recurso recién creado — es el estándar REST para operaciones de
  creación.
- **`Enum.TryParse<TransactionStatus>(status, ignoreCase: true, out var parsed)`**: convierte el
  texto que llega por query string (ej. `?status=Approved`) al enum, devolviendo `400 Bad
  Request` si el texto no coincide con ningún valor válido, en vez de romperse silenciosamente.

## Swagger — qué es y por qué se usó para probar

**Swagger** es una interfaz web que se genera automáticamente a partir del propio código
(atributos `[HttpPost]`/`[HttpGet]` y los tipos de los DTOs), y permite probar los endpoints
directamente desde el navegador, sin instalar nada aparte. La librería que lo genera
(`Swashbuckle.AspNetCore`) ya venía incluida por defecto en la plantilla `webapi`, y activada en
`Program.cs` (`app.UseSwagger(); app.UseSwaggerUI();`).

**Swagger vs. Postman**: ambos son herramientas estándar de la industria. Swagger no requiere
instalar nada ni configurar una colección — ideal para pruebas rápidas durante el desarrollo, y
sirve además como documentación viva de la API. Postman es más adecuado para pruebas más
elaboradas, colecciones compartidas con un equipo, o testing automatizado. Se usó Swagger acá
por ser la opción de cero fricción para la primera prueba en vivo del backend.

## Primera prueba end-to-end — `dotnet run` y resultados reales

```bash
dotnet run --project src/Api
```

Levanta el servidor Kestrel (el servidor web embebido de ASP.NET Core) en modo desarrollo,
escuchando en `http://localhost:5165` (el puerto se asigna automáticamente, puede variar).

**Pruebas realizadas contra el servidor real, con datos reales en PostgreSQL:**

| Prueba | Resultado |
|---|---|
| `POST /payments`, monto normal | `201 Created`, `status: Approved` (2), persistido en BD |
| Verificación de tarjeta enmascarada | `cardLast4` guardado, número completo nunca persistido |
| `GET /payments/{id}` | Devuelve la transacción real desde PostgreSQL |
| Reenvío de la misma petición (misma `Idempotency-Key`) | Devuelve la transacción existente — confirmado 1 sola fila en la tabla, sin duplicar |
| `POST /payments`, monto > 1.000.000 | `201 Created`, `status: Declined` (3), código `"51"` |
| `GET /payments?merchant_id=...&status=Declined` | Filtro funcionando correctamente |
| `POST /payments` sin header `Idempotency-Key` | `400 Bad Request` con mensaje explicativo |
| `GET /payments?status=NoExiste` | `400 Bad Request` con mensaje explicativo |

Verificación independiente en PostgreSQL (sin pasar por la API, para confirmar que los datos
realmente están en la base, no solo en memoria):
```bash
docker exec payment-processing-postgres psql -U payment_user -d payment_processing -c "SELECT \"Id\", \"MerchantId\", \"Amount\", \"Status\", \"IdempotencyKey\" FROM transactions;"
```

---

## Pendiente de documentar a medida que avancemos

- Inyección de dependencias en .NET (profundizar más allá de lo ya cubierto)
- xUnit — estructura de un test, `Fact` vs `Theory`
