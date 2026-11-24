# Gothic MOBA (BotArena)

Tryb gry w stylu MOBA (League of Legends) dla **Gothic 2 Online (G2O 0.3.9.3)**, napisany w **C# (.NET 10)**, z interfejsem 2D w **React + Vite + TypeScript** wyświetlanym w przeglądarce CEF wbudowanej w klienta G2O.

Dwie drużyny (niebiescy i czerwoni) walczą na mapie z bazami, liniami, wieżami, inhibitorami i nexusami. Co 30 s z baz wychodzą fale minionów, na mapie są obozy w dżungli, smok i Baron. Gracz wybiera bohatera w lobby, kupuje przedmioty w sklepie, zdobywa poziomy i złoto. Boty grają bohaterami i też kupują przedmioty.

## Co jest w tym repozytorium

Tylko kod C# trybu i front. To nie jest cały projekt.

| Katalog / plik | Zawartość |
| --- | --- |
| `*.cs`, `Ai/`, `Heroes/`, `Matches/`, `Lobby/`, `Shop/`, `Nav/`, `Waypoints/`, `Players/`, `Profiles/`, `Vitals/`, `Chat/`, `Auth/`, `Feedback/`, `MapEdit/` | kod serwera trybu (C#): mecze, bohaterowie i zdolności, AI minionów i mobów, sklep, nawigacja, konta, czat |
| `Persistence/`, `WorldBuilder/` | MySQL (Dapper + MySqlConnector), migracje, world builder |
| `WebCenter/` | strona administratora na hasło (serwer HTTP w trybie + `WebCenter/Web/*`) |
| `Generated/` | wygenerowany katalog przedmiotów i NPC (`GameItems`, `GameNpcs`) |
| `Ui/` | front: React + Vite + TypeScript (`src/`, `public/`, `package.json`) |
| `Ui/migrations/` | migracje SQL schematu bazy |
| `BotArena.csproj`, `BotArena.slnx` | projekt C# i solucja Visual Studio (tryb + projekt `Ui/BotArena.Ui.esproj`) |

**Czego tu nie ma**, a jest potrzebne do pełnego uruchomienia gry:

- serwer G2O z modułem C# (`GothicOnline.Server.dll`, host .NET, natywny moduł),
- skrypty Squirrel serwera i klienta (`server/bot-arena/*.nut`, `config.xml`, `scripts.xml`),
- zasoby: mapa `G2OMAPALOL`, modele i tekstury (paczki VDF), `data.xml`,
- skrypty budujące (`scripts/*.ps1`: `deploy-bot-arena.ps1`, `build-ui.ps1`, `start-db.ps1`).

Bez nich da się uruchomić **front w przeglądarce** (z atrapą klienta), ale nie da się z samego tego repozytorium zbudować ani uruchomić serwera gry.

## Wymagania

- Node.js 20+ i npm (front),
- .NET 10 SDK (tryb C#),
- Docker Desktop (baza MySQL; bez bazy tryb działa, ale dane są tylko w pamięci),
- Visual Studio 2026 (opcjonalnie; solucja `BotArena.slnx`),
- Windows x64 (serwer G2O i skrypty PowerShell).

## Front (Ui/)

Podgląd i praca nad wyglądem bez gry:

```powershell
cd Ui
npm install
npm run dev
```

Strona otwiera się pod `http://127.0.0.1:5173`. Poza grą (brak `window.squirrel`) działa **atrapa klienta** (`src/bridge/mock.ts`), która udaje serwer i dane gry. Klawisze w atrapie: Enter lub `/` czat, F6 panel admina, F7 lot kamerą, F8 builder, Tab kursor, Q/E, PgUp/PgDn.

Sprawdzenie typów i budowa:

```powershell
npm run typecheck
npm run build
```

Wynik trafia do `Ui/dist/`. W grze interfejs ładuje się jako `cef://index.html` z zasobu `BOTARENA_UI.ZIP`; paczkę buduje `scripts/build-ui.ps1` z pełnego projektu (struktura w ZIP-ie: `_WORK/DATA/WEB/<NAZWA>`, nazwy wielkimi literami).

Układ frontu:

| Ścieżka | Rola |
| --- | --- |
| `Ui/src/bridge/protocol.ts` | typowany kontrakt zdarzeń Squirrel ↔ strona |
| `Ui/src/bridge/mock.ts` | atrapa klienta do pracy w przeglądarce |
| `Ui/src/features/` | okna: `lobby`, `shop`, `hud`, `chat`, `board`, `minimap`, `builder`, `admin`, `nav`, `auth`, `feedback`, `menu`, `loading`, `world` |
| `Ui/src/data/` | katalog vobów, siatka nawigacji, miniatury |
| `Ui/src/assets/` | portrety bohaterów, ikony przedmiotów i zdolności, minimapa |

Komunikacja: Squirrel → strona przez `uiSend("nazwa", tabela)`, strona → Squirrel przez `squirrel.call("cefEvent", nazwa, ...)`.

## Serwer (C#)

Tryb to biblioteka .NET 10 (`public sealed partial class Gamemode : IGamemode`), która odwołuje się tylko do `GothicOnline.Server.dll` z modułu C# serwera G2O. Budowanie wymaga układu katalogów pełnego projektu *Gothic Online C# SDK*:

```text
<korzeń projektu>/
  examples/BotArena/        <- zawartość tego repozytorium
  server/bot-arena/         <- skrypty .nut, config.xml, scripts.xml, resources/, migrations/
  scripts/                  <- build-ui.ps1, deploy-bot-arena.ps1, start-db.ps1 ...
  runtime/server-windows-x64/   <- rozpakowany serwer G2O 0.3.9.3 z modułem C#
```

`BotArena.csproj` używa ścieżek względnych: `../../server/bot-arena/migrations/*.sql` (migracje; w tym repozytorium leżą w `Ui/migrations/`, skopiuj je do `server/bot-arena/migrations/`), `../../scripts/build-ui.ps1` (budowa frontu) i `..\..\server\bot-arena\resources\BOTARENA_UI.ZIP` (wynik).

### Budowa i wdrożenie

Zatrzymaj serwer i z korzenia projektu uruchom:

```powershell
.\scripts\deploy-bot-arena.ps1
```

Skrypt buduje front i tryb, publikuje je do `runtime/server-windows-x64` i kopiuje zasoby. Sama budowa trybu:

```powershell
dotnet build examples/BotArena/BotArena.csproj
```

Domyślnie projekt szuka SDK w `runtime/server-windows-x64/modules/csharp` (potem w `dist/modules/csharp`); inny folder: `-p:G2OSdkDirectory=<folder z GothicOnline.Server.dll>`. Budowa frontu w trakcie `dotnet build` (cel `BuildUi`) jest przyrostowa; wyłączysz ją przez `-p:BuildUi=false`. Build Debug kopiuje tryb na lokalny serwer, a działający serwer przeładowuje go bez restartu (hot reload).

### Start serwera i połączenie

```powershell
.\runtime\server-windows-x64\start-server.cmd
```

Potrzebny jest .NET 10 Runtime x64. Serwer nasłuchuje na porcie **28970** (16 miejsc, nie ogłasza się na publicznej liście). Połącz się klientem Gothic Online 0.3.9.3 z `127.0.0.1:28970` (lub adresem LAN serwera). Mapę `G2OMAPALOL.ZEN` klient pobiera sam przy pierwszym wejściu.

### Baza danych (MySQL)

Konta, profile, ranking (LP), zgłoszenia i voby z world buildera są trwałe w MySQL.

```powershell
.\scripts\start-db.ps1
```

Skrypt uruchamia kontener `mysql-server` z `docker-compose.yml` serwera i czeka, aż będzie zdrowy (`127.0.0.1:3306`). Schemat tworzy sam serwer przy starcie trybu z plików `migrations/*.sql` (kolejne zmiany schematu = nowe pliki, zastosowanych nie edytuje się).

Dane połączenia czyta `Persistence/DatabaseSettings.cs` z pliku `config/mysql.env` w katalogu roboczym serwera. Klucze: `MYSQL_HOST` (domyślnie `127.0.0.1`), `MYSQL_PORT` (`3306`), `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_DATABASE`, `MYSQL_POOLING`. Zmienne środowiskowe o tych nazwach mają pierwszeństwo. Bez `MYSQL_USER` i `MYSQL_DATABASE` tryb działa na magazynie w pamięci.

## Konfiguracja

| Plik | Co ustawia |
| --- | --- |
| `AdminSettings.cs` | admini: lista `Uids` (UID gracza widać w logu serwera: „UID: …”) albo `Password` dla komendy `/admin <hasło>`; `BanMinutes` |
| `ArenaSettings.cs` | świat, środek areny, ustawienia ogólne |
| `LaneSettings.cs` | linie, punkty marszu, skład i rytm fal minionów, wieże, obrażenia |
| `JungleSettings.cs`, `DrakeSettings.cs`, `BaronSettings.cs` | obozy w dżungli, smok, Baron |
| `Teams.cs` | drużyny |

**Zmień `AdminSettings.Uids` na własny UID.** W repozytorium jest UID autora trybu.

## Web Center (strona administratora)

Przy pierwszym starcie serwera powstaje `config/webcenter.env` z losowym hasłem (`PASSWORD`, `PORT` domyślnie 8088, `BIND` = `localhost` albo `all`; zmienne środowiskowe `WEBCENTER_PASSWORD`, `WEBCENTER_PORT`, `WEBCENTER_BIND` mają pierwszeństwo). Strona: `http://localhost:8088/`. Pokazuje przegląd serwera (RAM, CPU, tick), listę gier z mapą na żywo, czat, zgłoszenia graczy i listę graczy. Ruch to zwykłe HTTP, więc poza własną siecią użyj tunelu SSH, VPN lub proxy z TLS.

## Sterowanie i komendy

Klawisze w grze: **Enter** lub **/** czat, **F6** panel admina, **F7** latanie kamerą, **F8** world builder (admin), **F9** edytor ścieżek i obiektów (admin), **Tab** w builderze: kursor ↔ kamera.

Komendy czatu (część tylko dla admina): `/admin <hasło>`, `/ap`, `/fly`, `/kick`, `/ban`, `/zabij`, `/idz`, `/przywolaj`, `/team`, `/spawn`, `/wave`, `/reset`, `/heal`, `/arena`, `/bots`, `/stats`, `/pos`, `/instance`, `/vobs`. Lista podpowiedzi w czacie bierze się z atrybutów `[Command]` w kodzie.

## Stan

Kod C# i front kompilują się w projekcie, z którego pochodzą. Z samego tego repozytorium sprawdzony jest tylko front (`npm ci`, `npm run build`: typecheck + Vite). Duża część funkcji, zwłaszcza po stronie klienta G2O (CEF, kamera, modele, efekty), nie była sprawdzona w prawdziwym kliencie gry.

## Licencje i źródła

- [Dokumentacja G2O 0.3.9](https://docs.gothic-online.com/0.3.9/)
- Zależności NuGet: Dapper, MySqlConnector. Front: React 18, Vite 5, TypeScript.
