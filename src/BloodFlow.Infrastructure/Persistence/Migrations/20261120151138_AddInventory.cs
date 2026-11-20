using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloodBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BloodGroupId = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceType = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    OriginalCollectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryBasis = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false),
                    OnHandQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBatches", x => x.Id);
                    table.CheckConstraint("CK_BloodBatches_Dates", "[ExpiryDate] > [OriginalCollectionDate]");
                    table.CheckConstraint("CK_BloodBatches_ExpiryBasis", "[ExpiryBasis] IN ('Entered', 'DefaultShelfLife')");
                    table.CheckConstraint("CK_BloodBatches_Initial", "[InitialQuantity] > 0");
                    table.CheckConstraint("CK_BloodBatches_OnHand", "[OnHandQuantity] >= 0");
                    table.CheckConstraint("CK_BloodBatches_OnHandCoversReserved", "[OnHandQuantity] >= [ReservedQuantity]");
                    table.CheckConstraint("CK_BloodBatches_Reserved", "[ReservedQuantity] >= 0");
                    table.CheckConstraint("CK_BloodBatches_SourceType", "[SourceType] IN ('ManualIntake', 'Donation', 'Transfer')");
                    table.CheckConstraint("CK_BloodBatches_Status", "[Status] IN ('Active', 'Expired')");
                    table.ForeignKey(
                        name: "FK_BloodBatches_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodBatches_BloodGroups_BloodGroupId",
                        column: x => x.BloodGroupId,
                        principalTable: "BloodGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodBatches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    OnHandDelta = table.Column<int>(type: "int", nullable: false),
                    ReservedDelta = table.Column<int>(type: "int", nullable: false),
                    OnHandAfter = table.Column<int>(type: "int", nullable: false),
                    ReservedAfter = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransactions_Actor", "([ActorType] = 'User' AND [PerformedByUserId] IS NOT NULL) OR ([ActorType] = 'System' AND [PerformedByUserId] IS NULL)");
                    table.CheckConstraint("CK_InventoryTransactions_ActorType", "[ActorType] IN ('User', 'System')");
                    table.CheckConstraint("CK_InventoryTransactions_AdjustHasReason", "[TransactionType] <> 'Adjust' OR ([ReasonCode] IS NOT NULL AND [Reason] IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryTransactions_After", "[OnHandAfter] >= 0 AND [ReservedAfter] >= 0 AND [OnHandAfter] >= [ReservedAfter]");
                    table.CheckConstraint("CK_InventoryTransactions_ReasonCode", "[ReasonCode] IS NULL OR [ReasonCode] IN ('Damaged', 'Expired', 'Correction')");
                    table.CheckConstraint("CK_InventoryTransactions_Type", "[TransactionType] IN ('Intake', 'Adjust', 'Reserve', 'Release', 'Deduct')");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_BloodBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "BloodBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloodBatches_BloodGroupId",
                table: "BloodBatches",
                column: "BloodGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodBatches_CreatedByUserId",
                table: "BloodBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodBatches_OrganizationId_BloodGroupId_Status_ExpiryDate",
                table: "BloodBatches",
                columns: new[] { "OrganizationId", "BloodGroupId", "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodBatches_Status_ExpiryDate",
                table: "BloodBatches",
                columns: new[] { "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BatchId",
                table: "InventoryTransactions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ClientRequestId",
                table: "InventoryTransactions",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OrganizationId_CreatedAt",
                table: "InventoryTransactions",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PerformedByUserId",
                table: "InventoryTransactions",
                column: "PerformedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "BloodBatches");
        }
    }
}
