using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRestaurantPOS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInventarioInsumos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Productos",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<string>(
                name: "CodigoBarras",
                table: "Productos",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.CreateTable(
                name: "SecuenciasCodigo",
                columns: table => new
                {
                    IdSecuenciaCodigo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    TipoEntidad = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Prefijo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_unicode_ci"),
                    Longitud = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimoNumero = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FechaModificacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuenciasCodigo", x => x.IdSecuenciaCodigo);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "UnidadesMedida",
                columns: table => new
                {
                    IdUnidadMedida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_bin"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesMedida", x => x.IdUnidadMedida);
                    table.ForeignKey(
                        name: "FK_UnidadesMedida_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Insumos",
                columns: table => new
                {
                    IdInsumo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    IdUnidadMedida = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_bin"),
                    CodigoBarras = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_bin"),
                    Nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_unicode_ci"),
                    CostoReferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    StockMinimo = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insumos", x => x.IdInsumo);
                    table.ForeignKey(
                        name: "FK_Insumos_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Insumos_UnidadesMedida_IdUnidadMedida",
                        column: x => x.IdUnidadMedida,
                        principalTable: "UnidadesMedida",
                        principalColumn: "IdUnidadMedida",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ExistenciasInsumo",
                columns: table => new
                {
                    IdSucursal = table.Column<int>(type: "int", nullable: false),
                    IdInsumo = table.Column<int>(type: "int", nullable: false),
                    CantidadActual = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    FechaModificacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExistenciasInsumo", x => new { x.IdSucursal, x.IdInsumo });
                    table.ForeignKey(
                        name: "FK_ExistenciasInsumo_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "IdInsumo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExistenciasInsumo_Sucursales_IdSucursal",
                        column: x => x.IdSucursal,
                        principalTable: "Sucursales",
                        principalColumn: "IdSucursal",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    IdMovimientoInventario = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdSucursal = table.Column<int>(type: "int", nullable: false),
                    IdInsumo = table.Column<int>(type: "int", nullable: false),
                    IdUsuario = table.Column<int>(type: "int", nullable: false),
                    IdComanda = table.Column<long>(type: "bigint", nullable: true),
                    IdComandaDetalle = table.Column<long>(type: "bigint", nullable: true),
                    IdCaja = table.Column<int>(type: "int", nullable: true),
                    IdSesionCaja = table.Column<long>(type: "bigint", nullable: true),
                    Tipo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExistenciaAnterior = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExistenciaNueva = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Concepto = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_ci"),
                    FechaMovimiento = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.IdMovimientoInventario);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Cajas_IdCaja",
                        column: x => x.IdCaja,
                        principalTable: "Cajas",
                        principalColumn: "IdCaja",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_ComandaDetalles_IdComandaDetalle",
                        column: x => x.IdComandaDetalle,
                        principalTable: "ComandaDetalles",
                        principalColumn: "IdComandaDetalle",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Comandas_IdComanda",
                        column: x => x.IdComanda,
                        principalTable: "Comandas",
                        principalColumn: "IdComanda",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "IdInsumo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_SesionesCaja_IdSesionCaja",
                        column: x => x.IdSesionCaja,
                        principalTable: "SesionesCaja",
                        principalColumn: "IdSesionCaja",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Sucursales_IdSucursal",
                        column: x => x.IdSucursal,
                        principalTable: "Sucursales",
                        principalColumn: "IdSucursal",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "RecetasProducto",
                columns: table => new
                {
                    IdRecetaProducto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    IdInsumo = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecetasProducto", x => x.IdRecetaProducto);
                    table.ForeignKey(
                        name: "FK_RecetasProducto_Insumos_IdInsumo",
                        column: x => x.IdInsumo,
                        principalTable: "Insumos",
                        principalColumn: "IdInsumo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecetasProducto_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

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
                name: "IX_ExistenciasInsumo_IdInsumo",
                table: "ExistenciasInsumo",
                column: "IdInsumo");

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_IdEmpresa_Codigo",
                table: "Insumos",
                columns: new[] { "IdEmpresa", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_IdEmpresa_CodigoBarras",
                table: "Insumos",
                columns: new[] { "IdEmpresa", "CodigoBarras" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_IdUnidadMedida",
                table: "Insumos",
                column: "IdUnidadMedida");

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_Nombre",
                table: "Insumos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdCaja",
                table: "MovimientosInventario",
                column: "IdCaja");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdComanda",
                table: "MovimientosInventario",
                column: "IdComanda");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdComandaDetalle",
                table: "MovimientosInventario",
                column: "IdComandaDetalle");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdInsumo_FechaMovimiento",
                table: "MovimientosInventario",
                columns: new[] { "IdInsumo", "FechaMovimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdSesionCaja",
                table: "MovimientosInventario",
                column: "IdSesionCaja");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdSucursal_FechaMovimiento",
                table: "MovimientosInventario",
                columns: new[] { "IdSucursal", "FechaMovimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_IdUsuario",
                table: "MovimientosInventario",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_RecetasProducto_IdInsumo",
                table: "RecetasProducto",
                column: "IdInsumo");

            migrationBuilder.CreateIndex(
                name: "IX_RecetasProducto_IdProducto_IdInsumo",
                table: "RecetasProducto",
                columns: new[] { "IdProducto", "IdInsumo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecuenciasCodigo_IdEmpresa_TipoEntidad",
                table: "SecuenciasCodigo",
                columns: new[] { "IdEmpresa", "TipoEntidad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesMedida_IdEmpresa_Codigo",
                table: "UnidadesMedida",
                columns: new[] { "IdEmpresa", "Codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExistenciasInsumo");

            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "RecetasProducto");

            migrationBuilder.DropTable(
                name: "SecuenciasCodigo");

            migrationBuilder.DropTable(
                name: "Insumos");

            migrationBuilder.DropTable(
                name: "UnidadesMedida");

            migrationBuilder.DropIndex(
                name: "IX_Productos_Codigo",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CodigoBarras",
                table: "Productos");
        }
    }
}
