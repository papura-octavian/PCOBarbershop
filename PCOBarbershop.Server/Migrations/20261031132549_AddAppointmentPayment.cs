using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PCOBarbershop.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "Appointments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSessionId",
                table: "Appointments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StripeSessionId",
                table: "Appointments",
                column: "StripeSessionId");

            // Programarile neplatite deja existente primesc termen de plata de 30 de minute de la creare
            migrationBuilder.Sql(@"
                UPDATE ""Appointments""
                SET ""PaymentExpiresAt"" = ""CreatedAt"" + INTERVAL '30 minutes'
                WHERE ""Status"" = 'PendingPayment';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_StripeSessionId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "StripeSessionId",
                table: "Appointments");
        }
    }
}
