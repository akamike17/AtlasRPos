using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasRestaurantPOS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDevoluciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orden ajustado para MySQL:
            // 1) Agregar primero las columnas (el índice compuesto depende de Devuelto).
            // 2) Crear el índice compuesto IX_Pagos_IdComanda_Devuelto ANTES de eliminar
            //    el índice simple IX_Pagos_IdComanda, para que la FK FK_Pagos_Comandas_IdComanda
            //    quede siempre respaldada (evita error 1553 "Cannot drop index needed in a
            //    foreign key constraint").
            migrationBuilder.AddColumn<bool>(
                name: "Devuelto",
                table: "Pagos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDevolucion",
                table: "Pagos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdUsuarioDevolucion",
                table: "Pagos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDevolucion",
                table: "Pagos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCancelacion",
                table: "Comandas",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdUsuarioCancelacion",
                table: "Comandas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelacion",
                table: "Comandas",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdComanda_Devuelto",
                table: "Pagos",
                columns: new[] { "IdComanda", "Devuelto" });

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdComanda",
                table: "Pagos");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdUsuarioDevolucion",
                table: "Pagos",
                column: "IdUsuarioDevolucion");

            migrationBuilder.CreateIndex(
                name: "IX_Comandas_IdUsuarioCancelacion",
                table: "Comandas",
                column: "IdUsuarioCancelacion");

            migrationBuilder.AddForeignKey(
                name: "FK_Comandas_Usuarios_IdUsuarioCancelacion",
                table: "Comandas",
                column: "IdUsuarioCancelacion",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Usuarios_IdUsuarioDevolucion",
                table: "Pagos",
                column: "IdUsuarioDevolucion",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comandas_Usuarios_IdUsuarioCancelacion",
                table: "Comandas");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Usuarios_IdUsuarioDevolucion",
                table: "Pagos");

            // Orden ajustado para MySQL: recrear primero el índice simple que respalda
            // la FK FK_Pagos_Comandas_IdComanda antes de eliminar el compuesto.
            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdComanda",
                table: "Pagos",
                column: "IdComanda");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdComanda_Devuelto",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Pagos_IdUsuarioDevolucion",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "IX_Comandas_IdUsuarioCancelacion",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "Devuelto",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaDevolucion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "IdUsuarioDevolucion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MotivoDevolucion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaCancelacion",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "IdUsuarioCancelacion",
                table: "Comandas");

            migrationBuilder.DropColumn(
                name: "MotivoCancelacion",
                table: "Comandas");
        }
    }
}
