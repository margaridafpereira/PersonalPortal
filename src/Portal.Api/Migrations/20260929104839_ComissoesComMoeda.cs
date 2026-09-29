using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class ComissoesComMoeda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ComissoesEur",
                table: "operacoes_investimento",
                newName: "Comissoes");

            migrationBuilder.AddColumn<string>(
                name: "MoedaComissoes",
                table: "operacoes_investimento",
                type: "TEXT",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoedaComissoes",
                table: "operacoes_investimento");

            migrationBuilder.RenameColumn(
                name: "Comissoes",
                table: "operacoes_investimento",
                newName: "ComissoesEur");
        }
    }
}
