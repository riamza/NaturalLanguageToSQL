# Natural Language to SQL

Aplicație web care transformă întrebări scrise în **limbaj natural** (română/engleză) în **interogări SQL** executate pe o bază de date PostgreSQL. Sistemul folosește un model de limbaj (LLM) pentru a genera o **reprezentare intermediară (IR)** în format JSON, care este apoi **validată determinist** și tradusă în SQL **parametrizat**. Operațiile care modifică datele (`INSERT`, `UPDATE`, `DELETE`, `CREATE TABLE`, `UPSERT`) trec printr-un pas obligatoriu de **aprobare umană**, unde interogarea poate fi și editată înainte de execuție.

> Proiect realizat în cadrul lucrării de diplomă. Backend ASP.NET Core 8 (Clean Architecture) + Frontend React/TypeScript + PostgreSQL.

---

## Funcționalități

- Traducere din limbaj natural în SQL prin LLM, cu reprezentare intermediară (IR) în JSON.
- Validare de securitate (defense-in-depth): IR → validare schemă → SQL parametrizat.
- Introspecție dinamică a schemei bazei de date (tabele/coloane reale).
- Suport pentru `SELECT` (JOIN, GROUP BY, UNION) și operații de scriere (`INSERT`, `UPSERT`, `UPDATE`, `DELETE`, `CREATE TABLE`).
- **Aprobare umană** pentru operațiile care modifică date, cu opțiunea de a **edita manual SQL-ul** înainte de execuție.
- Cache pentru interogări reușite și istoric al interogărilor.
- Sugestii asistate de AI la erori și export/feedback pe rezultate.
- Interfață modernă: căutare, vizualizare schemă, istoric, metrici.

## Stivă tehnologică

| Componentă | Tehnologii |
|------------|-----------|
| Frontend   | React 19, TypeScript, Vite, axios, react-router-dom, recharts, lucide-react, sonner |
| Backend    | ASP.NET Core 8 (C#), Clean Architecture (Core / Application / Infrastructure / Api) |
| Bază de date | PostgreSQL 15 + Entity Framework Core 8 |
| LLM        | API compatibil OpenAI (implicit [Groq](https://groq.com), model `llama-3.3-70b-versatile`) |
| Infra      | Docker, Docker Compose, GitHub Actions (CI/CD) |

## Arhitectură (pe scurt)

Dependențele sunt orientate spre interior, conform principiilor Clean Architecture:

```
Frontend React (SPA)  ──HTTP/REST──►  Api (controllere)
                                          │
                Api ──► Application ──► Core (entități, IR)
      Infrastructure ──► Application
      Infrastructure ──► Core
```

- **Core** – entități de domeniu și reprezentarea intermediară (IR).
- **Application** – interfețe / contracte ale serviciilor.
- **Infrastructure** – implementări concrete (LLM, validare, SQL builder, acces la date).
- **Api** – controllere REST.

---

## Cerințe (prerechizite)

Pentru rularea cu Docker (recomandat):
- [Docker](https://www.docker.com/) și Docker Compose
- O cheie de API pentru LLM (ex. cont gratuit pe [Groq](https://console.groq.com))

Pentru rularea manuală (dezvoltare locală):
- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org) și npm
- [PostgreSQL 15](https://www.postgresql.org/) (sau pornit prin Docker)
- Unealta EF Core CLI: `dotnet tool install --global dotnet-ef`

---

## Rulare cu Docker (recomandat)

1. Creează un fișier `.env` în rădăcina proiectului (lângă `docker-compose.yml`):

```env
LLM_API_KEY=cheia_ta_de_api
```

2. Pornește toate serviciile:

```bash
docker compose up --build
```

3. Accesează aplicația:

| Serviciu  | URL |
|-----------|-----|
| Frontend  | http://localhost:5173 |
| Backend (API) | http://localhost:8080 |
| PostgreSQL | localhost:5433 (user `postgres`, parolă `postgrespassword`, db `NlToSQL`) |

> La prima rulare, aplică migrările EF Core pe baza de date (vezi secțiunea de mai jos), dacă tabelele nu există încă.

---

## Rulare manuală (dezvoltare locală)

### 1. Baza de date

Pornește un PostgreSQL local sau doar containerul de bază de date:

```bash
docker compose up -d db
```

Conexiunea implicită (din `backend/Api/appsettings.json`):
`Host=localhost;Port=5433;Database=NlToSQL;Username=postgres;Password=postgres`

Ajustează `ConnectionStrings:DefaultConnection` dacă folosești alte credențiale.

### 2. Backend (ASP.NET Core)

```bash
cd backend

# aplică migrările (creează tabelele + datele de test)
dotnet ef database update --project Infrastructure --startup-project Api

# pornește API-ul
dotnet run --project Api
```

API-ul pornește pe **http://localhost:5071**, iar documentația Swagger este la `http://localhost:5071/swagger`.

Configurează **cheia LLM** într-una din variante:
- în `backend/Api/appsettings.json` → `Llm:ApiKey`, sau
- prin variabilă de mediu `Llm__ApiKey`, sau
- printr-un fișier `.env` în rădăcina proiectului.

### 3. Frontend (React)

```bash
cd frontend
npm install
npm run dev
```

Frontend-ul pornește pe **http://localhost:5173** și se conectează implicit la `http://localhost:5071`.
Pentru un alt backend, setează `VITE_API_URL` (ex. într-un fișier `frontend/.env`):

```env
VITE_API_URL=http://localhost:8080
```

---

## Configurare LLM

Implicit se folosește un endpoint compatibil OpenAI (Groq). Setările sunt în `backend/Api/appsettings.json`:

```json
"Llm": {
  "Endpoint": "https://api.groq.com/openai/v1/chat/completions",
  "ModelName": "llama-3.3-70b-versatile",
  "ApiKey": ""
}
```

Poți schimba `Endpoint` și `ModelName` cu orice furnizor compatibil OpenAI.

---

## Endpoint-uri API principale

| Metodă | Rută | Descriere |
|--------|------|-----------|
| `POST` | `/api/query/ask` | Trimite o întrebare în limbaj natural și primește SQL-ul/rezultatul (sau cerere de aprobare). |
| `POST` | `/api/query/execute-approved` | Execută o operație de scriere aprobată de utilizator (eventual cu SQL editat manual). |
| `GET`  | `/api/schema` | Returnează schema bazei de date (tabele și coloane). |
| `GET`  | `/api/history` | Returnează istoricul interogărilor. |

---

## Structura proiectului

```
NaturalLanguageToSQL/
├── backend/
│   ├── Core/             # entități de domeniu + IR
│   ├── Application/      # interfețe / contracte
│   ├── Infrastructure/   # LLM, validare, SQL builder, EF Core, migrări
│   ├── Api/              # controllere REST, Program.cs
│   └── Dockerfile
├── frontend/             # aplicație React + Vite
│   └── Dockerfile
├── docker-compose.yml
└── README.md
```
