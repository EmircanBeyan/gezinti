using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gezinti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalPlaceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Places",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Places",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Places",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Places",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Places",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Places",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Places");
        }
    }
}
