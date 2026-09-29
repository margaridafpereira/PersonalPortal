namespace Portal.Core.Avisos;

/// <summary>
/// Um aviso já enviado por email: um prazo (<see cref="Chave"/>) com uma antecedência (<see cref="Dias"/>).
/// Serve para nunca repetir o mesmo aviso, mesmo que o serviço corra várias vezes no mesmo dia ou reinicie.
/// </summary>
public class AvisoEnviado
{
    public long Id { get; set; }
    public string UtilizadorId { get; set; } = "";
    public string Chave { get; set; } = "";
    public int Dias { get; set; }
    public DateTimeOffset EnviadoEm { get; set; }
}
