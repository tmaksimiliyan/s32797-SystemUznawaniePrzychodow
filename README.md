# System Uznawania Przychodów

Projekt zaliczeniowy zrealizowany w ramach przedmiotu *Aplikacje i Bazy Danych* (APBD). Aplikacja stanowi backendowe API dla hipotetycznej firmy sprzedającej oprogramowanie, której model biznesowy opiera się na dwóch równoległych źródłach dochodu: jednorazowych umowach na licencję wieczystą oraz cyklicznych subskrypcjach. Celem systemu jest nie tylko obsługa sprzedaży, lecz przede wszystkim **uznawanie przychodu** zgodnie z założeniem, że dochód można zaliczyć dopiero w momencie faktycznego spełnienia warunków umowy.

Dokument opisuje przyjęte założenia, architekturę rozwiązania, model dziedziny oraz reguły biznesowe, a także sposób uruchomienia i przetestowania aplikacji.

---

## 1. Cel i zakres projektu

Punktem wyjścia była obserwacja, że w firmach software'owych przychód nie jest pojęciem oczywistym. Podpisana umowa nie oznacza jeszcze pieniędzy w kasie, a wpłata zaliczki nie jest tożsama z przychodem, dopóki klient nie ureguluje całości należności w wyznaczonym terminie. Z tego powodu system rozróżnia dwa pojęcia:

- **przychód bieżący (rozpoznany)** — środki, które można już zaliczyć do dochodu, tj. w pełni opłacone i podpisane umowy oraz zaksięgowane płatności subskrypcyjne;
- **przychód przewidywany** — wartość, jaką firma osiągnęłaby przy założeniu, że wszystkie aktualnie negocjowane umowy zostaną podpisane, a aktywne subskrypcje będą kontynuowane.

Wokół tego rozróżnienia zbudowano pozostałe moduły: zarządzanie klientami, katalog oprogramowania wraz z systemem zniżek, obsługę umów i ich płatności, mechanizm subskrypcji oraz warstwę uwierzytelniania pracowników.

## 2. Zastosowane technologie

| Obszar | Rozwiązanie |
|---|---|
| Platforma | .NET 10 (ASP.NET Core Web API) |
| Dostęp do danych | Entity Framework Core 9 (Code First) |
| Baza danych | Microsoft SQL Server |
| Uwierzytelnianie | JWT (Bearer) + refresh token w cookie HttpOnly |
| Hashowanie haseł | BCrypt.Net-Next |
| Dokumentacja API | Swagger / OpenAPI (Swashbuckle) |
| Testy jednostkowe | xUnit + EF Core InMemory |
| Kursy walut | publiczne API Narodowego Banku Polskiego |

Wybór EF Core w podejściu Code First podyktowany był chęcią utrzymania pełnej kontroli nad modelem dziedziny w warstwie kodu C#; schemat bazy danych jest pochodną klas encji i wersjonowany za pomocą migracji.

## 3. Architektura rozwiązania

Aplikację zaprojektowano w układzie warstwowym, z wyraźnym rozdzieleniem odpowiedzialności:

```
Controllers   →  warstwa HTTP: walidacja wejścia, mapowanie wyjątków na kody odpowiedzi
Services      →  logika biznesowa i reguły domenowe
Logic         →  czyste funkcje obliczeniowe (PriceCalculator)
Data          →  AppDbContext, konfiguracja EF Core
Models        →  encje dziedziny
DTOs          →  obiekty transferu danych (wejście/wyjście API)
Exceptions    →  wyjątki domenowe
```

Kontrolery celowo pozostają „cienkie" — ich rolą jest jedynie przyjęcie żądania, delegacja do odpowiedniego serwisu i zwrócenie wyniku. Cała logika decyzyjna znajduje się w serwisach rejestrowanych w kontenerze wstrzykiwania zależności z cyklem życia `Scoped`. Serwisy komunikują się z bazą wyłącznie poprzez `AppDbContext`.

Na szczególną uwagę zasługuje klasa `PriceCalculator`, wydzielona do osobnej warstwy `Logic`. Jest to klasa statyczna, bezstanowa, pozbawiona zależności od bazy danych czy kontekstu HTTP. Takie podejście było zamierzone: kalkulacja cen to najbardziej wrażliwy fragment logiki, a jej izolacja umożliwia testowanie jednostkowe bez konieczności stawiania całej infrastruktury.

### Obsługa błędów

Zamiast zwracania kodów błędów rozsianych po całym kodzie, zdefiniowano trzy wyjątki domenowe — `NotFoundException`, `BadRequestException` oraz `ConflictException` — które serwisy zgłaszają w sytuacjach naruszenia reguł. Dzięki temu warstwa logiki operuje pojęciami dziedzinowymi, a tłumaczenie ich na odpowiednie kody HTTP (404, 400, 409) odbywa się jednolicie.

## 4. Model dziedziny

Centralnym elementem modelu jest **klient**, którego zamodelowano jako klasę abstrakcyjną `Client` z dwoma wariantami: `IndividualClient` (osoba fizyczna, identyfikowana numerem PESEL) oraz `CompanyClient` (firma, identyfikowana numerem KRS). Rozwiązanie wykorzystuje dziedziczenie odwzorowywane przez EF Core. Istotną różnicą między typami jest prawo do bycia zapomnianym: klient indywidualny może zostać oznaczony jako usunięty (`IsDeleted`), natomiast danych firmowych — zgodnie z założeniami — nie usuwa się trwale.

**Oprogramowanie** (`Software`) reprezentuje pozycję w katalogu i przechowuje roczną cenę licencji. Z każdym produktem powiązana jest kolekcja **zniżek** (`Discount`), przy czym każda zniżka obowiązuje w określonym przedziale dat i dotyczy albo umów, albo subskrypcji.

**Umowa** (`Contract`) wiąże klienta z konkretną wersją oprogramowania na ustalony czas. Przechowuje wyliczoną cenę, liczbę dodatkowych lat wsparcia, informację o podpisaniu oraz sumę dotychczasowych wpłat. Powiązane z nią **płatności** (`ContractPayment`) odzwierciedlają proces stopniowego regulowania należności.

**Subskrypcja** (`Subscription`) modeluje cykliczny dostęp do oprogramowania z określonym okresem odnowienia, a kolejne **płatności subskrypcyjne** (`SubscriptionPayment`) dokumentują opłacenie poszczególnych okresów.

**Pracownik** (`Employee`) reprezentuje użytkownika systemu wraz z rolą (`Admin` lub `Standard`) oraz danymi potrzebnymi do obsługi mechanizmu odświeżania tokenów.

## 5. Reguły biznesowe

Sercem projektu są reguły, które odróżniają go od prostego CRUD-a. Najważniejsze z nich zestawiono poniżej.

### Umowy

- Czas trwania umowy musi mieścić się w przedziale **od 3 do 30 dni**.
- Klient może wykupić od 0 do 3 **dodatkowych lat wsparcia**, z których każdy podnosi cenę bazową o 1000 PLN (rok pierwszy jest wliczony domyślnie).
- Klient nie może posiadać jednocześnie aktywnej umowy i aktywnej subskrypcji na ten sam produkt — sprawdzane są oba kierunki tej zależności.
- Należność można rozłożyć na kilka wpłat, jednak **całość musi zostać uregulowana przed datą zakończenia umowy**. Po przekroczeniu terminu wcześniejsze wpłaty traktuje się jako zwrócone, a umowa wymaga przygotowania od nowa.
- Umowa zostaje automatycznie oznaczona jako podpisana dopiero w chwili, gdy suma wpłat zrówna się z ceną.
- **Umowy podpisanej nie można usunąć** — odzwierciedla to nieodwracalność rozpoznanego przychodu.

### Naliczanie ceny

Cena umowy powstaje dwuetapowo. Najpierw do ceny bazowej stosowana jest **najwyższa aktywna zniżka promocyjna** obowiązująca w dniu zawarcia (jeżeli kilka zniżek nakłada się czasowo, wybierana jest korzystniejsza). Następnie, jeśli klient miał już wcześniej jakąkolwiek subskrypcję lub podpisaną umowę, otrzymuje dodatkowy **rabat lojalnościowy w wysokości 5%**. Analogiczny mechanizm zastosowano dla subskrypcji, z tym że rabat promocyjny obejmuje wyłącznie pierwszy okres rozliczeniowy — kolejne odnowienia korzystają już tylko z ewentualnego rabatu lojalnościowego.

### Subskrypcje

- Okres odnowienia może wynosić od 1 do 24 miesięcy.
- Płatność za kolejny okres można przyjąć dopiero po jego rozpoczęciu; próba przedwczesnej wpłaty jest odrzucana.
- Brak płatności za poprzedni okres skutkuje automatycznym **anulowaniem subskrypcji**, po czym nie da się jej już odnowić.
- Kwota płatności musi dokładnie odpowiadać należności za dany okres.

## 6. Moduł przychodów i integracja z NBP

Obliczanie przychodu bieżącego sprowadza się do zsumowania wartości podpisanych umów oraz zaksięgowanych płatności subskrypcyjnych. Przychód przewidywany dolicza do tego wartość umów będących jeszcze w negocjacji (niepodpisanych, lecz z nieprzekroczonym terminem) oraz spodziewane wpływy z aktywnych subskrypcji. Oba zestawienia można zawęzić do wybranego produktu.

Wyniki domyślnie wyrażane są w złotych, jednak system pozwala przeliczyć je na dowolną walutę. Kurs pobierany jest w czasie rzeczywistym z publicznego API Narodowego Banku Polskiego, przy użyciu `IHttpClientFactory` z nazwanym klientem. W przypadku niedostępności usługi lub podania nieznanego kodu waluty zwracany jest czytelny komunikat błędu.

## 7. Uwierzytelnianie i autoryzacja

Dostęp do wszystkich operacji biznesowych wymaga uwierzytelnienia. Zastosowano model oparty na dwóch tokenach: krótkożyjącym **access tokenie JWT**, przekazywanym w nagłówku `Authorization`, oraz **refresh tokenie** o dłuższej ważności (7 dni), przechowywanym w ciasteczku `HttpOnly`. Endpoint odświeżania wymienia ważny refresh token na nową parę, co pozwala utrzymać sesję bez ponownego logowania, jednocześnie ograniczając ekspozycję tokenu dostępowego.

Hasła nie są przechowywane jawnie — zapisywane są wyłącznie ich skróty wyznaczone algorytmem BCrypt. Część operacji (modyfikacja i usuwanie klientów) zarezerwowano dla roli `Admin`, co realizowane jest deklaratywnie za pomocą atrybutu autoryzacji na poziomie metod kontrolera.

## 8. Przegląd punktów końcowych

| Metoda | Ścieżka | Opis | Uprawnienia |
|---|---|---|---|
| POST | `/api/auth/sign-in` | Logowanie | publiczny |
| POST | `/api/auth/sign-up` | Rejestracja pracownika | publiczny |
| POST | `/api/auth/refresh` | Odświeżenie tokenu | refresh token |
| POST | `/api/auth/sign-out` | Wylogowanie | publiczny |
| POST | `/api/clients/individual` | Dodanie klienta indywidualnego | zalogowany |
| POST | `/api/clients/company` | Dodanie klienta firmowego | zalogowany |
| PUT | `/api/clients/individual/{id}` | Edycja klienta indywidualnego | Admin |
| PUT | `/api/clients/company/{id}` | Edycja klienta firmowego | Admin |
| DELETE | `/api/clients/{id}` | Usunięcie klienta | Admin |
| POST | `/api/contracts` | Utworzenie umowy | zalogowany |
| DELETE | `/api/contracts/{id}` | Usunięcie niepodpisanej umowy | zalogowany |
| POST | `/api/payments/contracts` | Płatność za umowę | zalogowany |
| POST | `/api/subscriptions` | Założenie subskrypcji | zalogowany |
| POST | `/api/subscriptions/renew` | Odnowienie subskrypcji | zalogowany |
| GET | `/api/revenue/current` | Przychód bieżący | zalogowany |
| GET | `/api/revenue/predicted` | Przychód przewidywany | zalogowany |

## 9. Przykładowe zapytania

Poniżej zebrano typowy przebieg pracy z systemem — od zalogowania, przez założenie umowy, aż po jej opłacenie i sprawdzenie przychodu. Wszystkie zapytania (poza logowaniem) wymagają nagłówka `Authorization: Bearer <accessToken>`.

### Logowanie

```http
POST /api/auth/sign-in
Content-Type: application/json

{
  "login": "admin",
  "password": "admin123"
}
```

Odpowiedź zawiera token dostępowy; refresh token ustawiany jest w ciasteczku `HttpOnly`.

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

### Dodanie klienta indywidualnego

```http
POST /api/clients/individual
Content-Type: application/json

{
  "firstName": "Anna",
  "lastName": "Nowak",
  "address": "ul. Kwiatowa 5, Poznań",
  "email": "anna.nowak@example.com",
  "phone": "501202303",
  "pesel": "92050554321"
}
```

```json
{
  "id": 3,
  "type": "Individual",
  "address": "ul. Kwiatowa 5, Poznań",
  "email": "anna.nowak@example.com",
  "phone": "501202303",
  "firstName": "Anna",
  "lastName": "Nowak",
  "pesel": "92050554321",
  "companyName": null,
  "krs": null
}
```

### Utworzenie umowy

```http
POST /api/contracts
Content-Type: application/json

{
  "clientId": 3,
  "softwareId": 1,
  "startDate": "2026-06-20",
  "endDate": "2026-07-05",
  "additionalSupportYears": 2
}
```

W odpowiedzi widać już cenę wyliczoną z uwzględnieniem dodatkowych lat wsparcia oraz obowiązujących zniżek. Umowa nie jest jeszcze podpisana, ponieważ nie wpłynęła żadna płatność.

```json
{
  "id": 7,
  "clientId": 3,
  "softwareId": 1,
  "softwareName": "FinanceManager Pro",
  "softwareVersion": "3.2",
  "startDate": "2026-06-20T00:00:00",
  "endDate": "2026-07-05T00:00:00",
  "price": 5950.00,
  "totalSupportYears": 3,
  "isSigned": false,
  "totalPaid": 0
}
```

### Płatność za umowę

```http
POST /api/payments/contracts
Content-Type: application/json

{
  "contractId": 7,
  "clientId": 3,
  "amount": 5950.00
}
```

Gdy suma wpłat zrówna się z ceną, umowa zostaje automatycznie oznaczona jako podpisana.

```json
{
  "id": 1,
  "contractId": 7,
  "clientId": 3,
  "amount": 5950.00,
  "date": "2026-06-21T10:15:00",
  "totalPaid": 5950.00,
  "contractPrice": 5950.00,
  "contractSigned": true
}
```

### Założenie subskrypcji

```http
POST /api/subscriptions
Content-Type: application/json

{
  "clientId": 3,
  "softwareId": 2,
  "name": "EduLearn – plan miesięczny",
  "renewalPeriodMonths": 1
}
```

```json
{
  "id": 4,
  "name": "EduLearn – plan miesięczny",
  "clientId": 3,
  "softwareId": 2,
  "softwareName": "EduLearn Platform",
  "renewalPeriodMonths": 1,
  "pricePerRenewal": 80.00,
  "startDate": "2026-06-21T00:00:00",
  "isActive": true
}
```

### Sprawdzenie przychodu

Domyślnie wynik podawany jest w złotych:

```http
GET /api/revenue/current
```

```json
{
  "amount": 5950.00,
  "currency": "PLN"
}
```

Wynik można przeliczyć na inną walutę za pomocą parametru `currency`; kurs pobierany jest z API NBP. Opcjonalny parametr `softwareId` zawęża zestawienie do jednego produktu:

```http
GET /api/revenue/predicted?currency=EUR&softwareId=1
```

```json
{
  "amount": 1383.72,
  "currency": "EUR"
}
```

### Przykład odpowiedzi błędnej

Naruszenie reguły biznesowej zwracane jest z odpowiednim kodem HTTP i czytelnym komunikatem — przykładowo próba utworzenia umowy na zbyt długi okres:

```json
HTTP/1.1 400 Bad Request

"Przedział czasowy kontraktu musi wynosić od 3 do 30 dni."
```

## 10. Struktura repozytorium

```
SystemUznawaniaPrzychodow/
├── Controllers/     punkty końcowe API
├── Services/        logika biznesowa (interfejs + implementacja)
├── Logic/           PriceCalculator – kalkulacja cen
├── Data/            AppDbContext
├── Models/          encje dziedziny
├── DTOs/            obiekty transferu danych
├── Exceptions/      wyjątki domenowe
├── Migrations/      migracje EF Core
└── Program.cs       konfiguracja aplikacji i dane początkowe

SystemUznawaniaPrzychodow.Tests/
└── testy jednostkowe serwisów i kalkulatora cen
```

Historia repozytorium została podzielona na gałęzie tematyczne (modele, baza danych, klienci, umowy, płatności, subskrypcje, przychody, uwierzytelnianie, testy), z których każda po zakończeniu była scalana do gałęzi głównej. Pozwala to prześledzić kolejne etapy budowy systemu.

## 11. Uruchomienie

### Wymagania wstępne

- .NET 10 SDK
- działająca instancja Microsoft SQL Server (np. w kontenerze Docker)
- narzędzie `dotnet-ef` (do ręcznego zarządzania migracjami)

Przykładowe uruchomienie bazy w Dockerze:

```bash
docker run -e "ACCEPT_EULA=Y" \
           -e "MSSQL_SA_PASSWORD=APBDprojectSystem123" \
           -p 1433:1433 \
           --name apbd-sqlserver \
           -d mcr.microsoft.com/mssql/server:2022-latest
```

Parametry połączenia (oraz konfigurację JWT) zdefiniowano w pliku `appsettings.json`. W razie potrzeby należy dostosować je do własnego środowiska.

### Krok po kroku

```bash
dotnet restore
dotnet run --project SystemUznawaniaPrzychodow
```

Migracje stosowane są automatycznie przy starcie aplikacji (`Database.MigrateAsync`), dlatego osobne `dotnet ef database update` nie jest konieczne. Po uruchomieniu w trybie deweloperskim dokumentacja Swagger dostępna jest pod adresem `/swagger`.

### Dane początkowe

Przy pierwszym uruchomieniu, o ile baza jest pusta, zasilana jest danymi testowymi: dwoma kontami pracowników (`admin`/`admin123` z rolą administratora oraz `user`/`user123` z rolą standardową), trzema pozycjami oprogramowania wraz z przykładowymi zniżkami oraz dwoma klientami. Ułatwia to natychmiastowe przetestowanie systemu bez ręcznego wprowadzania danych.

## 12. Testy

Testy jednostkowe skupiają się na regułach biznesowych, które najłatwiej naruszyć przy późniejszych zmianach. Najgęściej pokryto `PriceCalculator` — sprawdzono poprawność doliczania lat wsparcia, wyboru najlepszej zniżki, łączenia rabatu promocyjnego z lojalnościowym oraz zaokrągleń. Serwisy testowane są z wykorzystaniem dostawcy EF Core InMemory, co pozwala weryfikować logikę bez zależności od rzeczywistej bazy danych.

Uruchomienie testów:

```bash
dotnet test
```
