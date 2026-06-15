using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemUznawaniaPrzychodow.Migrations
{
    /// <inheritdoc />
    public partial class FixPeselIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_Pesel",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Pesel",
                table: "Clients",
                column: "Pesel",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_Pesel",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Pesel",
                table: "Clients",
                column: "Pesel",
                unique: true,
                filter: "[Pesel] IS NOT NULL");
        }
    }
}
