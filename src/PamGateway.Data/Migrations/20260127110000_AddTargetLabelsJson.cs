using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamGateway.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetLabelsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabelsJson",
                table: "Targets",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Targets",
                keyColumn: "Id",
                keyValue: "SCH-249443",
                column: "LabelsJson",
                value: "{\"os\":\"windows\",\"access\":\"rdp\"}");

            migrationBuilder.UpdateData(
                table: "Targets",
                keyColumn: "Id",
                keyValue: "SCH-229958",
                column: "LabelsJson",
                value: "{\"os\":\"windows\",\"role\":\"ad\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelsJson",
                table: "Targets");
        }
    }
}
