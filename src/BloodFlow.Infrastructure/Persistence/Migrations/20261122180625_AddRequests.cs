using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "BloodRequestNumbers");

            migrationBuilder.AddColumn<int>(
                name: "ReservationLineId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_BloodBatches_Id_OrganizationId_BloodGroupId",
                table: "BloodBatches",
                columns: new[] { "Id", "OrganizationId", "BloodGroupId" });

            migrationBuilder.CreateTable(
                name: "BloodRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    VerificationStatus = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    BloodGroupId = table.Column<byte>(type: "tinyint", nullable: false),
                    UnitsRequested = table.Column<int>(type: "int", nullable: false),
                    NeededBy = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DestinationHospitalId = table.Column<int>(type: "int", nullable: false),
                    DestinationOrgType = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequesterCapacity = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    PatientFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatientAnonymizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelationshipToPatient = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmergencyOverrideUsed = table.Column<bool>(type: "bit", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodRequests", x => x.Id);
                    table.UniqueConstraint("AK_BloodRequests_Id_BloodGroupId", x => new { x.Id, x.BloodGroupId });
                    table.UniqueConstraint("AK_BloodRequests_Id_RequestType", x => new { x.Id, x.RequestType });
                    table.CheckConstraint("CK_BloodRequests_Anonymized", "[PatientAnonymizedAt] IS NULL OR [PatientFullName] IS NULL");
                    table.CheckConstraint("CK_BloodRequests_Capacity", "[RequesterCapacity] IN ('HospitalStaff', 'Patient', 'Representative')");
                    table.CheckConstraint("CK_BloodRequests_DestinationIsHospital", "[DestinationOrgType] = 'Hospital'");
                    table.CheckConstraint("CK_BloodRequests_FulfilledIsVerified", "[Status] NOT IN ('Fulfilled', 'Received') OR [VerificationStatus] = 'Verified' OR [EmergencyOverrideUsed] = 1");
                    table.CheckConstraint("CK_BloodRequests_Status", "[Status] IN ('Pending', 'Approved', 'Fulfilled', 'Received', 'Rejected', 'Cancelled')");
                    table.CheckConstraint("CK_BloodRequests_Type", "[RequestType] IN ('Normal', 'Emergency')");
                    table.CheckConstraint("CK_BloodRequests_Units", "[UnitsRequested] > 0");
                    table.CheckConstraint("CK_BloodRequests_Verification", "[VerificationStatus] IN ('Unverified', 'Verified')");
                    table.ForeignKey(
                        name: "FK_BloodRequests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodRequests_AspNetUsers_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodRequests_BloodGroups_BloodGroupId",
                        column: x => x.BloodGroupId,
                        principalTable: "BloodGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodRequests_Organizations_DestinationHospitalId_DestinationOrgType",
                        columns: x => new { x.DestinationHospitalId, x.DestinationOrgType },
                        principalTable: "Organizations",
                        principalColumns: new[] { "Id", "Type" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodRequestStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodRequestStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_BloodRequestStatusHistory_Transition", "([FromStatus] IS NULL AND [ToStatus] = 'Pending') OR ([FromStatus] = 'Pending' AND [ToStatus] IN ('Approved', 'Rejected', 'Cancelled')) OR ([FromStatus] = 'Approved' AND [ToStatus] IN ('Fulfilled', 'Cancelled')) OR ([FromStatus] = 'Fulfilled' AND [ToStatus] = 'Received')");
                    table.ForeignKey(
                        name: "FK_BloodRequestStatusHistory_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloodRequestStatusHistory_BloodRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "BloodRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    OverriddenByUserId = table.Column<int>(type: "int", nullable: false),
                    ReasonText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OverriddenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewStatus = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyOverrides", x => x.Id);
                    table.CheckConstraint("CK_EmergencyOverrides_Review", "[ReviewStatus] IN ('PendingReview', 'Reviewed')");
                    table.CheckConstraint("CK_EmergencyOverrides_Type", "[RequestType] = 'Emergency'");
                    table.ForeignKey(
                        name: "FK_EmergencyOverrides_AspNetUsers_OverriddenByUserId",
                        column: x => x.OverriddenByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyOverrides_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyOverrides_BloodRequests_RequestId_RequestType",
                        columns: x => new { x.RequestId, x.RequestType },
                        principalTable: "BloodRequests",
                        principalColumns: new[] { "Id", "RequestType" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyOverrides_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    BloodGroupId = table.Column<byte>(type: "tinyint", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.UniqueConstraint("AK_Reservations_Id_OrganizationId_BloodGroupId", x => new { x.Id, x.OrganizationId, x.BloodGroupId });
                    table.CheckConstraint("CK_Reservations_ClosedAt", "([Status] = 'Active' AND [ClosedAt] IS NULL) OR ([Status] <> 'Active' AND [ClosedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_Reservations_Status", "[Status] IN ('Active', 'Consumed', 'Released')");
                    table.ForeignKey(
                        name: "FK_Reservations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_BloodRequests_RequestId_BloodGroupId",
                        columns: x => new { x.RequestId, x.BloodGroupId },
                        principalTable: "BloodRequests",
                        principalColumns: new[] { "Id", "BloodGroupId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    BloodGroupId = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    InvalidationReason = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationLines", x => x.Id);
                    table.CheckConstraint("CK_ReservationLines_Invalidation", "([Status] = 'Invalidated' AND [InvalidationReason] IS NOT NULL) OR ([Status] <> 'Invalidated' AND [InvalidationReason] IS NULL)");
                    table.CheckConstraint("CK_ReservationLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_ReservationLines_Status", "[Status] IN ('Active', 'Consumed', 'Released', 'Invalidated')");
                    table.ForeignKey(
                        name: "FK_ReservationLines_BloodBatches_BatchId_OrganizationId_BloodGroupId",
                        columns: x => new { x.BatchId, x.OrganizationId, x.BloodGroupId },
                        principalTable: "BloodBatches",
                        principalColumns: new[] { "Id", "OrganizationId", "BloodGroupId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLines_Reservations_ReservationId_OrganizationId_BloodGroupId",
                        columns: x => new { x.ReservationId, x.OrganizationId, x.BloodGroupId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "Id", "OrganizationId", "BloodGroupId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReservationLineId",
                table: "InventoryTransactions",
                column: "ReservationLineId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservationLine",
                table: "InventoryTransactions",
                sql: "[TransactionType] NOT IN ('Reserve', 'Release', 'Deduct') OR [ReservationLineId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_BloodGroupId",
                table: "BloodRequests",
                column: "BloodGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_ClientRequestId",
                table: "BloodRequests",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_CreatedByUserId",
                table: "BloodRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_DestinationHospitalId_BloodGroupId_CreatedAt",
                table: "BloodRequests",
                columns: new[] { "DestinationHospitalId", "BloodGroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_DestinationHospitalId_DestinationOrgType",
                table: "BloodRequests",
                columns: new[] { "DestinationHospitalId", "DestinationOrgType" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_DestinationHospitalId_Status",
                table: "BloodRequests",
                columns: new[] { "DestinationHospitalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_RequestNumber",
                table: "BloodRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_Status_RequestType_NeededBy",
                table: "BloodRequests",
                columns: new[] { "Status", "RequestType", "NeededBy" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_VerifiedByUserId",
                table: "BloodRequests",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequestStatusHistory_ChangedByUserId",
                table: "BloodRequestStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequestStatusHistory_RequestId_ToStatus",
                table: "BloodRequestStatusHistory",
                columns: new[] { "RequestId", "ToStatus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_OrganizationId",
                table: "EmergencyOverrides",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_OverriddenByUserId",
                table: "EmergencyOverrides",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_RequestId",
                table: "EmergencyOverrides",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_RequestId_RequestType",
                table: "EmergencyOverrides",
                columns: new[] { "RequestId", "RequestType" });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_ReviewedByUserId",
                table: "EmergencyOverrides",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyOverrides_ReviewStatus",
                table: "EmergencyOverrides",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLines_BatchId_OrganizationId_BloodGroupId",
                table: "ReservationLines",
                columns: new[] { "BatchId", "OrganizationId", "BloodGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLines_BatchId_Status",
                table: "ReservationLines",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLines_ReservationId_OrganizationId_BloodGroupId",
                table: "ReservationLines",
                columns: new[] { "ReservationId", "OrganizationId", "BloodGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CreatedByUserId",
                table: "Reservations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrganizationId",
                table: "Reservations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RequestId_BloodGroupId",
                table: "Reservations",
                columns: new[] { "RequestId", "BloodGroupId" });

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_OneLivePerRequest",
                table: "Reservations",
                column: "RequestId",
                unique: true,
                filter: "[Status] IN ('Active', 'Consumed')");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_ReservationLines_ReservationLineId",
                table: "InventoryTransactions",
                column: "ReservationLineId",
                principalTable: "ReservationLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_ReservationLines_ReservationLineId",
                table: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "BloodRequestStatusHistory");

            migrationBuilder.DropTable(
                name: "EmergencyOverrides");

            migrationBuilder.DropTable(
                name: "ReservationLines");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ReservationLineId",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservationLine",
                table: "InventoryTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_BloodBatches_Id_OrganizationId_BloodGroupId",
                table: "BloodBatches");

            migrationBuilder.DropColumn(
                name: "ReservationLineId",
                table: "InventoryTransactions");

            migrationBuilder.DropSequence(
                name: "BloodRequestNumbers");
        }
    }
}
