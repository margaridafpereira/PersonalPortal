using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portal.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConsentimentoAssistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssistenteAceiteEm",
                table: "preferencias",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistenteAceiteEm",
                table: "preferencias");
        }
    }
}
