using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACXIOMCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadConversionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConvertedFromLeadId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ConvertedFromLeadId",
                table: "Customers",
                column: "ConvertedFromLeadId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Leads_ConvertedFromLeadId",
                table: "Customers",
                column: "ConvertedFromLeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Leads_ConvertedFromLeadId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ConvertedFromLeadId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ConvertedFromLeadId",
                table: "Customers");
        }
    }
}
