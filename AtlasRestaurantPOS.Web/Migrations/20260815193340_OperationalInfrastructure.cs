using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRestaurantPOS.Web.Migrations
{
    /// <inheritdoc />
    public partial class OperationalInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCaja",
                table: "Pagos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdMetodoPago",
                table: "Pagos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdSesionCaja",
                table: "Pagos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdUsuario",
                table: "Pagos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Folio",
                table: "Comandas",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    IdAuditoria = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: true),
                    IdSucursal = table.Column<int>(type: "int", nullable: true),
                    IdCaja = table.Column<int>(type: "int", nullable: true),
                    IdSesionCaja = table.Column<long>(type: "bigint", nullable: true),
                    IdUsuario = table.Column<int>(type: "int", nullable: true),
                    Entidad = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci"),
                    IdEntidad = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_unicode_ci"),
                    Accion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci"),
                    DatosAnteriores = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_unicode_ci"),
                    DatosNuevos = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_unicode_ci"),
                    Ip = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_unicode_ci"),
                    Fecha = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.IdAuditoria);
                    table.ForeignKey(
                        name: "FK_Auditorias_Cajas_IdCaja",
                        column: x => x.IdCaja,
                        principalTable: "Cajas",
                        principalColumn: "IdCaja",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auditorias_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auditorias_SesionesCaja_IdSesionCaja",
                        column: x => x.IdSesionCaja,
                        principalTable: "SesionesCaja",
                        principalColumn: "IdSesionCaja",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auditorias_Sucursales_IdSucursal",
                        column: x => x.IdSucursal,
                        principalTable: "Sucursales",
                        principalColumn: "IdSucursal",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auditorias_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ConfiguracionesPos",
                columns: table => new
                {
                    IdConfiguracionPos = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    IdSucursal = table.Column<int>(type: "int", nullable: true),
                    Clave = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Valor = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_unicode_ci"),
                    Descripcion = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_unicode_ci"),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPos", x => x.IdConfiguracionPos);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesPos_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesPos_Sucursales_IdSucursal",
                        column: x => x.IdSucursal,
                        principalTable: "Sucursales",
                        principalColumn: "IdSucursal",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "FoliosSecuencia",
                columns: table => new
                {
                    IdFolioSecuencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdSucursal = table.Column<int>(type: "int", nullable: false),
                    TipoDocumento = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_unicode_ci"),
                    UltimoNumero = table.Column<long>(type: "bigint", nullable: false),
                    Prefijo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_unicode_ci"),
                    Longitud = table.Column<int>(type: "int", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoliosSecuencia", x => x.IdFolioSecuencia);
                    table.ForeignKey(
                        name: "FK_FoliosSecuencia_Sucursales_IdSucursal",
                        column: x => x.IdSucursal,
                        principalTable: "Sucursales",
                        principalColumn: "IdSucursal",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Impuestos",
                columns: table => new
                {
                    IdImpuesto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Tasa = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    IncluidoEnPrecio = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Impuestos", x => x.IdImpuesto);
                    table.ForeignKey(
                        name: "FK_Impuestos_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "MetodosPago",
                columns: table => new
                {
                    IdMetodoPago = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdEmpresa = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_unicode_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_unicode_ci"),
                    RequiereReferencia = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PermiteCambio = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodosPago", x => x.IdMetodoPago);
                    table.ForeignKey(
                        name: "FK_MetodosPago_Empresas_IdEmpresa",
                        column: x => x.IdEmpresa,
                        principalTable: "Empresas",
                        principalColumn: "IdEmpresa",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "ProductosImpuestos",
                columns: table => new
                {
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    IdImpuesto = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosImpuestos", x => new { x.IdProducto, x.IdImpuesto });
                    table.ForeignKey(
                        name: "FK_ProductosImpuestos_Impuestos_IdImpuesto",
                        column: x => x.IdImpuesto,
                        principalTable: "Impuestos",
                        principalColumn: "IdImpuesto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductosImpuestos_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdCaja",
                table: "Pagos",
                column: "IdCaja");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdMetodoPago",
                table: "Pagos",
                column: "IdMetodoPago");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdSesionCaja",
                table: "Pagos",
                column: "IdSesionCaja");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdUsuario",
                table: "Pagos",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Comandas_IdSucursal_Folio",
                table: "Comandas",
                columns: new[] { "IdSucursal", "Folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Entidad_IdEntidad",
                table: "Auditorias",
                columns: new[] { "Entidad", "IdEntidad" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Fecha",
                table: "Auditorias",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_IdCaja",
                table: "Auditorias",
                column: "IdCaja");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_IdEmpresa",
                table: "Auditorias",
                column: "IdEmpresa");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_IdSesionCaja",
                table: "Auditorias",
                column: "IdSesionCaja");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_IdSucursal",
                table: "Auditorias",
                column: "IdSucursal");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_IdUsuario_Fecha",
                table: "Auditorias",
                columns: new[] { "IdUsuario", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPos_IdEmpresa_IdSucursal_Clave",
                table: "ConfiguracionesPos",
                columns: new[] { "IdEmpresa", "IdSucursal", "Clave" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPos_IdSucursal",
                table: "ConfiguracionesPos",
                column: "IdSucursal");

            migrationBuilder.CreateIndex(
                name: "IX_FoliosSecuencia_IdSucursal_TipoDocumento",
                table: "FoliosSecuencia",
                columns: new[] { "IdSucursal", "TipoDocumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Impuestos_IdEmpresa_Nombre",
                table: "Impuestos",
                columns: new[] { "IdEmpresa", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetodosPago_IdEmpresa_Codigo",
                table: "MetodosPago",
                columns: new[] { "IdEmpresa", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductosImpuestos_IdImpuesto",
                table: "ProductosImpuestos",
                column: "IdImpuesto");

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Cajas_IdCaja",
                table: "Pagos",
                column: "IdCaja",
                principalTable: "Cajas",
                principalColumn: "IdCaja",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_MetodosPago_IdMetodoPago",
                table: "Pagos",
                column: "IdMetodoPago",
                principalTable: "MetodosPago",
                principalColumn: "IdMetodoPago",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_SesionesCaja_IdSesionCaja",
                table: "Pagos",
                column: "IdSesionCaja",
                principalTable: "SesionesCaja",
                principalColumn: "IdSesionCaja",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Usuarios_IdUsuario",
                table: "Pagos",
                column: "IdUsuario",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Cajas_IdCaja",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_MetodosPago_IdMetodoPago",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_SesionesCaja_IdSesionCaja",
                table: "Pagos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Usuarios_IdUsuario",
                table: "Pagos");

            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "ConfiguracionesPos");

            migrationBuilder.DropTable(
                name: "FoliosSecuencia");

            migrationBuilder.DropTable(
                name: "MetodosPago");

            migrationBuilder.DropTable(
                name: "ProductosImpuestos");

            migrationBuilder.DropTable(
                name: "Impuestos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdCaja",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdMetodoPago",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdSesionCaja",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdUsuario",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Comandas_IdSucursal_Folio",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "IdCaja",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdMetodoPago",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdSesionCaja",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdUsuario",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "Folio",
                table: "Comandas");
        }
    }
}
