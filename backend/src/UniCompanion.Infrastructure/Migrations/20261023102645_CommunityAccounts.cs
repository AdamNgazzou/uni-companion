using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunityAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedReplyId",
                table: "CommunityPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditedAt",
                table: "CommunityPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CommunityPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "CommunityPosts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CommunityBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    BlockedUserId = table.Column<string>(type: "text", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityBlocks_AspNetUsers_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityBlocks_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityReports_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedItems_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThreadSubscriptions",
                columns: table => new
                {
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadSubscriptions", x => new { x.ThreadId, x.OwnerId });
                    table.ForeignKey(
                        name: "FK_ThreadSubscriptions_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ThreadSubscriptions_CommunityPosts_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AuthorId_CreatedAt_Id",
                table: "CommunityPosts",
                columns: new[] { "AuthorId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBlocks_BlockedUserId",
                table: "CommunityBlocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityBlocks_OwnerId_BlockedUserId_IsAnonymous",
                table: "CommunityBlocks",
                columns: new[] { "OwnerId", "BlockedUserId", "IsAnonymous" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReports_PostId_ReporterId",
                table: "CommunityReports",
                columns: new[] { "PostId", "ReporterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReports_ReporterId",
                table: "CommunityReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityReports_State_CreatedAt",
                table: "CommunityReports",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_OwnerId_CreatedAt_Id",
                table: "SavedItems",
                columns: new[] { "OwnerId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_OwnerId_Kind_TargetId",
                table: "SavedItems",
                columns: new[] { "OwnerId", "Kind", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadSubscriptions_OwnerId",
                table: "ThreadSubscriptions",
                column: "OwnerId");
            migrationBuilder.Sql("""
                INSERT INTO "ThreadSubscriptions" ("ThreadId", "OwnerId", "Enabled")
                    SELECT DISTINCT COALESCE("ParentId", "Id"), "AuthorId", TRUE FROM "CommunityPosts" WHERE NOT "IsSample";
                UPDATE "CommunityPosts" SET "RequestHash" = encode(sha256(convert_to(
                    jsonb_build_array("ParentId"::text, "CourseId"::text, "Kind", "IsAnonymous", "Title", "Body")::text, 'UTF8')), 'hex');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityBlocks");

            migrationBuilder.DropTable(
                name: "CommunityReports");

            migrationBuilder.DropTable(
                name: "SavedItems");

            migrationBuilder.DropTable(
                name: "ThreadSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_AuthorId_CreatedAt_Id",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "AcceptedReplyId",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CommunityPosts");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "CommunityPosts");
        }
    }
}
