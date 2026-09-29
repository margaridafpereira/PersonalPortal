using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class CarroCompletoEAvisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApoliceSeguro",
                table: "veiculos",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cilindrada",
                table: "veiculos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmissoesCo2",
                table: "veiculos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormaCo2",
                table: "veiculos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Seguradora",
                table: "veiculos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorSeguroAnual",
                table: "veiculos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidadeCartaConducao",
                table: "perfis",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "abastecimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtilizadorId = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Quilometros = table.Column<int>(type: "INTEGER", nullable: false),
                    Litros = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    DepositoCheio = table.Column<bool>(type: "INTEGER", nullable: false),
                    Posto = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abastecimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_abastecimentos_veiculos_VeiculoId",
                        column: x => x.VeiculoId,
                        principalTable: "veiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "avisos_enviados",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UtilizadorId = table.Column<string>(type: "TEXT", nullable: false),
                    Chave = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Dias = table.Column<int>(type: "INTEGER", nullable: false),
                    EnviadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avisos_enviados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_avisos_enviados_AspNetUsers_UtilizadorId",
                        column: x => x.UtilizadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_abastecimentos_UtilizadorId_VeiculoId",
                table: "abastecimentos",
                columns: new[] { "UtilizadorId", "VeiculoId" });

            migrationBuilder.CreateIndex(
                name: "IX_abastecimentos_VeiculoId",
                table: "abastecimentos",
                column: "VeiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_avisos_enviados_UtilizadorId_Chave_Dias",
                table: "avisos_enviados",
                columns: new[] { "UtilizadorId", "Chave", "Dias" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abastecimentos");

            migrationBuilder.DropTable(
                name: "avisos_enviados");

            migrationBuilder.DropColumn(
                name: "ApoliceSeguro",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "Cilindrada",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "EmissoesCo2",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "NormaCo2",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "Seguradora",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "ValorSeguroAnual",
                table: "veiculos");

            migrationBuilder.DropColumn(
                name: "ValidadeCartaConducao",
                table: "perfis");
        }
    }
}
