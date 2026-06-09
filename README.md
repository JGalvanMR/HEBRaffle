# 📦 Nombre del Módulo: `HEBRaffle` — Sistema de Sorteo Corporativo (.NET MAUI)

---

## 🧭 Propósito

`HEBRaffle` es una aplicación móvil multiplataforma (iOS y Android) construida con .NET MAUI 9, diseñada para gestionar sorteos en eventos corporativos de HEB. Centraliza el registro de participantes, la ejecución de sorteos con aleatoriedad criptográficamente segura, el seguimiento de ganadores y la exportación de resultados a Excel — todo en modo completamente offline, sin dependencia de servicios externos.

---

## ⚙️ Responsabilidades

- Inicializar y migrar la base de datos SQLite local al arranque de la aplicación
- Registrar participantes manualmente (nombre, tienda, antigüedad, correo)
- Importar participantes en lote desde archivos `.xlsx` con detección flexible de columnas y normalización de acentos
- Detectar y rechazar duplicados (por combinación `FirstName + LastName + Store`, sin distinción de mayúsculas)
- Ejecutar sorteos con shuffle Fisher-Yates sobre `RandomNumberGenerator` (CSPRNG)
- Asignar números de premio de forma secuencial y persistir ganadores en SQLite
- Exportar participantes y ganadores a archivos `.xlsx` con formato visual HEB (rojo corporativo)
- Generar y compartir una plantilla Excel precargada para facilitar la importación masiva
- Proveer una experiencia visual animada durante el sorteo (efecto "ruleta" de nombres con desaceleración progresiva)
- Exponer estadísticas en tiempo real en el Dashboard (total, elegibles, ganadores)

---

## 🔄 Flujo de Funcionamiento

### Inicialización

1. `MauiProgram.CreateMauiApp()` registra todos los servicios, ViewModels y Views en el contenedor DI
2. `App.OnStart()` invoca `IDatabaseService.InitializeAsync()` → `AppDatabase.InitializeAsync()`
3. La base de datos se crea en `LocalApplicationData/hebraffle.db3` con modo WAL, `foreign_keys=ON` y `synchronous=NORMAL`
4. Se ejecutan `CREATE TABLE IF NOT EXISTS` para `Participants` y `Winners`
5. Se aplica migración incremental: `ALTER TABLE Participants ADD COLUMN Email TEXT DEFAULT ''` (ignorada si la columna ya existe)
6. Se crean índices: `IX_Participants_Unique` (UNIQUE sobre FirstName+LastName+Store NOCASE) e `IX_Winners_ParticipantId`
7. Cualquier error en la inicialización se reporta mediante `DisplayAlert` en el hilo de UI

### Registro Manual de Participantes

1. El usuario navega a `RegisterParticipantPage` desde Dashboard o TabBar
2. `RegisterParticipantViewModel` valida longitudes mínimas de campos y rango de `YearsInCompany`
3. Verifica unicidad mediante `IDatabaseService.ExistsDuplicateAsync()` antes de persistir
4. En modo edición (query parameter `participantId > 0`), carga datos existentes y actualiza el registro
5. Muestra toast de confirmación y limpia el formulario (nuevo) o navega atrás (edición)

### Importación desde Excel

1. El usuario descarga la plantilla desde `ImportPage` → `IImportService.GenerateTemplateAsync()` genera un `.xlsx` con columnas tipadas y filas de ejemplo, se comparte vía OS Share Sheet
2. El usuario selecciona el archivo completado vía `FilePicker`
3. `ImportService.ImportFromExcelAsync()` copia el archivo a una ruta temporal (seguridad del sandbox iOS), abre el workbook con ClosedXML
4. Se detectan columnas por nombre normalizado (sin acentos, lowercase) contra listas de aliases predefinidas
5. Por cada fila: se parsea el nombre completo, se extrae tienda, antigüedad y correo; se verifica duplicado; se inserta si es nuevo o se cuenta como `Skipped`
6. Filas con tienda vacía se reportan como `Errors` con detalle de fila; se elimina el archivo temporal
7. Si `Imported > 0`, espera 2 segundos y navega a `ParticipantsPage`

### Ejecución del Sorteo

1. `RaffleViewModel.DrawAsync()` consulta `IRaffleService.DrawWinnersAsync(count)`
2. `RaffleService` obtiene todos los participantes con `IsWinner = false`, aplica Fisher-Yates shuffle criptográfico
3. Persiste cada ganador con `InsertWinnerAsync()` (actualiza `IsWinner = 1` en el participante) y asigna `PrizeNumber = existingCount + i + 1`
4. El ViewModel construye un pool de nombres (no ganadores + ganadores) para la animación
5. `IDispatcherTimer` cicla 40 ticks a 40ms (acelerado) → ~220ms (desacelerado, ratio > 0.5) con interpolación lineal
6. Al completar los ticks, `RevealNextWinnerAsync()` muestra cada ganador en la tarjeta gold con nombre, tienda y número de premio
7. Los ganadores de sesión se acumulan en `SessionWinners` (observable) y se persisten; `FinishAnimation()` refresca contadores

### Exportación

1. `ExportService.ExportParticipantsAsync()` / `ExportWinnersAsync()` genera un `.xlsx` con cabecera roja HEB, fila de datos alternada, columnas auto-ajustadas y primera fila congelada
2. El archivo se guarda en `~/Documents/HEBRaffle/` con timestamp `yyyyMMdd_HHmmss`
3. Se comparte inmediatamente vía OS Share Sheet para descarga, correo o almacenamiento externo

---

## 📐 Reglas de Negocio

### 🔒 Restricciones

| ID | Regla | Fuente |
|----|-------|--------|
| RN-01 | Un participante no puede registrarse más de una vez con la misma combinación `FirstName + LastName + Store` (insensible a mayúsculas) | `AppDatabase`: `IX_Participants_Unique`, `ExistsDuplicateAsync()` |
| RN-02 | Un ganador queda excluido de sorteos posteriores en el mismo evento; `IsWinner = true` lo elimina del pool elegible | `AppDatabase.GetNonWinnerParticipantsAsync()` |
| RN-03 | El número de ganadores a sortear no puede superar los participantes elegibles disponibles en ese momento | `RaffleService.DrawWinnersAsync()`: `Math.Min(count, eligible.Count)` |
| RN-04 | El máximo de ganadores por sorteo está limitado a 30 | `RaffleViewModel.IncrementWinnersCommand`: `NumberOfWinners < 30` |
| RN-05 | La importación requiere obligatoriamente las columnas `NOMBRE completo` y `Tienda`; sin ellas se aborta con mensaje de error descriptivo | `ImportService.ImportFromExcelAsync()` |
| RN-06 | La importación rechaza filas con `Store` vacío (error individual, no aborta el proceso completo) | `ImportService`: `if (string.IsNullOrWhiteSpace(store))` → `errors++` |

### ✅ Validaciones

| Campo | Regla | Comportamiento ante fallo |
|-------|-------|--------------------------|
| `FirstName` | Mínimo 2 caracteres | Muestra `FirstNameError`; bloquea guardado |
| `LastName` | Mínimo 2 caracteres | Muestra `LastNameError`; bloquea guardado |
| `Store` | Mínimo 2 caracteres | Muestra `StoreError`; bloquea guardado |
| `YearsInCompany` | Entero en rango [0, 60] | Muestra `YearsError`; bloquea guardado |
| `ImportedRow.FullName` | No puede estar vacío; se ignora silenciosamente si lo está | `continue` sin contabilizar error |
| Duplicado en import | `ExistsDuplicateAsync()` retorna `true` | Cuenta como `Skipped`; no se reporta como error |
| `NumberOfWinners` | Mínimo 1 | `DecrementWinnersCommand`: `if (NumberOfWinners > 1)` |

### 🔁 Agrupaciones

- Los ganadores de una sesión se acumulan en `SessionWinners` (`ObservableCollection<Winner>`) en orden de sorteo
- Los números de premio son globalmente consecutivos: `existingWinnerCount + i + 1`, donde `existingWinnerCount` se consulta al inicio de cada sorteo, permitiendo múltiples sorteos acumulativos en el mismo evento
- El Dashboard agrupa métricas en tres contadores: Total / Elegibles / Ganadores

### ⚙️ Reglas Operativas

| Regla | Descripción |
|-------|-------------|
| Aleatoriedad criptográfica | `RandomNumberGenerator.GetInt32(n)` — no `System.Random` — garantiza equidistribución sin sesgos modulares |
| Migración no destructiva | `ALTER TABLE ADD COLUMN` con bloque `catch` silencioso si la columna ya existe; nunca DROP ni modificación de datos existentes |
| Sandbox iOS | El archivo Excel se copia a `Path.GetTempPath()` antes de abrirlo con ClosedXML para cumplir restricciones del sistema de archivos de iOS |
| Eliminación en cascada manual | `DeleteParticipantAsync()` elimina primero los registros en `Winners` antes de eliminar el participante (SQLite sin ON DELETE CASCADE automático en este driver) |
| Reset total del sorteo | `ResetRaffleAsync()` ejecuta `DELETE FROM Winners` seguido de `UPDATE Participants SET IsWinner = 0` — irreversible, sin posibilidad de restauración |
| Parseo de antigüedad | `Regex.Match(raw, @"\d+(\.\d+)?")` extrae el primer número de cadenas como "5 años" o "12 years"; sin match retorna 0 sin error |
| Normalización de nombres de columnas | Convierte a lowercase y reemplaza caracteres acentuados (á→a, é→e, etc.) antes de comparar contra aliases; permite columnas con títulos en español e inglés |

---

## 🔗 Dependencias

### Paquetes NuGet

| Paquete | Versión | Rol |
|---------|---------|-----|
| `CommunityToolkit.Maui` | 9.1.1 | Controles y helpers MAUI extendidos |
| `CommunityToolkit.Mvvm` | 8.3.2 | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` |
| `sqlite-net-pcl` | 1.9.172 | ORM SQLite async con atributos de mapeo |
| `SQLitePCLRaw.bundle_green` | 2.1.10 | Bindings nativos SQLite precompilados para ios-arm64 y android |
| `ClosedXML` | 0.102.3 | Lectura y escritura de archivos `.xlsx` (usa SixLabors en lugar de System.Drawing.Common → compatible con iOS) |
| `Microsoft.Maui.Controls` | 9.0.120 | Framework UI base |
| `Microsoft.Extensions.Logging.Debug` | 9.0.0 | Logging en modo DEBUG |

### Dependencias del Sistema

| Componente | Propósito |
|------------|-----------|
| `System.Security.Cryptography.RandomNumberGenerator` | Generación de índices aleatorios sin sesgo para el sorteo |
| `System.IO.FilePicker` (MAUI) | Selección de archivos Excel del dispositivo |
| `Share` (MAUI) | Compartición de archivos exportados vía OS Share Sheet |
| `IDispatcherTimer` (MAUI) | Motor de animación de la ruleta de nombres |
| SQLite WAL + FullMutex | Acceso concurrente seguro desde múltiples hilos |

### Módulos Internos

| Capa | Componentes |
|------|------------|
| Data | `AppDatabase` |
| Modelos | `Participant`, `Winner` |
| Servicios | `DatabaseService`, `RaffleService`, `ExportService`, `ImportService`, `NavigationService` |
| ViewModels | `BaseViewModel`, `DashboardViewModel`, `RegisterParticipantViewModel`, `ParticipantsViewModel`, `RaffleViewModel`, `WinnersViewModel`, `ImportViewModel` |
| Vistas | `DashboardPage`, `RegisterParticipantPage`, `ParticipantsPage`, `RafflePage`, `WinnersPage`, `ImportPage` |
| Converters | `BoolToColorConverter`, `InvertBoolConverter`, `NullToFalseConverter`, `IntToColorConverter`, `StringNotEmptyConverter` |

---

## ⚠️ Riesgos Técnicos

### 🔴 Crítico — Errores de compilación/runtime garantizados

**RT-01 · Campo indefinido `_database` en `DatabaseService.GetCountsAsync()`**

```csharp
// DatabaseService.cs — Línea que referencia un campo que no existe
var participants = await _database.Table<Participant>().CountAsync(); // CS0103
```

El único campo declarado es `_db`. Esta referencia genera un error de compilación (`CS0103`) que impide construir el proyecto. Ninguna funcionalidad del sistema puede compilar mientras exista este error.

**Corrección**: Reemplazar `_database` por `_db` en ambas sentencias de `GetCountsAsync()`.

---

**RT-02 · Método `DeleteAllAsync<T>()` no definido en `AppDatabase`**

```csharp
// DatabaseService.cs — Tres métodos afectados
return await _db.DeleteAllAsync<Winner>();      // ClearAllWinnersAsync
return await _db.DeleteAllAsync<Participant>(); // ClearAllParticipantsAsync
```

`AppDatabase` es una clase `sealed` que no expone `DeleteAllAsync<T>()`. Las llamadas generan errores de compilación `CS1061`. Los métodos afectados son `ClearAllWinnersAsync()`, `ClearAllParticipantsAsync()` y `ClearEverythingAsync()`, todos declarados en `IDatabaseService` e implementados con código inválido.

**Corrección**: Implementar en `AppDatabase`:
```csharp
public Task<int> DeleteAllAsync<T>() => Db.DeleteAllAsync<T>();
```

---

### 🟠 Alto — Riesgo de inconsistencia de datos

**RT-03 · Inserción de ganadores no atómica (sin transacción)**

`RaffleService.DrawWinnersAsync()` inserta cada ganador individualmente en un bucle `for`. Si el proceso es terminado por el SO (memoria insuficiente, cierre del usuario) después de persistir 2 de 5 ganadores, la base de datos queda en estado inconsistente: algunos participantes marcados como `IsWinner = true` sin registro correspondiente en `Winners`, con números de premio sin asignar.

**Corrección**: Envolver el bucle de inserción en `Db.RunInTransactionAsync()`.

---

**RT-04 · `ResetRaffleAsync()` no bloqueado por estado de animación**

`RaffleViewModel.ResetRaffleAsync()` usa `ExecuteSafeAsync`, que bloquea si `IsBusy` (operación de red/DB), pero no verifica `IsAnimating`. Un usuario que confirme el reset durante una animación activa puede producir: nombres de ganadores en la ruleta que ya no existen en DB, o `SessionWinners` con referencias a participantes cuyo `IsWinner` fue reseteado a `false` mientras se muestran en pantalla.

---

### 🟡 Medio — Degradación funcional

**RT-05 · Campo `Email` sin exposición en el formulario de registro**

`Participant.Email` existe en el modelo y en la base de datos (migración v1.1), pero `RegisterParticipantPage.xaml` no tiene ningún campo de entrada para él. Todos los participantes registrados manualmente tendrán `Email = ""` permanentemente. Solo el módulo de importación Excel puede poblar este campo.

---

**RT-06 · `ImportService.SplitFullName` no maneja convenciones de nombre español**

```csharp
var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
// "Juan Carlos Pérez López" → FirstName="Juan", LastName="Carlos Pérez López"
// "Ramón" (nombre sin apellido)  → FirstName="Ramón", LastName=""
```

El split en el primer espacio no respeta la convención de nombres compuestos ni dos apellidos. Nombres de una sola palabra generan `LastName=""`, lo que puede pasar la validación de duplicados pero producir datos de baja calidad. Adicionalmente, el `FirstName` resultante no se valida contra el mínimo de 2 caracteres que sí se aplica al registro manual.

---

**RT-07 · Permiso `READ_EXTERNAL_STORAGE` limitado a SDK ≤ 32**

```xml
<!-- AndroidManifest.xml -->
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE"
                 android:maxSdkVersion="32" />
```

En Android 13+ (SDK 33), el acceso a archivos de almacenamiento compartido requiere permisos granulares (`READ_MEDIA_IMAGES`, `READ_MEDIA_DOCUMENTS`) o el uso del sistema SAF (Storage Access Framework). El `FilePicker` de MAUI abstrae parte de esto, pero el acceso directo a paths externos puede fallar en dispositivos modernos Android.

---

**RT-08 · `IDispatcherTimer` puede quedar huérfano ante excepciones**

`StartAnimation()` crea y arranca un timer. Si `RevealNextWinnerAsync()` lanza una excepción no controlada (por ejemplo, `OperationCanceledException` no capturada), `FinishAnimation()` no se llama, `_pulseRunning` nunca se establece en `false`, y el timer continúa ejecutando `OnAnimTick` indefinidamente, generando un memory leak y posible degradación de rendimiento.

---

**RT-09 · `RaffleViewModel` registrado como Singleton preserva estado entre eventos**

```csharp
// MauiProgram.cs
builder.Services.AddSingleton<RaffleViewModel>();
builder.Services.AddSingleton<RafflePage>();
```

El propósito declarado es preservar el estado de animación, pero un Singleton que sobrevive a la vida de un evento puede mostrar ganadores de una sesión anterior, `SessionWinners` poblado de una ejecución previa, o un `NumberOfWinners` distinto de 1 al comenzar un nuevo evento. Si no se ejecuta Reset explícito, el operador puede iniciar un nuevo sorteo creyendo que el estado está limpio.

---

**RT-10 · `EligibleCount` puede estar desactualizado en `RaffleViewModel`**

`IncrementWinnersCommand` compara `NumberOfWinners < EligibleCount`, pero `EligibleCount` solo se actualiza en `OnAppearingAsync()` y al final de cada sorteo. Si entre dos taps rápidos al botón "+" otro proceso modifica la BD, el contador podría estar desactualizado, permitiendo que `NumberOfWinners` supere el conteo real de elegibles en el momento del draw (mitigado por `Math.Min` en `RaffleService`).

---

**RT-11 · Código muerto comprometido en el repositorio**

`MainPage.xaml` y `MainPage.xaml.cs` son artefactos del scaffold de plantilla MAUI; nunca son referenciados por `AppShell` ni registrados en DI. `DvlErrLog.txt` es un log de error de Visual Studio que no debe estar en control de versiones.

---

## 🧪 Casos Edge

| Escenario | Comportamiento Actual | ¿Controlado? |
|-----------|----------------------|--------------|
| Sortear con `count == EligibleCount` | Sorteo exitoso; el siguiente sorteo retorna lista vacía y mensaje "No eligible participants" | ✅ Sí |
| Archivo Excel con solo cabeceras y sin filas de datos | `ImportResult { ErrorDetails = ["File has no data rows."] }` | ✅ Sí |
| Todas las filas del Excel son duplicados | `Imported=0, Skipped=N`; no se navega a ParticipantsPage | ✅ Sí |
| `Tiempo en HEB` con valor "N/A" o texto no numérico | `ParseYears` retorna 0 sin error visible | ⚠️ Parcial — el usuario no recibe advertencia |
| Nombre compuesto de una sola palabra en el Excel | `LastName = ""`; se inserta con apellido vacío; puede duplicarse con distintos patrones de nombre | ❌ No controlado |
| Reset durante animación activa | Posible estado de UI inconsistente (ver RT-04) | ❌ No controlado |
| App en background durante inserción de ganadores | Sin transacción, puede quedar estado parcial en DB | ❌ No controlado |
| `EligibleCount = 0` al abrir RafflePage | `CanDraw = false`; botón deshabilitado; mensaje "No eligible participants" | ✅ Sí |
| iOS: primer arranque antes del primer desbloqueo del dispositivo | `NSFileProtectionCompleteUntilFirstUserAuthentication` permite acceso tras primer desbloqueo; configurado correctamente en `Entitlements.plist` | ✅ Sí |
| Archivo `.xlsx` de import con hoja en blanco como primera hoja | `ws = wb.Worksheets.FirstOrDefault(s => s.RowsUsed().Any())` salta hojas vacías y usa la primera con datos | ✅ Sí |
| Pool de animación vacío (todos los participantes son ganadores del sorteo actual) | Se añaden los ganadores al pool; `_namePool.AddRange(...)` garantiza al menos 1 elemento | ✅ Sí |

---

## 🧱 Suposiciones Detectadas

| ID | Suposición Implícita | Evidencia en el Código |
|----|---------------------|----------------------|
| SA-01 | Los nombres completos en el Excel tienen al menos un espacio que separa nombre de apellido | `SplitFullName`: `Split(' ', 2)` |
| SA-02 | La aplicación la opera una sola persona a la vez (sin concurrencia multi-usuario) | Sin locks de sesión, sin multi-user DB |
| SA-03 | Todos los participantes pertenecen a tiendas HEB (sin validación del dominio de tienda) | Campo `Store` es texto libre sin lookup |
| SA-04 | El evento completo se gestiona en una sola sesión sin reinicio de la app entre sorteos del mismo evento | `RaffleViewModel` Singleton; `PrizeNumber` es global acumulativo |
| SA-05 | El `EligibleCount` en UI es suficientemente fresco al momento de ejecutar el draw | Sin refresh previo forzado en `DrawAsync()` |
| SA-06 | El formato de la columna "Tiempo en HEB" siempre contiene al menos un dígito | `ParseYears` retorna 0 si no encuentra dígitos — puede enmascarar datos corruptos |
| SA-07 | El hash único de participante es `FirstName + LastName + Store` (no incluye `Email`, `YearsInCompany` ni fecha de registro) | Índice `IX_Participants_Unique` y `ExistsDuplicateAsync()` |
| SA-08 | Los exportes en `~/Documents/HEBRaffle/` son visibles para el usuario en la app "Archivos" de iOS | `UIFileSharingEnabled = true` en `Info.plist` |
| SA-09 | La pantalla está desbloqueada cuando se lanzan operaciones de DB en background | `NSFileProtectionCompleteUntilFirstUserAuthentication` en `Entitlements.plist` |

---

## 📈 Recomendaciones Técnicas

### 🔴 Crítico — Bloquean compilación o producen datos corruptos

**RT-01/02 · Reparar métodos rotos en `DatabaseService`**

Agregar `DeleteAllAsync<T>()` en `AppDatabase` y corregir `_database` → `_db` en `GetCountsAsync()`. Sin estas correcciones el proyecto no compila.

```csharp
// AppDatabase.cs — Agregar
public Task<int> DeleteAllAsync<T>() where T : new() =>
    Db.DeleteAllAsync<T>();
```

---

**RT-03 · Envolver la inserción de múltiples ganadores en una transacción SQLite**

```csharp
// RaffleService.DrawWinnersAsync — reemplazar el bucle actual por:
await _db.RunInTransactionAsync(async () =>
{
    for (int i = 0; i < drawn.Count; i++)
    {
        var winner = new Winner { ... };
        await _db.InsertWinnerAsync(winner);
        // ...
    }
});
```

---

### 🟠 Alto

**RT-04 · Bloquear `ResetRaffleAsync` si `IsAnimating == true`**

Cancelar la animación antes de ejecutar el reset o añadir la verificación:

```csharp
if (IsAnimating)
{
    await Shell.Current.DisplayAlert("Sorteo en curso", "Detén el sorteo antes de reiniciar.", "OK");
    return;
}
```

---

**RT-08 · Proteger `IDispatcherTimer` con try/finally**

```csharp
private async Task RevealNextWinnerAsync()
{
    try
    {
        // lógica actual
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Animation] {ex.Message}");
    }
    finally
    {
        if (_pendingIndex >= _pendingWinners.Count)
            FinishAnimation();
    }
}
```

---

### 🟡 Medio

**RT-05 · Exponer campo `Email` en `RegisterParticipantPage.xaml`**

Agregar un `Entry` bindeado a `Email` en `RegisterParticipantViewModel` para paridad con el módulo de importación.

**RT-06 · Mejorar `SplitFullName` para convenciones de nombre español**

Considerar split en las primeras 2 palabras como `FirstName` (nombre compuesto) y el resto como `LastName`:

```csharp
// Estrategia: primer token = FirstName, todo lo demás = LastName
// Adaptar según las convenciones reales del evento HEB
```

**RT-09 · Reconsiderar el ciclo de vida de `RaffleViewModel`**

Limpiar `SessionWinners` y restablecer `NumberOfWinners = 1` en `OnAppearingAsync()` si no hay ningún sorteo activo, para garantizar estado limpio en cada apertura de la pantalla sin depender de que el operador recuerde ejecutar Reset.

**RT-11 · Limpiar artefactos del repositorio**

Eliminar `MainPage.xaml`, `MainPage.xaml.cs` y `DvlErrLog.txt`. Agregar `*.txt` (o específicamente `DvlErrLog.txt`) al `.gitignore`.

---

### 🟢 Bajo — Calidad y mantenibilidad

- Agregar `ILogger` a `NavigationService` para registrar fallos de navegación silenciosos
- Extraer la configuración de la animación (`AnimTotalTicks`, `AnimFastMs`, `AnimSlowMs`) a constantes configurables o parámetros del constructor de `RaffleViewModel`
- Versionar la estructura de la DB con una tabla `_migrations` en lugar de el patrón `ALTER TABLE + catch`
- Agregar `READ_MEDIA_DOCUMENTS` en `AndroidManifest.xml` para soporte explícito de Android 13+

---

## 🧾 Resumen Ejecutivo

`HEBRaffle` es la herramienta digital que reemplaza el proceso manual de sorteo en los eventos corporativos de HEB. Permite al operador del evento registrar a los participantes directamente desde una tablet o teléfono — ya sea escribiendo los datos uno a uno, o cargando una lista completa desde una hoja de cálculo Excel — y luego ejecutar el sorteo con un solo toque. El sistema garantiza que ningún participante pueda ganar dos veces en el mismo evento y que el orden de los ganadores sea verdaderamente aleatorio, usando el mismo estándar de aleatoriedad que se emplea en criptografía.

Los resultados se pueden exportar inmediatamente a Excel con el formato visual corporativo de HEB para su archivo o distribución.

**Estado actual del sistema**: El sistema contiene **dos errores críticos de compilación** que impiden construir y ejecutar la aplicación en su estado actual — ambos relacionados con métodos de limpieza de datos en la capa de servicio (`ClearAllWinnersAsync`, `ClearEverythingAsync`, `GetCountsAsync`). Estas funciones no son utilizadas por ninguna pantalla en la versión actual de la aplicación, pero deben corregirse antes de que el proyecto pueda compilar con éxito.

Fuera de estos errores, la lógica central — registro, importación, sorteo y exportación — está correctamente implementada y es funcional. Los riesgos de mayor impacto operativo son la ausencia de transacción en la inserción múltiple de ganadores (puede generar datos inconsistentes si el dispositivo se cierra inesperadamente durante un sorteo) y la posibilidad de que el operador inicie un nuevo evento sin ejecutar un Reset explícito, viendo ganadores residuales de una sesión anterior.

---

*Documentación generada mediante análisis estático completo del código fuente de `HEBRaffle v1.0` (.NET MAUI 9 · net9.0-android · net9.0-ios)*