using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRestaurantPOS.Web.Migrations
{
    /// <inheritdoc />
    public partial class CierreQuirurgicoMulticaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Productos_Codigo",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasProducto_Nombre",
                table: "CategoriasProducto");

            migrationBuilder.AddColumn<int>(
                name: "IdEmpresa",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdEmpresa",
                table: "CategoriasProducto",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdEmpresa_Codigo",
                table: "Productos",
                columns: new[] { "IdEmpresa", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_IdEmpresa_CodigoBarras",
                table: "Productos",
                columns: new[] { "IdEmpresa", "CodigoBarras" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdComanda_IdInsumo_Tipo",
                table: "MovimientosInventario",
                columns: new[] { "IdComanda", "IdInsumo", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProducto_IdEmpresa_Nombre",
                table: "CategoriasProducto",
                columns: new[] { "IdEmpresa", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoriasProducto_Empresas_IdEmpresa",
                table: "CategoriasProducto",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Empresas_IdEmpresa",
                table: "Productos",
                column: "IdEmpresa",
                principalTable: "Empresas",
                principalColumn: "IdEmpresa",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategoriasProducto_Empresas_IdEmpresa",
                table: "CategoriasProducto");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Empresas_IdEmpresa",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdEmpresa_Codigo",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_IdEmpresa_CodigoBarras",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_IdComanda_IdInsumo_Tipo",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasProducto_IdEmpresa_Nombre",
                table: "CategoriasProducto");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdEmpresa",
                table: "CategoriasProducto");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProducto_Nombre",
                table: "CategoriasProducto",
                column: "Nombre",
                unique: true);
        }
    }
}
