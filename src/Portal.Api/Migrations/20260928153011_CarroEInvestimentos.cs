using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class CarroEInvestimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeccoesVistas",
                table: "preferencias",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "operacoes_investimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtilizadorId = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Momento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Ativo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Quantidade = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "TEXT", nullable: false),
                    Moeda = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ComissoesEur = table.Column<decimal>(type: "TEXT", nullable: false),
                    RetencaoFonte = table.Column<decimal>(type: "TEXT", nullable: false),
                    MoedaRetencao = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    Corretora = table.Column<string>(type: "TEXT", nullable: true),
                    IdExterno = table.Column<string>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operacoes_investimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operacoes_investimento_AspNetUsers_UtilizadorId",
                        column: x => x.UtilizadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "veiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtilizadorId = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Matricula = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    Combustivel = table.Column<string>(type: "TEXT", nullable: false),
                    DataPrimeiraMatricula = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RenovacaoSeguro = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ProximaRevisao = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ConsumoLitros100Km = table.Column<decimal>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_veiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_veiculos_AspNetUsers_UtilizadorId",
                        column: x => x.UtilizadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operacoes_investimento_UtilizadorId",
                table: "operacoes_investimento",
                column: "UtilizadorId");

            migrationBuilder.CreateIndex(
                name: "IX_operacoes_investimento_UtilizadorId_IdExterno",
                table: "operacoes_investimento",
                columns: new[] { "UtilizadorId", "IdExterno" });

            migrationBuilder.CreateIndex(
                name: "IX_veiculos_UtilizadorId",
                table: "veiculos",
                column: "UtilizadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operacoes_investimento");

            migrationBuilder.DropTable(
                name: "veiculos");

            migrationBuilder.DropColumn(
                name: "SeccoesVistas",
                table: "preferencias");
        }
    }
}
