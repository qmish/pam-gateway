using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamGateway.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyTargetLabelSelector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetLabelSelectorJson",
                table: "Policies",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Policies",
                keyColumn: "Id",
                keyValue: "POL-RDP-REMOTE",
                column: "TargetLabelSelectorJson",
                value: "{\"os\":\"windows\",\"access\":\"rdp\"}");

            migrationBuilder.UpdateData(
                table: "Policies",
                keyColumn: "Id",
                keyValue: "POL-SSH-SERVER",
                column: "TargetLabelSelectorJson",
                value: "{\"os\":\"linux\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetLabelSelectorJson",
                table: "Policies");
        }
    }
}
