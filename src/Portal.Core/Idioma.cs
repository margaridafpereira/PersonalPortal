namespace Portal.Core;

/// <summary>
/// Língua dos textos que o servidor gera (cartões, apoios, prazos, erros). O frontend envia a escolha da pessoa no
/// cabeçalho <c>X-Idioma</c> (ou <c>?idioma=</c> nos links) e o middleware chama <see cref="Definir"/>; o valor acompanha
/// o pedido através dos <c>await</c>. Por omissão, português: não depende da língua do sistema operativo.
/// Só muda a língua do texto: números e datas mantêm o formato português. Os avisos por email são sempre em português.
/// </summary>
public static class Idioma
{
    private static readonly AsyncLocal<bool> Ingles = new();

    public static bool EmIngles => Ingles.Value;

    /// <summary>Vale para o fluxo atual e para o que ele chamar; não muda quem chamou.</summary>
    public static void Definir(bool ingles) => Ingles.Value = ingles;

    /// <summary>O texto na língua do pedido.</summary>
    public static string T(string pt, string en) => EmIngles ? en : pt;
}
