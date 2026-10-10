using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crr.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialRiskPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risk_positions",
                columns: table => new
                {
                    trade_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    book = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    commodity = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    counterparty = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    instrument_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    position = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    market_price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    pnl = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    fx_exposure = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_positions", x => x.trade_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_risk_positions_as_of_date",
                table: "risk_positions",
                column: "as_of_date");

            migrationBuilder.CreateIndex(
                name: "IX_risk_positions_book",
                table: "risk_positions",
                column: "book");

            migrationBuilder.CreateIndex(
                name: "IX_risk_positions_commodity",
                table: "risk_positions",
                column: "commodity");

            migrationBuilder.CreateIndex(
                name: "IX_risk_positions_counterparty",
                table: "risk_positions",
                column: "counterparty");

            migrationBuilder.CreateIndex(
                name: "IX_risk_positions_region",
                table: "risk_positions",
                column: "region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "risk_positions");
        }
    }
}
