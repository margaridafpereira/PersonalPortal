# Desenvolvimento

## Estrutura

```
Portal.sln
src/
  Portal.Core/                 contrato IModulo, perfil, preferências, PortalDbContext
  Portal.Api/                  host: autenticação, base de dados, painel, registo dos módulos
  Modules/
    Portal.Modules.Perfil/     endpoints /api/perfil e /api/preferencias
    Portal.Modules.Apoios/     secção "Radar de apoios e prazos" + indexantes anuais
    Portal.Modules.Anuncios/   secção "Radar de anúncios"
tests/Portal.Tests/            unitários + integração (WebApplicationFactory, SQLite temporário)
web/                           frontend React + TypeScript (Vite)
```

## Correr localmente

Requisitos: .NET SDK 9, Node 20.19+.

```bash
# API em http://localhost:5227 (cria portal.db com SQLite na primeira execução)
dotnet run --project src/Portal.Api --launch-profile http

# Frontend em http://localhost:5173 (o Vite reencaminha /api para a API)
cd web && npm install && npm run dev

# Testes
dotnet test
```

## Base de dados

| Ambiente | Configuração |
|---|---|
| Local (por omissão) | `BaseDeDados:Fornecedor=Sqlite`, `ConnectionStrings:Portal=Data Source=portal.db` |
| PostgreSQL alojado (Neon, Supabase) | `BaseDeDados:Fornecedor=Postgres` e a connection string do serviço |

Para não pôr credenciais no repositório, usar variáveis de ambiente:
```bash
BaseDeDados__Fornecedor=Postgres
ConnectionStrings__Portal="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
```

Na fase 1 o esquema é criado com `EnsureCreated`. As migrações EF entram quando o esquema estabilizar.

## Como acrescentar uma secção

1. Criar o projeto `src/Modules/Portal.Modules.<Nome>` com uma referência a `Portal.Core`.
2. Implementar `IModulo`: `Id`, `Nome`, `Descricao`, `CamposPerfil`, `ObterCartaoAsync` e, se necessário, `RegistarServicos`, `ConfigurarModelo` e `MapearEndpoints`.
3. Acrescentar a instância em `Modulos.Todos` ([Modulos.cs](../src/Portal.Api/Modulos.cs)).

O painel, as preferências (mostrar, ocultar, ordenar) e os avisos de "falta preencher" funcionam sem mais alterações.

## Notas: computador da empresa (temporário)

- O NuGet usa só o nuget.org público, através do [nuget.config](../nuget.config) do repositório; o Artifactory da empresa não é usado.
- O MSBuild paralelo falha nesta máquina: usar `dotnet build -m:1 -nodeReuse:false` e `dotnet test -m:1 -nodeReuse:false`.
- O npm precisa de `NODE_OPTIONS=--use-system-ca` para aceitar o certificado da rede (definir só na sessão).
- **O proxy bloqueia o binário nativo do Vite** (`@rolldown/binding-win32-x64-msvc`). O frontend compila com `tsc`, mas `npm run dev` e `npm run build` só funcionam num computador pessoal.
