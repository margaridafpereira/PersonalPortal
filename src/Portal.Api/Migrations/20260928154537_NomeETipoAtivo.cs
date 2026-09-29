using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class NomeETipoAtivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "perfis",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoAtivo",
                table: "operacoes_investimento",
                type: "TEXT",
                nullable: false,
                // Operações já existentes ficam como ações; a pessoa corrige os ETF na página.
                defaultValue: "Acao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nome",
                table: "perfis");

            migrationBuilder.DropColumn(
                name: "TipoAtivo",
                table: "operacoes_investimento");
        }
    }
}
