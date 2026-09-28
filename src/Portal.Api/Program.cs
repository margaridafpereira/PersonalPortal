using Portal.Api;
using Portal.Core.Dados;
using Portal.Modules.Perfil;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBaseDeDados(builder.Configuration);
builder.Services.AddModulos(builder.Configuration);

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

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

await app.PrepararBaseDeDadosAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<Utilizador>();
app.MapPost("/api/auth/logout", async (Microsoft.AspNetCore.Identity.SignInManager<Utilizador> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPerfil();
app.MapPainel();
app.MapModulos();

app.Run();

public partial class Program;
