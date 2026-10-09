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

## Pendiente de documentar a medida que avancemos

- Entity Framework Core — qué es, migraciones, `DbContext`
- Docker / Docker Compose — contenedores, imágenes, por qué Postgres va en un contenedor
- Minimal API vs Controllers en ASP.NET Core
- Inyección de dependencias en .NET
- xUnit — estructura de un test, `Fact` vs `Theory`
