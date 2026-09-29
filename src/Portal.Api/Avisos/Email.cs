using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace Portal.Api.Avisos;

/// <summary>
/// Envio de email, em "Email" na configuração. Modo "Pasta" (por omissão): grava cada email como ficheiro .eml,
/// que se abre no Outlook ou no browser; serve para testar sem servidor de email. Modo "Smtp": envia de verdade
/// (Gmail com palavra-passe de aplicação, Outlook, Brevo…). A palavra-passe vai para os user-secrets, nunca para o código.
/// </summary>
public sealed class ConfiguracaoEmail
{
    public string Modo { get; set; } = "Pasta";
    public string Pasta { get; set; } = "emails-enviados";
    public string Remetente { get; set; } = "portal@localhost";
    public string NomeRemetente { get; set; } = "Portal pessoal";
    public string? Servidor { get; set; }
    public int Porta { get; set; } = 587;
    public string? Utilizador { get; set; }
    public string? PalavraPasse { get; set; }
    public bool Ssl { get; set; } = true;

    /// <summary>Endereço do portal, para o link no email.</summary>
    public string UrlPortal { get; set; } = "http://localhost:5173";

    public bool EnviaDeVerdade => Modo.Equals("Smtp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Servidor);
}

public sealed record Email(string Para, string Assunto, string Html, string Texto);

public interface IEnviadorEmail
{
    Task EnviarAsync(Email email, CancellationToken ct);

    /// <summary>Descrição para a pessoa: para onde vão os emails.</summary>
    string Destino { get; }
}

public sealed class EnviadorSmtp(IOptions<ConfiguracaoEmail> opcoes, IWebHostEnvironment ambiente) : IEnviadorEmail
{
    public string Destino => opcoes.Value.EnviaDeVerdade
        ? $"enviados por {opcoes.Value.Servidor}"
        : $"gravados como ficheiros .eml em {PastaCompleta()} (modo de teste: ainda não há servidor de email configurado)";

    public async Task EnviarAsync(Email email, CancellationToken ct)
    {
        var cfg = opcoes.Value;
        using var mensagem = new MailMessage
        {
            From = new MailAddress(cfg.Remetente, cfg.NomeRemetente, Encoding.UTF8),
            Subject = email.Assunto,
            SubjectEncoding = Encoding.UTF8,
            Body = email.Texto,
            BodyEncoding = Encoding.UTF8,
        };
        mensagem.To.Add(email.Para);
        mensagem.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.Html, Encoding.UTF8, "text/html"));

        using var smtp = new SmtpClient();
        if (cfg.EnviaDeVerdade)
        {
            smtp.Host = cfg.Servidor!;
            smtp.Port = cfg.Porta;
            smtp.EnableSsl = cfg.Ssl;
            if (!string.IsNullOrWhiteSpace(cfg.Utilizador))
                smtp.Credentials = new NetworkCredential(cfg.Utilizador, cfg.PalavraPasse);
        }
        else
        {
            Directory.CreateDirectory(PastaCompleta());
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = PastaCompleta();
        }
        await smtp.SendMailAsync(mensagem, ct);
    }

    private string PastaCompleta() => Path.GetFullPath(Path.Combine(ambiente.ContentRootPath, opcoes.Value.Pasta));
}
