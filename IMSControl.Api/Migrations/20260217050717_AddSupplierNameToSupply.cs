using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSControl.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierNameToSupply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "Supplies",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRecipeItems_SupplyId",
                table: "ProductRecipeItems",
                column: "SupplyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRecipeItems_Supplies_SupplyId",
                table: "ProductRecipeItems",
                column: "SupplyId",
                principalTable: "Supplies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRecipeItems_Supplies_SupplyId",
                table: "ProductRecipeItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductRecipeItems_SupplyId",
                table: "ProductRecipeItems");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "Supplies");
        }
    }
}
