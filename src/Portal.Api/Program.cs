using System.Net;
using Portal.Api;
using Portal.Api.Avisos;
using Portal.Core.Dados;
using Portal.Modules.Assistente;
using Portal.Modules.Perfil;

var builder = WebApplication.CreateBuilder(args);

// Numa rede de empresa, o proxy do sistema pede autenticação (407) e os serviços externos (câmbios do BdP,
// preços da DGEG, fornecedor de IA) falham. As credenciais do Windows chegam; sem proxy, isto não faz nada.
if (builder.Configuration.GetValue("Rede:ProxyComCredenciaisWindows", true))
    HttpClient.DefaultProxy.Credentials = CredentialCache.DefaultNetworkCredentials;

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBaseDeDados(builder.Configuration);
builder.Services.AddModulos(builder.Configuration);
builder.Services.AddAssistente(builder.Configuration);
builder.Services.AddAvisos(builder.Configuration);
builder.Services.AddProducao(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<Utilizador>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 10;
    })
    .AddEntityFrameworkStores<PortalDbContext>();

// A API é chamada pelo frontend: um pedido sem sessão recebe 401, não um redirect para /Account/Login.
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "portal.sessao";
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

var app = builder.Build();

// Língua dos textos gerados pelo servidor (ver Portal.Core.Idioma).
app.Use(async (ctx, next) =>
{
    var idioma = ctx.Request.Headers["X-Idioma"].FirstOrDefault() ?? ctx.Request.Query["idioma"].FirstOrDefault();
    if (idioma?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true)
        Portal.Core.Idioma.Definir(true);
    await next(ctx);
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

Producao.CriarPastaDaBaseDeDados(app.Configuration);
await app.PrepararBaseDeDadosAsync();

app.UseProducao();
app.UseAuthentication();
app.UseAuthorization();
app.UseDemoSoLeitura();

app.MapGroup("/api/auth").MapIdentityApi<Utilizador>();
app.MapPost("/api/auth/logout", async (Microsoft.AspNetCore.Identity.SignInManager<Utilizador> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPerfil();
app.MapPainel();
app.MapModulos();
app.MapAssistente();
app.MapAvisos();
app.MapDemo();
app.MapFrontend();

app.Run();

public partial class Program;
