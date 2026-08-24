using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gezinti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceProviderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Places_Provider_ExternalId",
                table: "Places",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Places_Provider_ExternalId",
                table: "Places");
        }
    }
}
