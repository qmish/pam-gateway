using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamGateway.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetConnectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "Targets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "Targets",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Targets",
                keyColumn: "Id",
                keyValue: "SCH-249443",
                columns: new[] { "Host", "Port" },
                values: new object[] { "rdp.local", 3389 });

            migrationBuilder.UpdateData(
                table: "Targets",
                keyColumn: "Id",
                keyValue: "SCH-229958",
                columns: new[] { "Host", "Port" },
                values: new object[] { "ad.local", 389 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Host",
                table: "Targets");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "Targets");
        }
    }
}
