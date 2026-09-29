using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeccoesVistasEPerfilSemVeiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contas anteriores a SeccoesVistas ficaram com "[]": sem isto, todas as secções pareciam novas e
            // as que a pessoa tinha ocultado voltavam a aparecer. Essas contas já conheciam Apoios e Anúncios;
            // Carro e Investimentos são novas para elas e aparecem ativas uma vez, como previsto.
            migrationBuilder.Sql("""UPDATE preferencias SET SeccoesVistas = '["apoios","anuncios"]' WHERE SeccoesVistas = '[]';""");

            // Substituídos pela secção Carro (os veículos têm a data da matrícula completa).
            migrationBuilder.DropColumn(
                name: "MesMatricula",
                table: "perfis");

            migrationBuilder.DropColumn(
                name: "TemVeiculo",
                table: "perfis");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MesMatricula",
                table: "perfis",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TemVeiculo",
                table: "perfis",
                type: "INTEGER",
                nullable: true);
        }
    }
}
